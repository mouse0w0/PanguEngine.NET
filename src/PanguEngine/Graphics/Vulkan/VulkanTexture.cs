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

    /// <inheritdoc/>
    public VkImage Image { get; }

    /// <inheritdoc/>
    public VkImageView ImageView { get; }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <inheritdoc/>
    public override TextureFormat Format { get; }

    /// <inheritdoc/>
    public override TextureDimension Dimension { get; }

    /// <inheritdoc/>
    public override uint Width { get; }

    /// <inheritdoc/>
    public override uint Height { get; }

    /// <inheritdoc/>
    public override uint Depth { get; }

    /// <inheritdoc/>
    public override uint MipLevels { get; }

    /// <inheritdoc/>
    public override uint ArrayLayers { get; }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        return _subresourceLayouts[GetLayoutIndex(mipLevel, arrayLayer)];
    }

    /// <inheritdoc/>
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