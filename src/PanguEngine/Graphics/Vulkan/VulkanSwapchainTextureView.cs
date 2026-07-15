using VkImageView = Silk.NET.Vulkan.ImageView;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan texture view wrapper for a presenter-owned swapchain image view.
/// </summary>
internal sealed class VulkanSwapchainTextureView : TextureView, IVulkanTextureView
{
    internal VulkanSwapchainTextureView(VulkanSwapchainTexture texture, VkImageView imageView)
    {
        VulkanTexture = texture;
        ImageView = imageView;
    }

    /// <summary>
    /// Gets the Vulkan swapchain texture referenced by the view.
    /// </summary>
    internal VulkanSwapchainTexture VulkanTexture { get; }

    /// <inheritdoc cref="TextureView.Texture"/>
    public override Texture Texture => VulkanTexture;

    /// <inheritdoc cref="IVulkanTextureView.Texture"/>
    IVulkanTexture IVulkanTextureView.Texture => VulkanTexture;

    /// <inheritdoc cref="IVulkanTextureView.ImageView"/>
    public VkImageView ImageView { get; }

    /// <inheritdoc cref="TextureView.Dimension"/>
    public override TextureViewDimension Dimension => TextureViewDimension.Type2D;

    /// <inheritdoc cref="TextureView.Format"/>
    public override TextureFormat Format => VulkanTexture.Format;

    /// <inheritdoc cref="TextureView.Width"/>
    public override uint Width => VulkanTexture.Width;

    /// <inheritdoc cref="TextureView.Height"/>
    public override uint Height => VulkanTexture.Height;

    /// <inheritdoc cref="TextureView.Depth"/>
    public override uint Depth => 1;

    /// <inheritdoc cref="TextureView.BaseMipLevel"/>
    public override uint BaseMipLevel => 0;

    /// <inheritdoc cref="TextureView.MipLevels"/>
    public override uint MipLevels => 1;

    /// <inheritdoc cref="TextureView.BaseArrayLayer"/>
    public override uint BaseArrayLayer => 0;

    /// <inheritdoc cref="TextureView.ArrayLayers"/>
    public override uint ArrayLayers => 1;

    /// <summary>
    /// Invalidates the view after the owning swapchain image view is no longer usable.
    /// </summary>
    internal void Invalidate()
    {
        MarkDestroyed();
    }

    /// <summary>
    /// Throws because swapchain texture views are owned by the presenter.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown when called.</exception>
    public override void Destroy()
    {
        throw new InvalidOperationException("Frame color output texture views are owned by the presenter.");
    }
}