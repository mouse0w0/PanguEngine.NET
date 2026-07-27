using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Vma;
using VmaMemoryUsage = Vma.MemoryUsage;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Render-thread staging buffer upload system that batches GPU buffer transfers.
/// </summary>
public static unsafe class VulkanUploader
{
    /// <summary>
    /// Internal upload request holding the destination buffer, data, and completion future.
    /// </summary>
    private sealed class PendingBufferUpload
    {
        /// <summary>
        /// The destination buffer to copy data into.
        /// </summary>
        public VulkanBuffer Dst { get; init; } = null!;

        /// <summary>
        /// The data to upload (owned copy).
        /// </summary>
        public byte[] Data { get; init; } = null!;

        /// <summary>
        /// The destination offset in bytes within the buffer.
        /// </summary>
        public ulong DstOffset { get; init; }

        /// <summary>
        /// The size of the upload in bytes.
        /// </summary>
        public ulong Size { get; init; }

        /// <summary>
        /// The future representing this upload's completion.
        /// </summary>
        public VulkanUploadHandle Handle { get; init; } = null!;
    }

    private sealed class PendingTextureUpload
    {
        public VulkanTexture Dst { get; init; } = null!;

        public byte[] Data { get; init; } = null!;

        public ulong Size { get; init; }

        public TextureUploadRegion Region { get; init; }

        public VulkanUploadHandle Handle { get; init; } = null!;
    }

    private sealed class PendingMipmapGeneration
    {
        public VulkanTexture Texture { get; init; } = null!;

        public VulkanUploadHandle Handle { get; init; } = null!;
    }

    private const ulong DefaultStagingSize = 4 * 1024 * 1024;

    private static bool _initialized;
    private static bool _faulted;
    private static Exception? _faultException;
    private static readonly Queue<PendingBufferUpload> PendingUploads = new();
    private static readonly Queue<PendingTextureUpload> PendingTextureUploads = new();
    private static readonly Queue<PendingMipmapGeneration> PendingMipmapGenerations = new();

    private static VulkanBuffer _stagingBuffer = null!;
    private static CommandPool _commandPool;
    private static CommandBuffer _commandBuffer;
    private static Fence _fence;
    private static ulong _size;

    /// <summary>
    /// Initializes the staging uploader with a host-visible staging buffer, command pool, and fence.
    /// </summary>
    /// <param name="initialSize">The initial staging buffer size in bytes. Defaults to 4 MiB.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialSize"/> is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is already initialized.</exception>
    public static void Initialize(ulong initialSize = DefaultStagingSize)
    {
        VulkanContext.EnsureRenderThread();

        if (_initialized)
            throw new InvalidOperationException("VulkanUploader is already initialized.");

        if (initialSize == 0)
            throw new ArgumentOutOfRangeException(nameof(initialSize),
                "Initial staging buffer size must be greater than zero.");

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = initialSize,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive
        };

        CommandPool commandPool = default;
        Fence fence = default;
        var commandPoolCreated = false;
        var fenceCreated = false;

        try
        {
            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = VulkanContext.GraphicsQueueFamily
            };

            if (VulkanContext.Vk.CreateCommandPool(VulkanContext.Device, in poolInfo, null, out commandPool) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create staging upload command pool.");
            commandPoolCreated = true;

            CommandBufferAllocateInfo commandBufferAllocInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            var tempBuffer = new CommandBuffer[1];
            fixed (CommandBuffer* ptr = tempBuffer)
            {
                if (VulkanContext.Vk.AllocateCommandBuffers(
                        VulkanContext.Device, in commandBufferAllocInfo, ptr) != Result.Success)
                    throw new InvalidOperationException("Failed to allocate staging upload command buffer.");
            }

            FenceCreateInfo fenceInfo = new()
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            if (VulkanContext.Vk.CreateFence(VulkanContext.Device, in fenceInfo, null, out fence) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create staging upload fence.");
            fenceCreated = true;

            AllocationCreateInfo allocInfo = new()
            {
                Usage = VmaMemoryUsage.Auto,
                Flags = AllocationCreateFlags.HostAccessSequentialWriteBit
            };
            var stagingBuffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);

            _size = initialSize;
            _stagingBuffer = stagingBuffer;
            _commandPool = commandPool;
            _commandBuffer = tempBuffer[0];
            _fence = fence;
            _initialized = true;
        }
        catch
        {
            if (fenceCreated)
                VulkanContext.Vk.DestroyFence(VulkanContext.Device, fence, null);
            if (commandPoolCreated)
                VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, commandPool, null);
            throw;
        }
    }

    /// <summary>
    /// Destroys the staging uploader, releasing all Vulkan resources.
    /// Fails any pending upload futures with <see cref="ObjectDisposedException"/>.
    /// </summary>
    public static void Destroy()
    {
        VulkanContext.EnsureRenderThread();

        List<PendingBufferUpload>? remainingBuffers = null;
        List<PendingTextureUpload>? remainingTextures = null;
        List<PendingMipmapGeneration>? remainingMipmaps = null;

        if (!_initialized)
            return;

        while (PendingUploads.Count > 0)
        {
            remainingBuffers ??= [];
            remainingBuffers.Add(PendingUploads.Dequeue());
        }

        while (PendingTextureUploads.Count > 0)
        {
            remainingTextures ??= [];
            remainingTextures.Add(PendingTextureUploads.Dequeue());
        }

        while (PendingMipmapGenerations.Count > 0)
        {
            remainingMipmaps ??= [];
            remainingMipmaps.Add(PendingMipmapGenerations.Dequeue());
        }

        _initialized = false;
        _faulted = false;
        _faultException = null;

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        _stagingBuffer.Destroy();
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, _commandPool, null);
        VulkanContext.Vk.DestroyFence(VulkanContext.Device, _fence, null);

        if (remainingBuffers != null)
        {
            var disposedEx = new ObjectDisposedException(nameof(VulkanUploader));
            foreach (var upload in remainingBuffers)
            {
                upload.Handle.SignalFailure(disposedEx);
                upload.Dst.Lifetime.ReleaseHold();
            }
        }

        if (remainingTextures != null)
        {
            var disposedEx = new ObjectDisposedException(nameof(VulkanUploader));
            foreach (var upload in remainingTextures)
            {
                upload.Handle.SignalFailure(disposedEx);
                upload.Dst.Lifetime.ReleaseHold();
            }
        }

        if (remainingMipmaps != null)
        {
            var disposedEx = new ObjectDisposedException(nameof(VulkanUploader));
            foreach (var generation in remainingMipmaps)
            {
                generation.Handle.SignalFailure(disposedEx);
                generation.Texture.Lifetime.ReleaseHold();
            }
        }
    }

    /// <summary>
    /// Enqueues a buffer upload for later batch execution by <see cref="FlushPendingUploads"/>.
    /// Must be called from the render thread.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the data elements.</typeparam>
    /// <param name="dst">The destination buffer. Must have <see cref="BufferUsageFlags.TransferDstBit"/>.</param>
    /// <param name="data">The data to upload.</param>
    /// <param name="dstOffset">The destination byte offset within the buffer.</param>
    /// <returns>An <see cref="UploadHandle"/> that completes when the upload's GPU copy is done.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is not initialized or is faulted.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dst"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dst"/> was not created with <see cref="BufferUsageFlags.TransferDstBit"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dstOffset"/> or data size exceeds the buffer bounds.</exception>
    /// <exception cref="OverflowException">Thrown when the data size calculation overflows.</exception>
    internal static VulkanUploadHandle EnqueueBufferUpload<T>(
        VulkanBuffer dst,
        ReadOnlySpan<T> data,
        ulong dstOffset = 0) where T : unmanaged
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(dst);

        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));

        if (dataSize == 0)
            return CreateCompletedHandle();

        if (!dst.Usage.HasFlag(BufferUsageFlags.TransferDstBit))
            throw new ArgumentException("Destination buffer must have TransferDst usage.", nameof(dst));

        if (dstOffset > dst.Size || dataSize > dst.Size - dstOffset)
            throw new ArgumentOutOfRangeException(nameof(dstOffset),
                "Destination offset and data size exceed the buffer bounds.");

        var dataCopy = new byte[dataSize];
        data.CopyTo(MemoryMarshal.Cast<byte, T>(dataCopy.AsSpan()));

        var handle = new VulkanUploadHandle();

        var upload = new PendingBufferUpload
        {
            Dst = dst,
            Data = dataCopy,
            DstOffset = dstOffset,
            Size = dataSize,
            Handle = handle
        };

        var lifetime = dst.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), dst);
        try
        {
            EnsureCanEnqueue();
            PendingUploads.Enqueue(upload);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return handle;
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

        var dataCopy = data.ToArray();
        var handle = new VulkanUploadHandle();
        var upload = new PendingTextureUpload
        {
            Dst = dst,
            Data = dataCopy,
            Size = (ulong)dataCopy.Length,
            Region = region,
            Handle = handle
        };
        var lifetime = dst.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), dst);
        try
        {
            EnsureCanEnqueue();
            PendingTextureUploads.Enqueue(upload);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return handle;
    }

    internal static VulkanUploadHandle EnqueueMipmapGeneration(VulkanTexture texture)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(texture);

        texture.ThrowIfDestroyed();

        var handle = new VulkanUploadHandle();
        var generation = new PendingMipmapGeneration
        {
            Texture = texture,
            Handle = handle
        };
        var lifetime = texture.Lifetime;
        ObjectDisposedException.ThrowIf(!lifetime.TryAcquireHold(), texture);
        try
        {
            EnsureCanEnqueue();
            PendingMipmapGenerations.Enqueue(generation);
        }
        catch
        {
            lifetime.ReleaseHold();
            throw;
        }

        return handle;
    }

    /// <summary>
    /// Executes all pending uploads by recording copy commands, submitting to the GPU, and waiting for completion.
    /// Must only be called from a single dedicated thread (the render submit thread).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is not initialized, is faulted, or is called from the wrong thread.</exception>
    public static void FlushPendingUploads()
    {
        VulkanContext.EnsureRenderThread();
        if (!_initialized)
            throw new InvalidOperationException("VulkanUploader is not initialized.");

        if (_faulted)
            throw _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");

        var batch = DrainPendingUploads();
        if (batch.Buffers.Count == 0 && batch.Textures.Count == 0 && batch.Mipmaps.Count == 0)
            return;

        ulong totalSize;
        try
        {
            totalSize = CalculateTotalSize(batch);
        }
        catch (OverflowException ex)
        {
            FailBatch(batch, ex);
            return;
        }

        if (totalSize > _size)
        {
            try
            {
                Grow(totalSize);
            }
            catch (Exception ex)
            {
                FailBatch(batch, ex);
                return;
            }
        }

        if (VulkanContext.Vk.WaitForFences(VulkanContext.Device, 1, in _fence, true, ulong.MaxValue) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to wait for staging upload fence before recording.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        if (VulkanContext.Vk.ResetFences(VulkanContext.Device, 1, in _fence) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to reset staging upload fence.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        byte* mapped;
        try
        {
            mapped = _stagingBuffer.Map<byte>();
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        ulong stagingOffset = 0;
        try
        {
            foreach (var upload in batch.Buffers)
            {
                stagingOffset = AlignStagingOffset(stagingOffset);
                upload.Data.AsSpan().CopyTo(new Span<byte>(mapped + stagingOffset, upload.Data.Length));
                stagingOffset += upload.Size;
            }

            foreach (var upload in batch.Textures)
            {
                stagingOffset = AlignStagingOffset(stagingOffset);
                upload.Data.AsSpan().CopyTo(new Span<byte>(mapped + stagingOffset, upload.Data.Length));
                stagingOffset += upload.Size;
            }

            if (totalSize > 0)
                _stagingBuffer.Flush(0, totalSize);
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }
        finally
        {
            _stagingBuffer.Unmap();
        }

        if (VulkanContext.Vk.ResetCommandBuffer(_commandBuffer, 0) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to reset staging command buffer.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        if (VulkanContext.Vk.BeginCommandBuffer(_commandBuffer, in beginInfo) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to begin staging command buffer.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        try
        {
            stagingOffset = 0;
            foreach (var upload in batch.Buffers)
            {
                stagingOffset = AlignStagingOffset(stagingOffset);
                BufferCopy copyRegion = new()
                {
                    SrcOffset = stagingOffset,
                    DstOffset = upload.DstOffset,
                    Size = upload.Size
                };
                VulkanContext.Vk.CmdCopyBuffer(
                    _commandBuffer, _stagingBuffer.Buffer, upload.Dst.Buffer, 1, in copyRegion);
                stagingOffset += upload.Size;
            }

            foreach (var upload in batch.Textures)
            {
                stagingOffset = AlignStagingOffset(stagingOffset);
                RecordTextureUpload(upload, stagingOffset);
                stagingOffset += upload.Size;
            }

            foreach (var generation in batch.Mipmaps)
                RecordMipmapGeneration(generation);
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        if (VulkanContext.Vk.EndCommandBuffer(_commandBuffer) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to end staging command buffer.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        var submissionValue = VulkanContext.NextGlobalTimelineValue();
        var cmdBuffer = _commandBuffer;
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
            PCommandBuffers = &cmdBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &timelineSemaphore
        };

        var submitResult = VulkanContext.Vk.QueueSubmit(VulkanContext.GraphicsQueue, 1, in submitInfo, _fence);
        if (submitResult != Result.Success)
        {
            var ex = new InvalidOperationException($"Failed to submit staging upload commands: {submitResult}.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        if (VulkanContext.Vk.WaitForFences(VulkanContext.Device, 1, in _fence, true, ulong.MaxValue) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to wait for staging upload completion after submission.");
            EnterFaulted(ex);
            FailBatch(batch, ex, submissionValue);
            return;
        }

        SucceedBatch(batch, submissionValue);
    }

    private static (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures,
        List<PendingMipmapGeneration> Mipmaps) DrainPendingUploads()
    {
        List<PendingBufferUpload> buffers = [];
        while (PendingUploads.Count > 0)
            buffers.Add(PendingUploads.Dequeue());

        List<PendingTextureUpload> textures = [];
        while (PendingTextureUploads.Count > 0)
            textures.Add(PendingTextureUploads.Dequeue());

        List<PendingMipmapGeneration> mipmaps = [];
        while (PendingMipmapGenerations.Count > 0)
            mipmaps.Add(PendingMipmapGenerations.Dequeue());

        return (buffers, textures, mipmaps);
    }

    private static ulong CalculateTotalSize(
        (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures,
            List<PendingMipmapGeneration> Mipmaps) batch)
    {
        ulong total = 0;
        foreach (var upload in batch.Buffers)
        {
            total = AlignStagingOffset(total);
            total = checked(total + upload.Size);
        }

        foreach (var upload in batch.Textures)
        {
            total = AlignStagingOffset(total);
            total = checked(total + upload.Size);
        }

        return total;
    }

    private static ulong AlignStagingOffset(ulong offset)
    {
        return checked((offset + 3UL) & ~3UL);
    }

    private static void RecordTextureUpload(PendingTextureUpload upload, ulong stagingOffset)
    {
        var layerCount = upload.Dst.Dimension == TextureDimension.Type3D ? 1 : upload.Region.LayerCount;
        var baseArrayLayer = upload.Dst.Dimension == TextureDimension.Type3D ? 0 : upload.Region.ArrayLayer;
        for (var layer = 0u; layer < layerCount; layer++)
        {
            var arrayLayer = upload.Dst.Dimension == TextureDimension.Type3D ? 0 : baseArrayLayer + layer;
            var oldLayout = upload.Dst.GetLayout(upload.Region.MipLevel, arrayLayer);
            VulkanBarrier.RecordImageLayoutTransition(_commandBuffer, upload.Dst.Image, upload.Region.MipLevel,
                1, arrayLayer, 1, ImageAspectFlags.ColorBit, oldLayout,
                ImageLayout.TransferDstOptimal, VulkanBarrier.GetStageForLayout(oldLayout),
                VulkanBarrier.GetAccessForLayout(oldLayout), PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferWriteBit);
        }

        BufferImageCopy copyRegion = new()
        {
            BufferOffset = stagingOffset,
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
        VulkanContext.Vk.CmdCopyBufferToImage(_commandBuffer, _stagingBuffer.Buffer, upload.Dst.Image,
            ImageLayout.TransferDstOptimal, 1, in copyRegion);

        VulkanBarrier.RecordImageLayoutTransition(_commandBuffer, upload.Dst.Image, upload.Region.MipLevel,
            1, baseArrayLayer, layerCount, ImageAspectFlags.ColorBit,
            ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, PipelineStageFlags2.TransferBit,
            AccessFlags2.TransferWriteBit, PipelineStageFlags2.FragmentShaderBit, AccessFlags2.ShaderSampledReadBit);

        for (var layer = 0u; layer < layerCount; layer++)
        {
            var arrayLayer = upload.Dst.Dimension == TextureDimension.Type3D ? 0 : baseArrayLayer + layer;
            upload.Dst.SetLayout(upload.Region.MipLevel, arrayLayer, ImageLayout.ShaderReadOnlyOptimal);
        }
    }

    private static void RecordMipmapGeneration(PendingMipmapGeneration generation)
    {
        var texture = generation.Texture;
        var layerCount = texture.Dimension == TextureDimension.Type3D ? 1 : texture.ArrayLayers;

        for (var layer = 0u; layer < layerCount; layer++)
        {
            RecordMipmapGenerationLayer(texture, layer);
        }
    }

    private static void RecordMipmapGenerationLayer(VulkanTexture texture, uint arrayLayer)
    {
        if (texture.GetLayout(0, arrayLayer) == ImageLayout.Undefined)
            throw new InvalidOperationException("Texture base mip has not been initialized.");

        for (var srcMip = 0u; srcMip < texture.MipLevels - 1; srcMip++)
        {
            var dstMip = srcMip + 1;
            TransitionTextureSubresource(texture, srcMip, arrayLayer, ImageLayout.TransferSrcOptimal,
                PipelineStageFlags2.TransferBit, AccessFlags2.TransferReadBit);
            TransitionTextureSubresource(texture, dstMip, arrayLayer, ImageLayout.TransferDstOptimal,
                PipelineStageFlags2.TransferBit, AccessFlags2.TransferWriteBit);

            var srcWidth = VulkanTexture.GetMipExtent(texture.Width, srcMip);
            var srcHeight = texture.Dimension == TextureDimension.Type1D
                ? 1
                : VulkanTexture.GetMipExtent(texture.Height, srcMip);
            var dstWidth = VulkanTexture.GetMipExtent(texture.Width, dstMip);
            var dstHeight = texture.Dimension == TextureDimension.Type1D
                ? 1
                : VulkanTexture.GetMipExtent(texture.Height, dstMip);

            ImageBlit blit = new()
            {
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = srcMip,
                    BaseArrayLayer = arrayLayer,
                    LayerCount = 1
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = dstMip,
                    BaseArrayLayer = arrayLayer,
                    LayerCount = 1
                }
            };
            blit.SrcOffsets[0] = new Offset3D(0, 0, 0);
            blit.SrcOffsets[1] = new Offset3D(checked((int)srcWidth), checked((int)srcHeight), 1);
            blit.DstOffsets[0] = new Offset3D(0, 0, 0);
            blit.DstOffsets[1] = new Offset3D(checked((int)dstWidth), checked((int)dstHeight), 1);

            VulkanContext.Vk.CmdBlitImage(_commandBuffer, texture.Image, ImageLayout.TransferSrcOptimal,
                texture.Image, ImageLayout.TransferDstOptimal, 1, in blit, Filter.Linear);

            TransitionTextureSubresource(texture, srcMip, arrayLayer, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags2.FragmentShaderBit, AccessFlags2.ShaderSampledReadBit);
        }

        TransitionTextureSubresource(texture, texture.MipLevels - 1, arrayLayer, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags2.FragmentShaderBit, AccessFlags2.ShaderSampledReadBit);
    }

    private static void TransitionTextureSubresource(
        VulkanTexture texture,
        uint mipLevel,
        uint arrayLayer,
        ImageLayout newLayout,
        PipelineStageFlags2 dstStage,
        AccessFlags2 dstAccess)
    {
        var oldLayout = texture.GetLayout(mipLevel, arrayLayer);
        if (oldLayout == newLayout)
            return;

        VulkanBarrier.RecordImageLayoutTransition(_commandBuffer, texture.Image, mipLevel, 1, arrayLayer, 1,
            ImageAspectFlags.ColorBit, oldLayout, newLayout,
            VulkanBarrier.GetStageForLayout(oldLayout), VulkanBarrier.GetAccessForLayout(oldLayout), dstStage,
            dstAccess);
        texture.SetLayout(mipLevel, arrayLayer, newLayout);
    }


    private static void Grow(ulong requiredSize)
    {
        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        var newSize = Math.Max(requiredSize, checked(_size * 2));

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = newSize,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = VmaMemoryUsage.Auto,
            Flags = AllocationCreateFlags.HostAccessSequentialWriteBit
        };

        var newBuffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);

        _stagingBuffer.Destroy();
        _stagingBuffer = newBuffer;
        _size = newSize;
    }

    private static void EnterFaulted(Exception exception)
    {
        if (_faulted)
            return;
        _faulted = true;
        _faultException = exception;
    }

    private static void FailBatch(
        (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures,
            List<PendingMipmapGeneration> Mipmaps) batch,
        Exception exception,
        ulong? submissionValue = null)
    {
        foreach (var upload in batch.Buffers)
        {
            upload.Handle.SignalFailure(exception);
        }

        foreach (var upload in batch.Textures)
        {
            upload.Handle.SignalFailure(exception);
        }

        foreach (var generation in batch.Mipmaps)
        {
            generation.Handle.SignalFailure(exception);
        }

        ReleaseBatchHolds(batch, submissionValue);
    }

    private static void SucceedBatch(
        (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures,
            List<PendingMipmapGeneration> Mipmaps) batch,
        ulong submissionValue)
    {
        foreach (var upload in batch.Buffers)
            upload.Handle.SignalSuccess();
        foreach (var upload in batch.Textures)
            upload.Handle.SignalSuccess();
        foreach (var generation in batch.Mipmaps)
            generation.Handle.SignalSuccess();

        ReleaseBatchHolds(batch, submissionValue);
    }

    private static void ReleaseBatchHolds(
        (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures,
            List<PendingMipmapGeneration> Mipmaps) batch,
        ulong? submissionValue)
    {
        foreach (var upload in batch.Buffers)
            ReleaseHold(upload.Dst.Lifetime, submissionValue);
        foreach (var upload in batch.Textures)
            ReleaseHold(upload.Dst.Lifetime, submissionValue);
        foreach (var generation in batch.Mipmaps)
            ReleaseHold(generation.Texture.Lifetime, submissionValue);
    }

    private static void ReleaseHold(VulkanResourceLifetime lifetime, ulong? submissionValue)
    {
        if (submissionValue.HasValue)
            lifetime.ReleaseHold(submissionValue.Value);
        else
            lifetime.ReleaseHold();
    }

    private static void EnsureCanEnqueue()
    {
        if (!_initialized)
            throw new InvalidOperationException("VulkanUploader is not initialized.");
        if (_faulted)
            throw _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");
    }

    private static VulkanUploadHandle CreateCompletedHandle()
    {
        var handle = new VulkanUploadHandle();
        handle.SignalSuccess();
        return handle;
    }
}