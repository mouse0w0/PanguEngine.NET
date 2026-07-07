using Silk.NET.Vulkan;
using VkImage = Silk.NET.Vulkan.Image;
using VkImageView = Silk.NET.Vulkan.ImageView;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan texture wrapper for a swapchain image used as a frame color output.
/// </summary>
internal sealed class VulkanSwapchainTexture : Texture, IVulkanTexture
{
    private ImageLayout _layout = ImageLayout.Undefined;
    private bool _destroyed;

    /// <summary>
    /// Creates a Vulkan swapchain texture wrapper.
    /// </summary>
    /// <param name="image">The Vulkan swapchain image.</param>
    /// <param name="imageView">The Vulkan image view.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    internal VulkanSwapchainTexture(VkImage image, VkImageView imageView, TextureFormat format, uint width, uint height)
    {
        Image = image;
        ImageView = imageView;
        Format = format;
        Width = width;
        Height = height;
    }

    /// <inheritdoc/>
    public VkImage Image { get; }

    /// <inheritdoc/>
    public VkImageView ImageView { get; }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <inheritdoc/>
    public override TextureDimension Dimension => TextureDimension.Type2D;

    /// <inheritdoc/>
    public override TextureFormat Format { get; }

    /// <inheritdoc/>
    public override uint Width { get; }

    /// <inheritdoc/>
    public override uint Height { get; }

    /// <inheritdoc/>
    public override uint Depth => 1;

    /// <inheritdoc/>
    public override uint MipLevels => 1;

    /// <inheritdoc/>
    public override uint ArrayLayers => 1;

    /// <inheritdoc/>
    public override TextureUsage Usage => TextureUsage.ColorAttachment;

    /// <inheritdoc/>
    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
        ThrowIfDestroyed();
        ValidateSubresource(mipLevel, arrayLayer);
        return _layout;
    }

    /// <inheritdoc/>
    public void SetLayout(uint mipLevel, uint arrayLayer, ImageLayout layout)
    {
        ThrowIfDestroyed();
        ValidateSubresource(mipLevel, arrayLayer);
        _layout = layout;
    }

    /// <summary>
    /// Invalidates the wrapper after the owning swapchain image is no longer usable.
    /// </summary>
    internal void Invalidate()
    {
        _destroyed = true;
    }

    /// <summary>
    /// Resets the tracked image layout to the initial layout.
    /// </summary>
    internal void ResetLayout()
    {
        ThrowIfDestroyed();
        _layout = ImageLayout.Undefined;
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        throw new InvalidOperationException("Frame color output textures are owned by the presenter.");
    }

    /// <summary>
    /// Validates a swapchain texture subresource address.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    private static void ValidateSubresource(uint mipLevel, uint arrayLayer)
    {
        if (mipLevel != 0)
            throw new ArgumentOutOfRangeException(nameof(mipLevel),
                "Swapchain textures have exactly one mip level.");
        if (arrayLayer != 0)
            throw new ArgumentOutOfRangeException(nameof(arrayLayer),
                "Swapchain textures have exactly one array layer.");
    }
}