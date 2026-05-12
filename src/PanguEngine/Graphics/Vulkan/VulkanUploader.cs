using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Vma;
using Buffer = Silk.NET.Vulkan.Buffer;

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
        public UploadHandle Handle { get; init; } = null!;
    }

    /// <summary>
    /// Represents the completion state of a staging buffer upload request.
    /// </summary>
    public sealed class UploadHandle
    {
        private readonly ManualResetEventSlim _event = new(false);
        private volatile bool _completed;
        private ExceptionDispatchInfo? _exception;

        internal UploadHandle()
        {
        }

        /// <summary>
        /// Gets whether the upload has completed (either succeeded or failed).
        /// </summary>
        public bool IsCompleted => _completed;

        internal void SignalSuccess()
        {
            _completed = true;
            _event.Set();
        }

        internal void SignalFailure(Exception exception)
        {
            _exception = ExceptionDispatchInfo.Capture(exception);
            _completed = true;
            _event.Set();
        }

        /// <summary>
        /// Blocks the calling thread until the upload completes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when called from the render submit thread to prevent deadlock.</exception>
        public void Wait()
        {
            if (IsRenderSubmitThread)
                throw new InvalidOperationException(
                    "Cannot wait on an UploadHandle from the render submit thread; this would cause a deadlock.");

            _event.Wait();

            _exception?.Throw();
        }
    }

    private const ulong DefaultStagingSize = 4 * 1024 * 1024;

    private static bool _initialized;
    private static bool _faulted;
    private static Exception? _faultException;
    private static readonly Lock LifecycleLock = new();
    private static readonly ConcurrentQueue<PendingBufferUpload> PendingUploads = new();

    private static int _renderSubmitThreadId;

    private static Buffer _stagingBufferHandle;
    private static Allocation* _stagingAlloc;
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
                Usage = MemoryUsage.CpuToGpu,
            };

            if (VulkanContext.Vk.CreateBuffer(VulkanContext.Device, in bufferInfo, null, out _stagingBufferHandle) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create staging buffer.");
            VulkanAllocator.AllocateMemoryForBuffer(_stagingBufferHandle, allocInfo, out _stagingAlloc);

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
        List<PendingBufferUpload>? remaining = null;

        lock (LifecycleLock)
        {
            if (!_initialized)
                return;

            while (PendingUploads.TryDequeue(out var upload))
            {
                remaining ??= new List<PendingBufferUpload>();
                remaining.Add(upload);
            }

            _initialized = false;
            _faulted = false;
            _faultException = null;
        }

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        VulkanAllocator.DestroyBuffer(_stagingBufferHandle, _stagingAlloc);
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, _commandPool, null);
        VulkanContext.Vk.DestroyFence(VulkanContext.Device, _fence, null);

        if (remaining != null)
        {
            var disposedEx = new ObjectDisposedException(nameof(VulkanUploader));
            foreach (var upload in remaining)
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
    public static UploadHandle EnqueueBufferUpload<T>(
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

        var handle = new UploadHandle();

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
        if (batch.Count == 0)
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
            mapped = VulkanAllocator.Map<byte>(_stagingAlloc);
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            FailBatch(batch, ex);
            return;
        }

        ulong stagingOffset = 0;
        foreach (var upload in batch)
        {
            upload.Data.AsSpan().CopyTo(new Span<byte>(mapped + stagingOffset, (int)upload.Size));
            stagingOffset += upload.Size;
        }

        VulkanAllocator.Unmap(_stagingAlloc);

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
        foreach (var upload in batch)
        {
            BufferCopy copyRegion = new()
            {
                SrcOffset = stagingOffset,
                DstOffset = upload.DstOffset,
                Size = upload.Size,
            };
            VulkanContext.Vk.CmdCopyBuffer(_commandBuffer, _stagingBufferHandle, upload.Dst.Buffer, 1, in copyRegion);
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

        foreach (var upload in batch)
        {
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

    private static List<PendingBufferUpload> DrainPendingUploads()
    {
        var batch = new List<PendingBufferUpload>();
        while (PendingUploads.TryDequeue(out var upload))
        {
            batch.Add(upload);
        }

        return batch;
    }

    private static ulong CalculateTotalSize(List<PendingBufferUpload> batch)
    {
        ulong total = 0;
        foreach (var upload in batch)
        {
            total = checked(total + upload.Size);
        }

        return total;
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
            Usage = MemoryUsage.CpuToGpu,
        };

        if (VulkanContext.Vk.CreateBuffer(VulkanContext.Device, in bufferInfo, null, out var newBuffer) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create larger staging buffer.");
        VulkanAllocator.AllocateMemoryForBuffer(newBuffer, allocInfo, out var newAlloc);

        VulkanAllocator.DestroyBuffer(_stagingBufferHandle, _stagingAlloc);
        _stagingBufferHandle = newBuffer;
        _stagingAlloc = newAlloc;
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

    private static void FailBatch(List<PendingBufferUpload> batch, Exception exception)
    {
        foreach (var upload in batch)
        {
            upload.Handle.SignalFailure(exception);
        }
    }

    private static UploadHandle CreateCompletedHandle()
    {
        var handle = new UploadHandle();
        handle.SignalSuccess();
        return handle;
    }
}