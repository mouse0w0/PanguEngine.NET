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
    public T* Map<T>() where T : unmanaged
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanBuffer));

        return VulkanAllocator.Map<T>(_allocation);
    }

    /// <summary>
    /// Unmaps the buffer memory.
    /// </summary>
    public void Unmap()
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanBuffer));

        VulkanAllocator.Unmap(_allocation);
    }

    /// <summary>
    /// Destroys the buffer and frees its GPU memory.
    /// </summary>
    public override void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        var buffer = Buffer;
        var allocation = _allocation;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue, () => VulkanAllocator.DestroyBuffer(buffer, allocation));
    }
}