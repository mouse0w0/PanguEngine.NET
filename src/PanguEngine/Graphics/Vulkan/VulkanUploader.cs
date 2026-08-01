using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Render-thread staging upload system that batches GPU buffer and texture transfers.
/// </summary>
public static unsafe class VulkanUploader
{
    private abstract class PendingUpload
    {
        private bool _holdReleased;

        internal required VulkanUploadHandle Handle { get; init; }

        protected abstract VulkanResourceLifetime Lifetime { get; }

        internal bool IsTerminal { get; private set; }

        internal bool IsPlanned { get; set; }

        internal void SignalFailure(Exception exception)
        {
            SignalFailureHandle(exception);
            ReleaseHoldOnce(null);
        }

        internal void SignalSubmittedFailure(Exception exception, ulong submissionValue)
        {
            SignalFailureHandle(exception);
            ReleaseHoldOnce(submissionValue);
        }

        internal void BindSubmission(ulong submissionValue)
        {
            ReleaseHoldOnce(submissionValue);
        }

        private void SignalFailureHandle(Exception exception)
        {
            if (IsTerminal)
                return;

            IsTerminal = true;
            Handle.SignalFailure(exception);
        }

        private void ReleaseHoldOnce(ulong? submissionValue)
        {
            if (_holdReleased)
                return;

            _holdReleased = true;
            if (submissionValue.HasValue)
                Lifetime.ReleaseHold(submissionValue.Value);
            else
                Lifetime.ReleaseHold();
        }
    }

    private sealed class PendingBufferUpload : PendingUpload
    {
        internal required VulkanBuffer Dst { get; init; }

        internal required byte[] Data { get; init; }

        internal ulong DstOffset { get; init; }

        internal ulong Size { get; init; }

        internal VulkanStagingSegment? Segment { get; set; }

        protected override VulkanResourceLifetime Lifetime => Dst.Lifetime;
    }

    private sealed class PendingTextureUpload : PendingUpload
    {
        internal required VulkanTexture Dst { get; init; }

        internal required byte[] Data { get; init; }

        internal ulong Size { get; init; }

        internal TextureUploadRegion Region { get; init; }

        internal VulkanStagingSegment? Segment { get; set; }

        internal List<PlannedTextureLayer> Layers { get; } = [];

        protected override VulkanResourceLifetime Lifetime => Dst.Lifetime;
    }

    private sealed class PendingMipmapGeneration : PendingUpload
    {
        internal required VulkanTexture Texture { get; init; }

        internal List<PlannedMipmapStep> Steps { get; } = [];

        protected override VulkanResourceLifetime Lifetime => Texture.Lifetime;
    }

    private readonly record struct PlannedTextureLayer(
        uint MipLevel,
        uint ArrayLayer,
        ImageLayout OldLayout,
        VulkanImageState FinalState);

    private readonly record struct PlannedMipmapStep(
        uint ArrayLayer,
        uint SourceMip,
        uint DestinationMip,
        ImageLayout SourceOldLayout,
        ImageLayout DestinationOldLayout,
        VulkanImageState FinalState);

    private const ulong DefaultStagingSize = 4 * 1024 * 1024;

    private static readonly Queue<PendingUpload> PendingOperations = new();
    private static bool _initialized;
    private static bool _destroying;
    private static bool _faulted;
    private static Exception? _faultException;
    private static VulkanStagingPagePool _stagingPages = null!;
    private static VulkanUploadPacketRing _packetRing = null!;
    private static CommandPool _commandPool;

    /// <summary>
    /// Initializes the staging uploader with a persistent page pool and three upload packets.
    /// </summary>
    /// <param name="initialSize">The regular staging page size in bytes. Defaults to 4 MiB.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialSize"/> is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is already initialized.</exception>
    public static void Initialize(ulong initialSize = DefaultStagingSize)
    {
        VulkanContext.EnsureRenderThread();

        if (_initialized)
            throw new InvalidOperationException("VulkanUploader is already initialized.");
        if (initialSize == 0)
            throw new ArgumentOutOfRangeException(nameof(initialSize),
                "Initial staging page size must be greater than zero.");

        CommandPool commandPool = default;
        var commandPoolCreated = false;
        VulkanStagingPagePool? stagingPages = null;

        try
        {
            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = VulkanContext.GraphicsQueueFamily
            };
            if (VulkanContext.Vk.CreateCommandPool(
                    VulkanContext.Device,
                    in poolInfo,
                    null,
                    out commandPool) != Result.Success)
                throw new InvalidOperationException("Failed to create staging upload command pool.");
            commandPoolCreated = true;

            CommandBufferAllocateInfo allocateInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)VulkanUploadPacketRing.Capacity
            };
            var commandBuffers = new CommandBuffer[VulkanUploadPacketRing.Capacity];
            fixed (CommandBuffer* commandBufferPointer = commandBuffers)
            {
                if (VulkanContext.Vk.AllocateCommandBuffers(
                        VulkanContext.Device,
                        in allocateInfo,
                        commandBufferPointer) != Result.Success)
                    throw new InvalidOperationException("Failed to allocate staging upload command buffers.");
            }

            stagingPages = new VulkanStagingPagePool(initialSize);

            _stagingPages = stagingPages;
            _packetRing = new VulkanUploadPacketRing(commandBuffers);
            _commandPool = commandPool;
            _destroying = false;
            _initialized = true;
        }
        catch
        {
            stagingPages?.Destroy();
            if (commandPoolCreated)
                VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, commandPool, null);
            throw;
        }
    }

    /// <summary>
    /// Destroys the staging uploader and fails all requests that have not been submitted.
    /// </summary>
    public static void Destroy()
    {
        VulkanContext.EnsureRenderThread();

        if (!_initialized)
            return;

        _destroying = true;
        var disposedException = new ObjectDisposedException(nameof(VulkanUploader));
        var pendingFailure = FailPendingOperations(disposedException);
        if (pendingFailure != null)
            EnterFaulted(pendingFailure);

        var waitResult = VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
        if (waitResult == Result.ErrorDeviceLost)
        {
            var deviceLostException = new InvalidOperationException(
                $"Failed to wait for the Vulkan device during uploader destruction: {waitResult}.");
            EnterFaulted(deviceLostException);
            _packetRing.FaultSubmittedHandles(_faultException ?? deviceLostException);
            _packetRing.ReclaimAll(
                _stagingPages,
                completeSuccessfully: false,
                exception: _faultException ?? deviceLostException);
            DestroyResourcesAndReset();
            return;
        }

        if (waitResult != Result.Success)
        {
            var exception = new InvalidOperationException(
                $"Failed to wait for the Vulkan device during uploader destruction: {waitResult}.");
            EnterFaulted(exception);
            _packetRing.FaultSubmittedHandles(_faultException ?? exception);
            _destroying = false;
            throw exception;
        }

        if (_faulted)
        {
            var exception = _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");
            _packetRing.FaultSubmittedHandles(exception);
            _packetRing.ReclaimAll(_stagingPages, completeSuccessfully: false, exception: exception);
        }
        else
        {
            _packetRing.ReclaimAll(_stagingPages, completeSuccessfully: true, exception: null);
        }

        DestroyResourcesAndReset();
    }

    internal static VulkanUploadHandle EnqueueBufferUpload<T>(
        VulkanBuffer dst,
        ReadOnlySpan<T> data,
        ulong dstOffset = 0) where T : unmanaged
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(dst);

        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));
        if (dataSize == 0)
            return CreateSucceededHandle();
        if (!dst.Usage.HasFlag(BufferUsageFlags.TransferDstBit))
            throw new ArgumentException("Destination buffer must have TransferDst usage.", nameof(dst));
        if (dstOffset > dst.Size || dataSize > dst.Size - dstOffset)
            throw new ArgumentOutOfRangeException(nameof(dstOffset),
                "Destination offset and data size exceed the buffer bounds.");

        var dataCopy = new byte[dataSize];
        data.CopyTo(MemoryMarshal.Cast<byte, T>(dataCopy.AsSpan()));
        var upload = new PendingBufferUpload
        {
            Dst = dst,
            Data = dataCopy,
            DstOffset = dstOffset,
            Size = dataSize,
            Handle = new VulkanUploadHandle()
        };

        var lifetime = dst.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), dst);
        try
        {
            EnsureCanEnqueue();
            PendingOperations.Enqueue(upload);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return upload.Handle;
    }

    internal static VulkanUploadHandle EnqueueTextureUpload(
        VulkanTexture dst,
        ReadOnlySpan<byte> data,
        TextureUploadRegion region)
    {
        VulkanContext.EnsureRenderThread();
        if (data.Length == 0)
            throw new ArgumentException("Texture upload data must not be empty.", nameof(data));

        ArgumentNullException.ThrowIfNull(dst);
        dst.ThrowIfDestroyed();

        var upload = new PendingTextureUpload
        {
            Dst = dst,
            Data = data.ToArray(),
            Size = (ulong)data.Length,
            Region = region,
            Handle = new VulkanUploadHandle()
        };

        var lifetime = dst.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), dst);
        try
        {
            EnsureCanEnqueue();
            PendingOperations.Enqueue(upload);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return upload.Handle;
    }

    internal static VulkanUploadHandle EnqueueMipmapGeneration(VulkanTexture texture)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(texture);
        texture.ThrowIfDestroyed();

        var generation = new PendingMipmapGeneration
        {
            Texture = texture,
            Handle = new VulkanUploadHandle()
        };

        var lifetime = texture.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), texture);
        try
        {
            EnsureCanEnqueue();
            PendingOperations.Enqueue(generation);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return generation.Handle;
    }

    /// <summary>
    /// Retires completed upload packets and submits at most one pending upload batch.
    /// </summary>
    /// <remarks>
    /// The render thread may block when all three upload packets are still in flight. A resource submitted by this
    /// method can be consumed by a later submission on the same graphics queue before its handle is completed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is not initialized or has faulted.</exception>
    public static void Pump()
    {
        EnsureCanPump();
        var timelineValue = QueryTimeline();
        _packetRing.RetireCompleted(timelineValue, _stagingPages);
        if (PendingOperations.Count == 0)
            return;

        if (_packetRing.NextPacket.IsSubmitted)
        {
            var semaphore = VulkanContext.GlobalTimelineSemaphore;
            var submissionValue = _packetRing.NextPacket.SubmissionValue;
            SemaphoreWaitInfo waitInfo = new()
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = 1,
                PSemaphores = &semaphore,
                PValues = &submissionValue
            };
            var waitResult = VulkanContext.Vk.WaitSemaphores(
                VulkanContext.Device,
                in waitInfo,
                ulong.MaxValue);
            if (waitResult != Result.Success)
                FailPendingAndSubmitted(new InvalidOperationException(
                    $"Failed to wait for a Vulkan upload packet: {waitResult}."));

            timelineValue = QueryTimeline();
            _packetRing.RetireCompleted(timelineValue, _stagingPages);
        }

        _packetRing.EnsureNextAvailable();
        SubmitPendingBatch();
    }

    private static ulong QueryTimeline()
    {
        var result = VulkanContext.Vk.GetSemaphoreCounterValue(
            VulkanContext.Device,
            VulkanContext.GlobalTimelineSemaphore,
            out var timelineValue);
        if (result != Result.Success)
            FailPendingAndSubmitted(new InvalidOperationException(
                $"Failed to query the global Vulkan timeline: {result}."));

        return timelineValue;
    }

    private static void SubmitPendingBatch()
    {
        var textureStates = new Dictionary<VulkanTexture, VulkanUploadLayoutState>(
            ReferenceEqualityComparer.Instance);
        var batch = DrainPendingUploads();
        VulkanStagingLease? lease = null;
        ulong submissionValue = 0;
        var packetCommitted = false;

        try
        {
            lease = _stagingPages.BeginBatch();
            PlanBatch(batch, textureStates, lease);
            if (!batch.Any(operation => operation.IsPlanned && !operation.IsTerminal))
            {
                _stagingPages.Recycle(lease);
                return;
            }

            CopyDataToStaging(batch);
            _stagingPages.FlushWrittenRanges(lease);

            var packet = _packetRing.NextPacket;
            var commandBuffer = packet.CommandBuffer;
            var resetCommandResult = VulkanContext.Vk.ResetCommandBuffer(commandBuffer, 0);
            if (resetCommandResult != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to reset the staging upload command buffer: {resetCommandResult}.");

            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };
            var beginResult = VulkanContext.Vk.BeginCommandBuffer(commandBuffer, in beginInfo);
            if (beginResult != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to begin the staging upload command buffer: {beginResult}.");

            RecordBatch(batch, commandBuffer);

            var endResult = VulkanContext.Vk.EndCommandBuffer(commandBuffer);
            if (endResult != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to end the staging upload command buffer: {endResult}.");

            var handles = batch
                .Where(operation => operation.IsPlanned && !operation.IsTerminal)
                .Select(operation => operation.Handle)
                .ToArray();
            _packetRing.EnsureNextAvailable();
            submissionValue = VulkanContext.NextGlobalTimelineValue();
            var timelineSemaphore = VulkanContext.GlobalTimelineSemaphore;
            TimelineSemaphoreSubmitInfo timelineSubmitInfo = new()
            {
                SType = StructureType.TimelineSemaphoreSubmitInfo,
                SignalSemaphoreValueCount = 1,
                PSignalSemaphoreValues = &submissionValue
            };
            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                PNext = &timelineSubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &timelineSemaphore
            };
            var submitResult = VulkanContext.Vk.QueueSubmit(
                VulkanContext.GraphicsQueue,
                1,
                in submitInfo,
                default);
            if (submitResult is Result.ErrorOutOfHostMemory or Result.ErrorOutOfDeviceMemory)
                throw CreateSubmitException(submitResult);

            _packetRing.CommitNextSubmission(submissionValue, lease, handles);
            packetCommitted = true;
            if (submitResult != Result.Success)
                throw CreateSubmitException(submitResult);

            CommitTextureLayouts(textureStates);
            var bindingFailure = BindSubmittedOperations(batch, submissionValue);
            if (bindingFailure != null)
                throw bindingFailure;

            foreach (var handle in handles)
                handle.SignalReady();
        }
        catch (Exception exception)
        {
            if (packetCommitted)
                FailSubmittedBatch(batch, submissionValue, exception);
            FailUnsubmittedBatch(batch, lease, exception);
        }
    }

    private static InvalidOperationException CreateSubmitException(Result result)
    {
        return new InvalidOperationException($"Failed to submit staging upload commands: {result}.");
    }

    private static List<PendingUpload> DrainPendingUploads()
    {
        List<PendingUpload> batch = new(PendingOperations.Count);
        while (PendingOperations.Count > 0)
            batch.Add(PendingOperations.Dequeue());
        return batch;
    }

    private static void PlanBatch(
        IReadOnlyList<PendingUpload> batch,
        Dictionary<VulkanTexture, VulkanUploadLayoutState> textureStates,
        VulkanStagingLease lease)
    {
        foreach (var operation in batch)
        {
            try
            {
                switch (operation)
                {
                    case PendingBufferUpload bufferUpload:
                        PlanBufferUpload(bufferUpload, lease);
                        break;
                    case PendingTextureUpload textureUpload:
                        PlanTextureUpload(textureUpload, textureStates, lease);
                        break;
                    case PendingMipmapGeneration mipmapGeneration:
                        PlanMipmapGeneration(mipmapGeneration, textureStates);
                        break;
                }
            }
            catch (VulkanStagingPageAllocationException exception)
            {
                operation.SignalFailure(exception);
            }
            catch (OverflowException exception)
            {
                operation.SignalFailure(exception);
            }
        }
    }

    private static void PlanBufferUpload(PendingBufferUpload upload, VulkanStagingLease lease)
    {
        upload.Segment = _stagingPages.Allocate(lease, upload.Size, 1);
        upload.IsPlanned = true;
    }

    private static void PlanTextureUpload(
        PendingTextureUpload upload,
        Dictionary<VulkanTexture, VulkanUploadLayoutState> textureStates,
        VulkanStagingLease lease)
    {
        upload.Segment = _stagingPages.Allocate(
            lease,
            upload.Size,
            VulkanBarrier.GetTextureUploadAlignment(upload.Dst.Format));

        var state = GetTextureState(textureStates, upload.Dst);
        var transaction = state.Clone();
        var finalState = VulkanBarrier.GetTextureUploadDestination(upload.Dst.Usage);
        var layerCount = upload.Dst.Dimension == TextureDimension.Type3D
            ? 1u
            : upload.Region.LayerCount;
        var baseArrayLayer = upload.Dst.Dimension == TextureDimension.Type3D
            ? 0u
            : upload.Region.ArrayLayer;
        for (var layer = 0u; layer < layerCount; layer++)
        {
            var arrayLayer = baseArrayLayer + layer;
            var oldLayout = transaction.Get(upload.Region.MipLevel, arrayLayer);
            upload.Layers.Add(new PlannedTextureLayer(
                upload.Region.MipLevel,
                arrayLayer,
                oldLayout,
                finalState));
            transaction.Set(upload.Region.MipLevel, arrayLayer, finalState.Layout);
        }

        state.Merge(transaction);
        upload.IsPlanned = true;
    }

    private static void PlanMipmapGeneration(
        PendingMipmapGeneration generation,
        Dictionary<VulkanTexture, VulkanUploadLayoutState> textureStates)
    {
        var state = GetTextureState(textureStates, generation.Texture);
        if (!state.AreAllBaseMipsInitialized())
        {
            generation.SignalFailure(
                new InvalidOperationException("Texture base mip has not been initialized for every array layer."));
            return;
        }

        var transaction = state.Clone();
        var finalState = VulkanBarrier.GetTextureUploadDestination(generation.Texture.Usage);
        for (var layer = 0u; layer < transaction.ArrayLayers; layer++)
        {
            for (var sourceMip = 0u; sourceMip < transaction.MipLevels - 1; sourceMip++)
            {
                var destinationMip = sourceMip + 1;
                generation.Steps.Add(new PlannedMipmapStep(
                    layer,
                    sourceMip,
                    destinationMip,
                    transaction.Get(sourceMip, layer),
                    transaction.Get(destinationMip, layer),
                    finalState));
                transaction.Set(sourceMip, layer, finalState.Layout);
                transaction.Set(destinationMip, layer, ImageLayout.TransferDstOptimal);
            }

            transaction.Set(transaction.MipLevels - 1, layer, finalState.Layout);
        }

        state.Merge(transaction);
        generation.IsPlanned = true;
    }

    private static VulkanUploadLayoutState GetTextureState(
        Dictionary<VulkanTexture, VulkanUploadLayoutState> textureStates,
        VulkanTexture texture)
    {
        if (textureStates.TryGetValue(texture, out var state))
            return state;

        var layerCount = texture.Dimension == TextureDimension.Type3D
            ? 1u
            : texture.ArrayLayers;
        state = new VulkanUploadLayoutState(texture.MipLevels, layerCount, texture.GetLayout);
        textureStates.Add(texture, state);
        return state;
    }

    private static void CopyDataToStaging(IReadOnlyList<PendingUpload> batch)
    {
        foreach (var operation in batch)
        {
            if (!operation.IsPlanned || operation.IsTerminal)
                continue;

            switch (operation)
            {
                case PendingBufferUpload bufferUpload:
                {
                    var segment = bufferUpload.Segment ??
                                  throw new InvalidOperationException("Buffer upload has no staging segment.");
                    bufferUpload.Data.AsSpan().CopyTo(
                        new Span<byte>(segment.Destination, bufferUpload.Data.Length));
                    break;
                }
                case PendingTextureUpload textureUpload:
                {
                    var segment = textureUpload.Segment ??
                                  throw new InvalidOperationException("Texture upload has no staging segment.");
                    textureUpload.Data.AsSpan().CopyTo(
                        new Span<byte>(segment.Destination, textureUpload.Data.Length));
                    break;
                }
            }
        }
    }

    private static void RecordBatch(
        IReadOnlyList<PendingUpload> batch,
        CommandBuffer commandBuffer)
    {
        foreach (var operation in batch)
        {
            if (!operation.IsPlanned || operation.IsTerminal)
                continue;

            switch (operation)
            {
                case PendingBufferUpload bufferUpload:
                    RecordBufferUpload(bufferUpload, commandBuffer);
                    break;
                case PendingTextureUpload textureUpload:
                    RecordTextureUpload(textureUpload, commandBuffer);
                    break;
                case PendingMipmapGeneration mipmapGeneration:
                    RecordMipmapGeneration(mipmapGeneration, commandBuffer);
                    break;
            }
        }
    }

    private static void RecordBufferUpload(
        PendingBufferUpload upload,
        CommandBuffer commandBuffer)
    {
        var segment = upload.Segment ??
                      throw new InvalidOperationException("Buffer upload has no staging segment.");
        BufferCopy copyRegion = new()
        {
            SrcOffset = segment.Offset,
            DstOffset = upload.DstOffset,
            Size = upload.Size
        };
        VulkanContext.Vk.CmdCopyBuffer(
            commandBuffer,
            segment.Buffer.Buffer,
            upload.Dst.Buffer,
            1,
            in copyRegion);
        VulkanBarrier.RecordBufferUploadBarrier(
            commandBuffer,
            upload.Dst.Buffer,
            upload.DstOffset,
            upload.Size,
            upload.Dst.Usage);
    }

    private static void RecordTextureUpload(
        PendingTextureUpload upload,
        CommandBuffer commandBuffer)
    {
        var segment = upload.Segment ??
                      throw new InvalidOperationException("Texture upload has no staging segment.");
        foreach (var layer in upload.Layers)
        {
            VulkanBarrier.RecordImageLayoutTransition(
                commandBuffer,
                upload.Dst.Image,
                layer.MipLevel,
                1,
                layer.ArrayLayer,
                1,
                ImageAspectFlags.ColorBit,
                layer.OldLayout,
                ImageLayout.TransferDstOptimal,
                VulkanBarrier.GetStageForLayout(layer.OldLayout),
                VulkanBarrier.GetAccessForLayout(layer.OldLayout),
                PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferWriteBit);
        }

        var layerCount = upload.Dst.Dimension == TextureDimension.Type3D
            ? 1u
            : upload.Region.LayerCount;
        var baseArrayLayer = upload.Dst.Dimension == TextureDimension.Type3D
            ? 0u
            : upload.Region.ArrayLayer;
        BufferImageCopy copyRegion = new()
        {
            BufferOffset = segment.Offset,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = upload.Region.MipLevel,
                BaseArrayLayer = baseArrayLayer,
                LayerCount = layerCount
            },
            ImageOffset = new Offset3D
            {
                X = checked((int)upload.Region.X),
                Y = checked((int)upload.Region.Y),
                Z = checked((int)upload.Region.Z)
            },
            ImageExtent = new Extent3D
            {
                Width = upload.Region.Width,
                Height = upload.Region.Height,
                Depth = upload.Region.Depth
            }
        };
        VulkanContext.Vk.CmdCopyBufferToImage(
            commandBuffer,
            segment.Buffer.Buffer,
            upload.Dst.Image,
            ImageLayout.TransferDstOptimal,
            1,
            in copyRegion);

        var finalState = upload.Layers[0].FinalState;
        VulkanBarrier.RecordImageLayoutTransition(
            commandBuffer,
            upload.Dst.Image,
            upload.Region.MipLevel,
            1,
            baseArrayLayer,
            layerCount,
            ImageAspectFlags.ColorBit,
            ImageLayout.TransferDstOptimal,
            finalState.Layout,
            PipelineStageFlags2.TransferBit,
            AccessFlags2.TransferWriteBit,
            finalState.Stage,
            finalState.Access);
    }

    private static void RecordMipmapGeneration(
        PendingMipmapGeneration generation,
        CommandBuffer commandBuffer)
    {
        var texture = generation.Texture;
        foreach (var step in generation.Steps)
        {
            VulkanBarrier.RecordImageLayoutTransition(
                commandBuffer,
                texture.Image,
                step.SourceMip,
                1,
                step.ArrayLayer,
                1,
                ImageAspectFlags.ColorBit,
                step.SourceOldLayout,
                ImageLayout.TransferSrcOptimal,
                VulkanBarrier.GetStageForLayout(step.SourceOldLayout),
                VulkanBarrier.GetAccessForLayout(step.SourceOldLayout),
                PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferReadBit);
            VulkanBarrier.RecordImageLayoutTransition(
                commandBuffer,
                texture.Image,
                step.DestinationMip,
                1,
                step.ArrayLayer,
                1,
                ImageAspectFlags.ColorBit,
                step.DestinationOldLayout,
                ImageLayout.TransferDstOptimal,
                VulkanBarrier.GetStageForLayout(step.DestinationOldLayout),
                VulkanBarrier.GetAccessForLayout(step.DestinationOldLayout),
                PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferWriteBit);

            var sourceWidth = VulkanTexture.GetMipExtent(texture.Width, step.SourceMip);
            var sourceHeight = texture.Dimension == TextureDimension.Type1D
                ? 1
                : VulkanTexture.GetMipExtent(texture.Height, step.SourceMip);
            var destinationWidth = VulkanTexture.GetMipExtent(texture.Width, step.DestinationMip);
            var destinationHeight = texture.Dimension == TextureDimension.Type1D
                ? 1
                : VulkanTexture.GetMipExtent(texture.Height, step.DestinationMip);
            ImageBlit blit = new()
            {
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = step.SourceMip,
                    BaseArrayLayer = step.ArrayLayer,
                    LayerCount = 1
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = step.DestinationMip,
                    BaseArrayLayer = step.ArrayLayer,
                    LayerCount = 1
                }
            };
            blit.SrcOffsets[0] = new Offset3D(0, 0, 0);
            blit.SrcOffsets[1] = new Offset3D(
                checked((int)sourceWidth),
                checked((int)sourceHeight),
                1);
            blit.DstOffsets[0] = new Offset3D(0, 0, 0);
            blit.DstOffsets[1] = new Offset3D(
                checked((int)destinationWidth),
                checked((int)destinationHeight),
                1);
            VulkanContext.Vk.CmdBlitImage(
                commandBuffer,
                texture.Image,
                ImageLayout.TransferSrcOptimal,
                texture.Image,
                ImageLayout.TransferDstOptimal,
                1,
                in blit,
                Filter.Linear);

            VulkanBarrier.RecordImageLayoutTransition(
                commandBuffer,
                texture.Image,
                step.SourceMip,
                1,
                step.ArrayLayer,
                1,
                ImageAspectFlags.ColorBit,
                ImageLayout.TransferSrcOptimal,
                step.FinalState.Layout,
                PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferReadBit,
                step.FinalState.Stage,
                step.FinalState.Access);

            if (step.DestinationMip == texture.MipLevels - 1)
            {
                VulkanBarrier.RecordImageLayoutTransition(
                    commandBuffer,
                    texture.Image,
                    step.DestinationMip,
                    1,
                    step.ArrayLayer,
                    1,
                    ImageAspectFlags.ColorBit,
                    ImageLayout.TransferDstOptimal,
                    step.FinalState.Layout,
                    PipelineStageFlags2.TransferBit,
                    AccessFlags2.TransferWriteBit,
                    step.FinalState.Stage,
                    step.FinalState.Access);
            }
        }
    }

    private static void CommitTextureLayouts(
        IReadOnlyDictionary<VulkanTexture, VulkanUploadLayoutState> textureStates)
    {
        foreach (var (texture, state) in textureStates)
        {
            foreach (var subresource in state.EnumerateLayouts())
                texture.SetLayout(subresource.MipLevel, subresource.ArrayLayer, subresource.Layout);
        }
    }

    private static Exception? BindSubmittedOperations(
        IReadOnlyList<PendingUpload> batch,
        ulong submissionValue)
    {
        Exception? firstFailure = null;
        foreach (var operation in batch)
        {
            if (!operation.IsPlanned || operation.IsTerminal)
                continue;

            try
            {
                operation.BindSubmission(submissionValue);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        return firstFailure;
    }

    [DoesNotReturn]
    private static void FailUnsubmittedBatch(
        IReadOnlyList<PendingUpload> batch,
        VulkanStagingLease? lease,
        Exception exception)
    {
        EnterFaulted(exception);
        FailOperations(batch, exception, null);
        FailPendingOperations(exception);
        _packetRing.FaultSubmittedHandles(_faultException ?? exception);
        if (lease is { IsRecycled: false })
            _stagingPages.Recycle(lease);
        ThrowFault();
    }

    [DoesNotReturn]
    private static void FailSubmittedBatch(
        IReadOnlyList<PendingUpload> batch,
        ulong submissionValue,
        Exception exception)
    {
        EnterFaulted(exception);
        FailOperations(batch, exception, submissionValue);
        FailPendingOperations(exception);
        _packetRing.FaultSubmittedHandles(_faultException ?? exception);
        ThrowFault();
    }

    [DoesNotReturn]
    private static void FailPendingAndSubmitted(Exception exception)
    {
        EnterFaulted(exception);
        FailPendingOperations(exception);
        _packetRing.FaultSubmittedHandles(_faultException ?? exception);
        ThrowFault();
    }

    private static void FailOperations(
        IReadOnlyList<PendingUpload> operations,
        Exception exception,
        ulong? submissionValue)
    {
        foreach (var operation in operations)
        {
            if (operation.IsTerminal)
                continue;

            try
            {
                if (submissionValue.HasValue)
                    operation.SignalSubmittedFailure(exception, submissionValue.Value);
                else
                    operation.SignalFailure(exception);
            }
            catch (Exception cleanupException)
            {
                EnterFaulted(cleanupException);
            }
        }
    }

    private static Exception? FailPendingOperations(Exception exception)
    {
        Exception? firstFailure = null;
        while (PendingOperations.Count > 0)
        {
            try
            {
                PendingOperations.Dequeue().SignalFailure(exception);
            }
            catch (Exception cleanupException)
            {
                firstFailure ??= cleanupException;
            }
        }

        return firstFailure;
    }

    [DoesNotReturn]
    private static void ThrowFault()
    {
        ExceptionDispatchInfo.Capture(
            _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.")).Throw();
        throw new InvalidOperationException("VulkanUploader fault propagation returned unexpectedly.");
    }

    private static void DestroyResourcesAndReset()
    {
        _stagingPages.Destroy();
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, _commandPool, null);

        PendingOperations.Clear();
        _initialized = false;
        _destroying = false;
        _faulted = false;
        _faultException = null;
        _stagingPages = null!;
        _packetRing = null!;
        _commandPool = default;
    }

    private static void EnsureCanPump()
    {
        VulkanContext.EnsureRenderThread();
        if (!_initialized)
            throw new InvalidOperationException("VulkanUploader is not initialized.");
        ObjectDisposedException.ThrowIf(_destroying, typeof(VulkanUploader));
        if (_faulted)
            ThrowFault();
    }

    private static void EnterFaulted(Exception exception)
    {
        if (_faulted)
            return;

        _faulted = true;
        _faultException = exception;
    }

    private static void EnsureCanEnqueue()
    {
        if (!_initialized)
            throw new InvalidOperationException("VulkanUploader is not initialized.");
        ObjectDisposedException.ThrowIf(_destroying, typeof(VulkanUploader));
        if (_faulted)
            ThrowFault();
    }

    private static VulkanUploadHandle CreateSucceededHandle()
    {
        var handle = new VulkanUploadHandle();
        handle.SignalSuccess();
        return handle;
    }
}
