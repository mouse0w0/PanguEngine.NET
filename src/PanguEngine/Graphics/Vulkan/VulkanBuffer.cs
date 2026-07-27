using Silk.NET.Vulkan;
using Vma;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// A Vulkan buffer with bound GPU memory.
/// </summary>
public sealed unsafe class VulkanBuffer : Buffer
{
    private readonly Allocation* _allocation;
    private byte* _mappedData;
    private bool _persistentlyMapped;

    /// <summary>
    /// The Vulkan buffer handle.
    /// </summary>
    public VkBuffer Buffer { get; }

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public override ulong Size { get; }

    /// <summary>
    /// The Vulkan buffer usage flags.
    /// </summary>
    public BufferUsageFlags Usage { get; }

    internal VulkanBuffer(VkBuffer buffer, Allocation* allocation, ulong size, BufferUsageFlags usage)
    {
        Buffer = buffer;
        _allocation = allocation;
        Size = size;
        Usage = usage;
        Lifetime = new VulkanResourceLifetime(
            this,
            () => VulkanAllocator.DestroyBuffer(buffer, allocation),
            VulkanDeletionQueue.Enqueue);
    }

    internal VulkanResourceLifetime Lifetime { get; }

    /// <summary>
    /// Maps the buffer memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <returns>A pointer to the mapped memory.</returns>
    internal T* Map<T>() where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        return VulkanAllocator.Map<T>(_allocation);
    }

    /// <summary>
    /// Unmaps the buffer memory.
    /// </summary>
    internal void Unmap()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        VulkanAllocator.Unmap(_allocation);
    }

    /// <summary>
    /// Flushes CPU writes to the buffer allocation so they are visible to the device.
    /// </summary>
    /// <param name="offset">The byte offset relative to the allocation.</param>
    /// <param name="size">The number of bytes to flush.</param>
    internal void Flush(ulong offset, ulong size)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        VulkanAllocator.Flush(_allocation, offset, size);
    }

    internal void PersistentlyMapForWrite()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        if (_persistentlyMapped)
            return;

        _mappedData = Map<byte>();
        _persistentlyMapped = true;
    }

    /// <inheritdoc/>
    public override void Write<T>(in T value, ulong destinationOffset = 0)
    {
        var copy = value;
        Write(new ReadOnlySpan<T>(&copy, 1), destinationOffset);
    }

    /// <inheritdoc/>
    public override void Write<T>(ReadOnlySpan<T> data, ulong destinationOffset = 0)
    {
        VulkanContext.EnsureRenderThread();
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        if (!_persistentlyMapped)
            throw new InvalidOperationException(
                "Buffer.Write requires a buffer created with CpuToGpu memory usage.");

        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));
        if (destinationOffset > Size || dataSize > Size - destinationOffset)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset),
                "Destination offset and data size exceed the buffer bounds.");
        if (dataSize == 0)
            return;

        fixed (T* source = data)
        {
            System.Buffer.MemoryCopy(source, _mappedData + destinationOffset, Size - destinationOffset, dataSize);
        }
    }

    /// <summary>
    /// Destroys the buffer and frees its GPU memory.
    /// </summary>
    public override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (IsDestroyed) return;

        if (_persistentlyMapped)
        {
            VulkanAllocator.Unmap(_allocation);
            _mappedData = null;
            _persistentlyMapped = false;
        }

        MarkDestroyed();
        Lifetime.RequestDestroy();
    }
}