using Silk.NET.Vulkan;
using VkImage = Silk.NET.Vulkan.Image;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan texture wrapper for a swapchain image used as a frame color output.
/// </summary>
internal sealed class VulkanSwapchainTexture : Texture, IVulkanTexture
{
    private ImageLayout _layout = ImageLayout.Undefined;

    /// <summary>
    /// Creates a Vulkan swapchain texture wrapper.
    /// </summary>
    /// <param name="image">The Vulkan swapchain image.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    internal VulkanSwapchainTexture(VkImage image, TextureFormat format, uint width, uint height)
    {
        Image = image;
        Format = format;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the Vulkan image handle.
    /// </summary>
    public VkImage Image { get; }

    /// <summary>
    /// Gets the texture dimensional shape.
    /// </summary>
    public override TextureDimension Dimension => TextureDimension.Type2D;

    /// <summary>
    /// Gets the texture pixel format.
    /// </summary>
    public override TextureFormat Format { get; }

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
    public override uint Depth => 1;

    /// <summary>
    /// Gets the number of mip levels.
    /// </summary>
    public override uint MipLevels => 1;

    /// <summary>
    /// Gets the number of array layers.
    /// </summary>
    public override uint ArrayLayers => 1;

    /// <summary>
    /// Gets the texture usage flags.
    /// </summary>
    public override TextureUsage Usage => TextureUsage.ColorAttachment;

    /// <summary>
    /// Gets the texture creation capability flags.
    /// </summary>
    public override TextureCreateFlags CreateFlags => TextureCreateFlags.None;

    /// <summary>
    /// Gets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <returns>The tracked image layout.</returns>
    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
        ThrowIfDestroyed();
        ValidateSubresource(mipLevel, arrayLayer);
        return _layout;
    }

    /// <summary>
    /// Sets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <param name="layout">The image layout.</param>
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
        MarkDestroyed();
    }

    /// <summary>
    /// Resets the tracked image layout to the initial layout.
    /// </summary>
    internal void ResetLayout()
    {
        ThrowIfDestroyed();
        _layout = ImageLayout.Undefined;
    }

    /// <summary>
    /// Throws because swapchain textures are owned by the presenter.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown when called.</exception>
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