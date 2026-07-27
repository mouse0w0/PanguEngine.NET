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
    private readonly ImageLayout[] _subresourceLayouts;
    private uint _activeViewCount;

    /// <summary>
    /// Gets the Vulkan image handle.
    /// </summary>
    public VkImage Image { get; }

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

    /// <summary>
    /// Gets the texture creation capability flags.
    /// </summary>
    public override TextureCreateFlags CreateFlags { get; }

    internal VulkanTexture(VkImage image, Allocation* allocation, TextureDimension dimension,
        TextureFormat format, uint width, uint height, uint depth, uint mipLevels, uint arrayLayers, TextureUsage usage,
        TextureCreateFlags createFlags)
    {
        Image = image;
        Dimension = dimension;
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        MipLevels = mipLevels;
        ArrayLayers = arrayLayers;
        Usage = usage;
        CreateFlags = createFlags;

        var trackedSubresourceCount = dimension == TextureDimension.Type3D
            ? mipLevels
            : checked(mipLevels * arrayLayers);
        _subresourceLayouts = new ImageLayout[trackedSubresourceCount];
        Array.Fill(_subresourceLayouts, ImageLayout.Undefined);
        Lifetime = new VulkanResourceLifetime(
            this,
            () => VulkanAllocator.DestroyImage(image, allocation),
            VulkanDeletionQueue.Enqueue);
    }

    internal VulkanResourceLifetime Lifetime { get; }

    /// <summary>
    /// Gets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <returns>The tracked image layout.</returns>
    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
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

    internal VkImageView CreateImageView(in TextureViewDescription description)
    {
        ThrowIfDestroyed();
        if (!Lifetime.TryAcquireHold())
            throw new ObjectDisposedException(nameof(VulkanTexture));

        try
        {
            ImageViewCreateInfo viewInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Image,
                ViewType = VulkanMapping.ToVulkanImageViewType(description.Dimension),
                Format = VulkanMapping.ToVulkanFormat(Format),
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = VulkanMapping.ToVulkanImageAspect(Format),
                    BaseMipLevel = description.BaseMipLevel,
                    LevelCount = description.MipLevels,
                    BaseArrayLayer = description.BaseArrayLayer,
                    LayerCount = description.ArrayLayers
                }
            };

            if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in viewInfo, null, out var imageView) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create texture image view.");

            _activeViewCount++;
            return imageView;
        }
        catch
        {
            Lifetime.ReleaseHold();
            throw;
        }
    }

    internal void DestroyUnpublishedImageView(VkImageView imageView)
    {
        VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null);
        ReleaseView();
        ReleaseNativeViewHold();
    }

    /// <summary>
    /// Unregisters a live view created from this texture.
    /// </summary>
    internal void ReleaseView()
    {
        if (_activeViewCount == 0)
            throw new InvalidOperationException("Texture view count cannot be negative.");
        _activeViewCount--;
    }

    internal void ReleaseNativeViewHold()
    {
        Lifetime.ReleaseHold();
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
        VulkanContext.EnsureRenderThread();
        if (IsDestroyed)
            return;
        if (_activeViewCount != 0)
            throw new InvalidOperationException("Texture cannot be destroyed while texture views are still alive.");

        MarkDestroyed();
        Lifetime.RequestDestroy();
    }
}