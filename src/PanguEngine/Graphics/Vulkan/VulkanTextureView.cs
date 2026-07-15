using VkImageView = Silk.NET.Vulkan.ImageView;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="TextureView"/>.
/// </summary>
internal sealed unsafe class VulkanTextureView : TextureView, IVulkanTextureView
{
    private bool _destroyed;

    internal VulkanTextureView(
        VulkanTexture texture,
        VkImageView imageView,
        in TextureViewDescription description,
        uint width,
        uint height,
        uint depth)
    {
        VulkanTexture = texture;
        ImageView = imageView;
        Dimension = description.Dimension;
        Width = width;
        Height = height;
        Depth = depth;
        BaseMipLevel = description.BaseMipLevel;
        MipLevels = description.MipLevels;
        BaseArrayLayer = description.BaseArrayLayer;
        ArrayLayers = description.ArrayLayers;
        texture.RegisterView();
    }

    /// <summary>
    /// Gets the Vulkan texture referenced by the view.
    /// </summary>
    internal VulkanTexture VulkanTexture { get; }

    /// <inheritdoc cref="TextureView.Texture"/>
    public override Texture Texture => VulkanTexture;

    /// <inheritdoc cref="IVulkanTextureView.Texture"/>
    IVulkanTexture IVulkanTextureView.Texture => VulkanTexture;

    /// <inheritdoc cref="IVulkanTextureView.ImageView"/>
    public VkImageView ImageView { get; }

    /// <inheritdoc cref="TextureView.IsDestroyed"/>
    public override bool IsDestroyed => _destroyed;

    /// <inheritdoc cref="TextureView.Dimension"/>
    public override TextureViewDimension Dimension { get; }

    /// <inheritdoc cref="TextureView.Format"/>
    public override TextureFormat Format => VulkanTexture.Format;

    /// <inheritdoc cref="TextureView.Width"/>
    public override uint Width { get; }

    /// <inheritdoc cref="TextureView.Height"/>
    public override uint Height { get; }

    /// <inheritdoc cref="TextureView.Depth"/>
    public override uint Depth { get; }

    /// <inheritdoc cref="TextureView.BaseMipLevel"/>
    public override uint BaseMipLevel { get; }

    /// <inheritdoc cref="TextureView.MipLevels"/>
    public override uint MipLevels { get; }

    /// <inheritdoc cref="TextureView.BaseArrayLayer"/>
    public override uint BaseArrayLayer { get; }

    /// <inheritdoc cref="TextureView.ArrayLayers"/>
    public override uint ArrayLayers { get; }

    /// <inheritdoc cref="GraphicsResource.Destroy"/>
    public override void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanTexture.ReleaseView(retireValue);
        var imageView = ImageView;
        VulkanDeletionQueue.Enqueue(retireValue,
            () => VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null));
    }
}