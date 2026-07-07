using Silk.NET.Vulkan;
using Vma;
using VkImage = Silk.NET.Vulkan.Image;
using VkImageView = Silk.NET.Vulkan.ImageView;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Texture"/>.
/// </summary>
internal sealed unsafe class VulkanTexture : Texture, IVulkanTexture
{
    private readonly Allocation* _allocation;
    private readonly ImageLayout[] _subresourceLayouts;
    private bool _destroyed;

    /// <summary>
    /// Gets the Vulkan image handle.
    /// </summary>
    public VkImage Image { get; }

    /// <summary>
    /// Gets the Vulkan image view handle.
    /// </summary>
    public VkImageView ImageView { get; }

    /// <summary>
    /// Gets whether the texture has been destroyed.
    /// </summary>
    public override bool IsDestroyed => _destroyed;

    /// <summary>
    /// Gets the texture pixel format.
    /// </summary>
    public override TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture dimensional shape.
    /// </summary>
    public override TextureDimension Dimension { get; }

    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    public override uint Width { get; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    public override uint Height { get; }

    /// <summary>
    /// Gets the texture depth in pixels.
    /// </summary>
    public override uint Depth { get; }

    /// <summary>
    /// Gets the number of mip levels.
    /// </summary>
    public override uint MipLevels { get; }

    /// <summary>
    /// Gets the number of array layers.
    /// </summary>
    public override uint ArrayLayers { get; }

    /// <summary>
    /// Gets the texture usage flags.
    /// </summary>
    public override TextureUsage Usage { get; }

    internal VulkanTexture(VkImage image, Allocation* allocation, VkImageView imageView, TextureDimension dimension,
        TextureFormat format, uint width, uint height, uint depth, uint mipLevels, uint arrayLayers, TextureUsage usage)
    {
        Image = image;
        _allocation = allocation;
        ImageView = imageView;
        Dimension = dimension;
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        MipLevels = mipLevels;
        ArrayLayers = arrayLayers;
        Usage = usage;

        var trackedSubresourceCount = dimension == TextureDimension.Type3D
            ? mipLevels
            : checked(mipLevels * arrayLayers);
        _subresourceLayouts = new ImageLayout[trackedSubresourceCount];
        Array.Fill(_subresourceLayouts, ImageLayout.Undefined);
    }

    /// <summary>
    /// Gets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <returns>The tracked image layout.</returns>
    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        return _subresourceLayouts[GetLayoutIndex(mipLevel, arrayLayer)];
    }

    /// <summary>
    /// Sets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <param name="layout">The image layout.</param>
    public void SetLayout(uint mipLevel, uint arrayLayer, ImageLayout layout)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        _subresourceLayouts[GetLayoutIndex(mipLevel, arrayLayer)] = layout;
    }

    /// <summary>
    /// Calculates an extent at the specified mip level.
    /// </summary>
    /// <param name="extent">The base mip extent.</param>
    /// <param name="mipLevel">The mip level.</param>
    /// <returns>The mip extent, clamped to at least one.</returns>
    internal static uint GetMipExtent(uint extent, uint mipLevel)
    {
        return Math.Max(1u, extent >> (int)mipLevel);
    }

    private int GetLayoutIndex(uint mipLevel, uint arrayLayer)
    {
        if (mipLevel >= MipLevels)
            throw new ArgumentOutOfRangeException(nameof(mipLevel), "Texture mip level is out of range.");
        if (Dimension == TextureDimension.Type3D)
        {
            if (arrayLayer != 0)
                throw new ArgumentOutOfRangeException(nameof(arrayLayer), "3D textures do not have array layers.");
            return checked((int)mipLevel);
        }

        if (arrayLayer >= ArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(arrayLayer), "Texture array layer is out of range.");
        return checked((int)(mipLevel * ArrayLayers + arrayLayer));
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        var image = Image;
        var imageView = ImageView;
        var allocation = _allocation;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue, () =>
        {
            VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null);
            VulkanAllocator.DestroyImage(image, allocation);
        });
    }
}