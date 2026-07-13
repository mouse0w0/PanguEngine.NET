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
    private bool _destroyed;

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

    /// <summary>
    /// Gets whether the buffer has been destroyed.
    /// </summary>
    public override bool IsDestroyed => _destroyed;

    internal VulkanBuffer(VkBuffer buffer, Allocation* allocation, ulong size, BufferUsageFlags usage)
    {
        Buffer = buffer;
        _allocation = allocation;
        Size = size;
        Usage = usage;
    }

    /// <summary>
    /// Maps the buffer memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <returns>A pointer to the mapped memory.</returns>
    internal T* Map<T>() where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);

        return VulkanAllocator.Map<T>(_allocation);
    }

    /// <summary>
    /// Unmaps the buffer memory.
    /// </summary>
    internal void Unmap()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);

        VulkanAllocator.Unmap(_allocation);
    }

    internal void PersistentlyMapForWrite()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
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
        ObjectDisposedException.ThrowIf(_destroyed, this);
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
        if (_destroyed) return;

        if (_persistentlyMapped)
        {
            _mappedData = null;
            _persistentlyMapped = false;
        }

        _destroyed = true;

        var buffer = Buffer;
        var allocation = _allocation;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue, () => VulkanAllocator.DestroyBuffer(buffer, allocation));
    }
}