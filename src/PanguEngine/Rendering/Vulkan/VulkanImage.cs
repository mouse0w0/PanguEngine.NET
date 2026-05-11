using Silk.NET.Vulkan;
using Vma;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// A Vulkan image with bound GPU memory.
/// </summary>
public sealed unsafe class VulkanImage
{
    /// <summary>
    /// The Vulkan image handle.
    /// </summary>
    public Image Image { get; }

    private readonly VulkanAllocator _allocator;
    private readonly Allocation* _allocation;
    private bool _destroyed;

    internal VulkanImage(Image image, VulkanAllocator allocator, Allocation* allocation)
    {
        Image = image;
        _allocator = allocator;
        _allocation = allocation;
    }

    /// <summary>
    /// Destroys the image and frees its GPU memory.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        _allocator.DestroyImage(Image, _allocation);
    }
}