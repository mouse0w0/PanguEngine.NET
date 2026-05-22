using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Vma;
using VmaMemoryUsage = Vma.MemoryUsage;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Thread-safe staging buffer upload system that batches GPU buffer transfers.
/// Worker threads enqueue upload requests; the render submit thread executes them in a single flush.
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

    private const ulong DefaultStagingSize = 4 * 1024 * 1024;

    private static bool _initialized;
    private static bool _faulted;
    private static Exception? _faultException;
    private static readonly Lock LifecycleLock = new();
    private static readonly ConcurrentQueue<PendingBufferUpload> PendingUploads = new();
    private static readonly ConcurrentQueue<PendingTextureUpload> PendingTextureUploads = new();

    private static int _renderSubmitThreadId;

    private static VulkanBuffer _stagingBuffer = null!;
    private static CommandPool _commandPool;
    private static CommandBuffer _commandBuffer;
    private static Fence _fence;
    private static ulong _size;

    internal static bool IsRenderSubmitThread =>
        _renderSubmitThreadId != 0 && Environment.CurrentManagedThreadId == _renderSubmitThreadId;

    /// <summary>
    /// Initializes the staging uploader with a host-visible staging buffer, command pool, and fence.
    /// </summary>
    /// <param name="initialSize">The initial staging buffer size in bytes. Defaults to 4 MiB.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialSize"/> is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is already initialized.</exception>
    public static void Initialize(ulong initialSize = DefaultStagingSize)
    {
        lock (LifecycleLock)
        {
            if (_initialized)
                throw new InvalidOperationException("VulkanUploader is already initialized.");

            if (initialSize == 0)
                throw new ArgumentOutOfRangeException(nameof(initialSize),
                    "Initial staging buffer size must be greater than zero.");

            _size = initialSize;

            BufferCreateInfo bufferInfo = new()
            {
                SType = StructureType.BufferCreateInfo,
                Size = _size,
                Usage = BufferUsageFlags.TransferSrcBit,
                SharingMode = SharingMode.Exclusive,
            };

            AllocationCreateInfo allocInfo = new()
            {
                Usage = VmaMemoryUsage.CpuToGpu,
            };

            _stagingBuffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);

            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = VulkanContext.GraphicsQueueFamily,
            };

            if (VulkanContext.Vk.CreateCommandPool(VulkanContext.Device, in poolInfo, null, out _commandPool) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create staging upload command pool.");

            CommandBufferAllocateInfo allocInfo2 = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1,
            };

            var tempBuffer = new CommandBuffer[1];
            fixed (CommandBuffer* ptr = tempBuffer)
            {
                if (VulkanContext.Vk.AllocateCommandBuffers(VulkanContext.Device, in allocInfo2, ptr) !=
                    Result.Success)
                    throw new InvalidOperationException("Failed to allocate staging upload command buffer.");
            }

            _commandBuffer = tempBuffer[0];

            FenceCreateInfo fenceInfo = new()
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit,
            };

            if (VulkanContext.Vk.CreateFence(VulkanContext.Device, in fenceInfo, null, out _fence) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create staging upload fence.");

            _initialized = true;
        }
    }

    /// <summary>
    /// Destroys the staging uploader, releasing all Vulkan resources.
    /// Fails any pending upload futures with <see cref="ObjectDisposedException"/>.
    /// </summary>
    public static void Destroy()
    {
        List<PendingBufferUpload>? remainingBuffers = null;
        List<PendingTextureUpload>? remainingTextures = null;

        lock (LifecycleLock)
        {
            if (!_initialized)
                return;

            while (PendingUploads.TryDequeue(out var upload))
            {
                remainingBuffers ??= new List<PendingBufferUpload>();
                remainingBuffers.Add(upload);
            }

            while (PendingTextureUploads.TryDequeue(out var upload))
            {
                remainingTextures ??= new List<PendingTextureUpload>();
                remainingTextures.Add(upload);
            }

            _initialized = false;
            _faulted = false;
            _faultException = null;
        }

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
            }
        }

        if (remainingTextures != null)
        {
            var disposedEx = new ObjectDisposedException(nameof(VulkanUploader));
            foreach (var upload in remainingTextures)
            {
                upload.Handle.SignalFailure(disposedEx);
            }
        }
    }

    /// <summary>
    /// Enqueues a buffer upload for later batch execution by <see cref="FlushPendingUploads"/>.
    /// Thread-safe; may be called from any thread.
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
        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));

        if (dataSize == 0)
            return CreateCompletedHandle();

        lock (LifecycleLock)
        {
            if (!_initialized)
                throw new InvalidOperationException("VulkanUploader is not initialized.");

            if (_faulted)
                throw _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");
        }

        if (dst == null)
            throw new ArgumentNullException(nameof(dst));

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
            Handle = handle,
        };

        PendingUploads.Enqueue(upload);

        return handle;
    }

    internal static VulkanUploadHandle EnqueueTextureUpload(
        VulkanTexture dst,
        ReadOnlySpan<byte> data,
        TextureUploadRegion region)
    {
        if (data.Length == 0)
            throw new ArgumentException("Texture upload data must not be empty.", nameof(data));

        lock (LifecycleLock)
        {
            if (!_initialized)
                throw new InvalidOperationException("VulkanUploader is not initialized.");

            if (_faulted)
                throw _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");
        }

        if (dst == null)
            throw new ArgumentNullException(nameof(dst));

        if (dst.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanTexture));
        ValidateTextureUploadRegion(dst, region);

        var dataCopy = data.ToArray();
        var handle = new VulkanUploadHandle();
        PendingTextureUploads.Enqueue(new PendingTextureUpload
        {
            Dst = dst,
            Data = dataCopy,
            Size = (ulong)dataCopy.Length,
            Region = region,
            Handle = handle,
        });

        return handle;
    }

    /// <summary>
    /// Executes all pending uploads by recording copy commands, submitting to the GPU, and waiting for completion.
    /// Must only be called from a single dedicated thread (the render submit thread).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the uploader is not initialized, is faulted, or is called from the wrong thread.</exception>
    public static void FlushPendingUploads()
    {
        if (!_initialized)
            throw new InvalidOperationException("VulkanUploader is not initialized.");

        if (_faulted)
            throw _faultException ?? new InvalidOperationException("VulkanUploader is in a faulted state.");

        BindRenderSubmitThread();

        var batch = DrainPendingUploads();
        if (batch.Buffers.Count == 0 && batch.Textures.Count == 0)
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
        foreach (var upload in batch.Buffers)
        {
            stagingOffset = AlignStagingOffset(stagingOffset);
            upload.Data.AsSpan().CopyTo(new Span<byte>(mapped + stagingOffset, (int)upload.Size));
            stagingOffset += upload.Size;
        }

        foreach (var upload in batch.Textures)
        {
            stagingOffset = AlignStagingOffset(stagingOffset);
            upload.Data.AsSpan().CopyTo(new Span<byte>(mapped + stagingOffset, (int)upload.Size));
            stagingOffset += upload.Size;
        }

        _stagingBuffer.Unmap();

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
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        if (VulkanContext.Vk.BeginCommandBuffer(_commandBuffer, in beginInfo) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to begin staging command buffer.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        stagingOffset = 0;
        foreach (var upload in batch.Buffers)
        {
            stagingOffset = AlignStagingOffset(stagingOffset);
            BufferCopy copyRegion = new()
            {
                SrcOffset = stagingOffset,
                DstOffset = upload.DstOffset,
                Size = upload.Size,
            };
            VulkanContext.Vk.CmdCopyBuffer(_commandBuffer, _stagingBuffer.Buffer, upload.Dst.Buffer, 1, in copyRegion);
            stagingOffset += upload.Size;
        }

        foreach (var upload in batch.Textures)
        {
            stagingOffset = AlignStagingOffset(stagingOffset);
            RecordTextureUpload(upload, stagingOffset);
            stagingOffset += upload.Size;
        }

        if (VulkanContext.Vk.EndCommandBuffer(_commandBuffer) != Result.Success)
        {
            var ex = new InvalidOperationException("Failed to end staging command buffer.");
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        var cmdBuffer = _commandBuffer;
        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmdBuffer,
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
            FailBatch(batch, ex);
            return;
        }

        foreach (var upload in batch.Buffers)
        {
            upload.Handle.SignalSuccess();
        }

        foreach (var upload in batch.Textures)
        {
            upload.Dst.CompleteUpload(ImageLayout.ShaderReadOnlyOptimal);
            upload.Handle.SignalSuccess();
        }
    }

    private static void BindRenderSubmitThread()
    {
        var currentId = Environment.CurrentManagedThreadId;
        if (_renderSubmitThreadId == 0)
        {
            _renderSubmitThreadId = currentId;
        }
        else if (_renderSubmitThreadId != currentId)
        {
            throw new InvalidOperationException(
                "FlushPendingUploads must be called from the same thread that called it the first time.");
        }
    }

    private static (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures) DrainPendingUploads()
    {
        var buffers = new List<PendingBufferUpload>();
        while (PendingUploads.TryDequeue(out var upload))
        {
            buffers.Add(upload);
        }

        var textures = new List<PendingTextureUpload>();
        while (PendingTextureUploads.TryDequeue(out var upload))
        {
            textures.Add(upload);
        }

        return (buffers, textures);
    }

    private static ulong CalculateTotalSize(
        (List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures) batch)
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

    private static void ValidateTextureUploadRegion(VulkanTexture texture, TextureUploadRegion region)
    {
        if (region.X != 0)
            throw new ArgumentOutOfRangeException(nameof(region.X), "Texture upload X must be zero.");
        if (region.Y != 0)
            throw new ArgumentOutOfRangeException(nameof(region.Y), "Texture upload Y must be zero.");
        if (region.Z != 0)
            throw new ArgumentOutOfRangeException(nameof(region.Z), "Texture upload Z must be zero.");
        if (region.MipLevel != 0)
            throw new ArgumentOutOfRangeException(nameof(region.MipLevel), "Texture upload mip level must be zero.");
        if (region.ArrayLayer != 0)
            throw new ArgumentOutOfRangeException(nameof(region.ArrayLayer),
                "Texture upload array layer must be zero.");
        if (region.LayerCount == 0 || region.LayerCount > texture.ArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(region.LayerCount),
                "Texture upload layer count is out of range.");
        if (region.Width == 0 || region.Width > texture.Width)
            throw new ArgumentOutOfRangeException(nameof(region.Width), "Texture upload width is out of range.");
        if (region.Height == 0 || region.Height > texture.Height)
            throw new ArgumentOutOfRangeException(nameof(region.Height), "Texture upload height is out of range.");
        if (region.Depth == 0 || region.Depth > texture.Depth)
            throw new ArgumentOutOfRangeException(nameof(region.Depth), "Texture upload depth is out of range.");
        if (texture.Dimension == TextureDimension.Type3D && region.LayerCount != 1)
            throw new ArgumentOutOfRangeException(nameof(region.LayerCount),
                "3D texture upload layer count must be one.");
        if (region.Width != texture.Width || region.Height != texture.Height || region.Depth != texture.Depth ||
            region.LayerCount != texture.ArrayLayers)
            throw new ArgumentException("Texture upload region must cover the full base level.", nameof(region));
    }

    private static void RecordTextureUpload(PendingTextureUpload upload, ulong stagingOffset)
    {
        ImageMemoryBarrier2 toTransferBarrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TopOfPipeBit,
            SrcAccessMask = AccessFlags2.None,
            DstStageMask = PipelineStageFlags2.TransferBit,
            DstAccessMask = AccessFlags2.TransferWriteBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = upload.Dst.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = upload.Region.LayerCount,
            },
        };

        DependencyInfo toTransferDependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &toTransferBarrier,
        };
        VulkanContext.Vk.CmdPipelineBarrier2(_commandBuffer, &toTransferDependency);

        BufferImageCopy copyRegion = new()
        {
            BufferOffset = stagingOffset,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = upload.Dst.Dimension == TextureDimension.Type3D ? 1 : upload.Region.LayerCount,
            },
            ImageOffset = new Offset3D
            {
                X = 0,
                Y = 0,
                Z = 0,
            },
            ImageExtent = new Extent3D
            {
                Width = upload.Region.Width,
                Height = upload.Region.Height,
                Depth = upload.Region.Depth,
            },
        };
        VulkanContext.Vk.CmdCopyBufferToImage(_commandBuffer, _stagingBuffer.Buffer, upload.Dst.Image,
            ImageLayout.TransferDstOptimal, 1, in copyRegion);

        ImageMemoryBarrier2 toShaderReadBarrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = PipelineStageFlags2.FragmentShaderBit,
            DstAccessMask = AccessFlags2.ShaderSampledReadBit,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = upload.Dst.Image,
            SubresourceRange = toTransferBarrier.SubresourceRange,
        };

        DependencyInfo toShaderReadDependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &toShaderReadBarrier,
        };
        VulkanContext.Vk.CmdPipelineBarrier2(_commandBuffer, &toShaderReadDependency);
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
            SharingMode = SharingMode.Exclusive,
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = VmaMemoryUsage.CpuToGpu,
        };

        var newBuffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);

        _stagingBuffer.Destroy();
        _stagingBuffer = newBuffer;
        _size = newSize;
    }

    private static void EnterFaulted(Exception exception)
    {
        lock (LifecycleLock)
        {
            if (_faulted)
                return;
            _faulted = true;
            _faultException = exception;
        }
    }

    private static void FailBatch((List<PendingBufferUpload> Buffers, List<PendingTextureUpload> Textures) batch,
        Exception exception)
    {
        foreach (var upload in batch.Buffers)
        {
            upload.Handle.SignalFailure(exception);
        }

        foreach (var upload in batch.Textures)
        {
            upload.Handle.SignalFailure(exception);
        }
    }

    private static VulkanUploadHandle CreateCompletedHandle()
    {
        var handle = new VulkanUploadHandle();
        handle.SignalSuccess();
        return handle;
    }
}