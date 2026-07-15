using VkImageView = Silk.NET.Vulkan.ImageView;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Provides a Vulkan image view and its texture subresource range.
/// </summary>
internal interface IVulkanTextureView
{
    /// <summary>
    /// Gets the Vulkan image view handle.
    /// </summary>
    VkImageView ImageView { get; }

    /// <summary>
    /// Gets the Vulkan texture referenced by the view.
    /// </summary>
    IVulkanTexture Texture { get; }

    /// <summary>
    /// Gets the texture view dimensional shape.
    /// </summary>
    TextureViewDimension Dimension { get; }

    /// <summary>
    /// Gets the texture view format.
    /// </summary>
    TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture view width.
    /// </summary>
    uint Width { get; }

    /// <summary>
    /// Gets the texture view height.
    /// </summary>
    uint Height { get; }

    /// <summary>
    /// Gets the texture view depth.
    /// </summary>
    uint Depth { get; }

    /// <summary>
    /// Gets the first mip level included in the view.
    /// </summary>
    uint BaseMipLevel { get; }

    /// <summary>
    /// Gets the number of mip levels included in the view.
    /// </summary>
    uint MipLevels { get; }

    /// <summary>
    /// Gets the first array layer included in the view.
    /// </summary>
    uint BaseArrayLayer { get; }

    /// <summary>
    /// Gets the number of array layers included in the view.
    /// </summary>
    uint ArrayLayers { get; }

    /// <summary>
    /// Gets whether the texture view has been destroyed.
    /// </summary>
    bool IsDestroyed { get; }
}