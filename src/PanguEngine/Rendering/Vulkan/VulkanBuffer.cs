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

    private readonly Allocation* _allocation;
    private bool _destroyed;

    internal VulkanBuffer(Buffer buffer, Allocation* allocation)
    {
        Buffer = buffer;
        _allocation = allocation;
    }

    /// <summary>
    /// Maps the buffer memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <returns>A pointer to the mapped memory.</returns>
    public T* Map<T>() where T : unmanaged
    {
        return VulkanAllocator.Map<T>(_allocation);
    }

    /// <summary>
    /// Unmaps the buffer memory.
    /// </summary>
    public void Unmap()
    {
        VulkanAllocator.Unmap(_allocation);
    }

    /// <summary>
    /// Destroys the buffer and frees its GPU memory.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        VulkanAllocator.DestroyBuffer(Buffer, _allocation);
    }
}