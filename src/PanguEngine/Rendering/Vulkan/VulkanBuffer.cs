using Vma;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// A Vulkan buffer with bound GPU memory.
/// </summary>
public sealed unsafe class VulkanBuffer
{
    /// <summary>
    /// The Vulkan buffer handle.
    /// </summary>
    public Buffer Buffer { get; }

    private readonly VulkanAllocator _allocator;
    private readonly Allocation* _allocation;
    private bool _destroyed;

    internal VulkanBuffer(Buffer buffer, VulkanAllocator allocator, Allocation* allocation)
    {
        Buffer = buffer;
        _allocator = allocator;
        _allocation = allocation;
    }

    /// <summary>
    /// Maps the buffer memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <returns>A pointer to the mapped memory.</returns>
    public T* Map<T>() where T : unmanaged
    {
        return _allocator.Map<T>(_allocation);
    }

    /// <summary>
    /// Unmaps the buffer memory.
    /// </summary>
    public void Unmap()
    {
        _allocator.Unmap(_allocation);
    }

    /// <summary>
    /// Destroys the buffer and frees its GPU memory.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        _allocator.DestroyBuffer(Buffer, _allocation);
    }
}