using Silk.NET.Vulkan;
using VkImage = Silk.NET.Vulkan.Image;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Provides Vulkan texture handles and layout tracking used by command recording.
/// </summary>
internal interface IVulkanTexture
{
    /// <summary>
    /// Gets the Vulkan image handle.
    /// </summary>
    VkImage Image { get; }

    /// <summary>
    /// Gets the texture dimension.
    /// </summary>
    TextureDimension Dimension { get; }

    /// <summary>
    /// Gets the texture format.
    /// </summary>
    TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture width.
    /// </summary>
    uint Width { get; }

    /// <summary>
    /// Gets the texture height.
    /// </summary>
    uint Height { get; }

    /// <summary>
    /// Gets the texture depth.
    /// </summary>
    uint Depth { get; }

    /// <summary>
    /// Gets the texture mip level count.
    /// </summary>
    uint MipLevels { get; }

    /// <summary>
    /// Gets the texture array layer count.
    /// </summary>
    uint ArrayLayers { get; }

    /// <summary>
    /// Gets the texture usage flags.
    /// </summary>
    TextureUsage Usage { get; }

    /// <summary>
    /// Gets the texture creation capability flags.
    /// </summary>
    TextureCreateFlags CreateFlags { get; }

    /// <summary>
    /// Gets whether the texture has been destroyed.
    /// </summary>
    bool IsDestroyed { get; }

    /// <summary>
    /// Gets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <returns>The tracked image layout.</returns>
    ImageLayout GetLayout(uint mipLevel, uint arrayLayer);

    /// <summary>
    /// Sets the tracked image layout for a texture subresource.
    /// </summary>
    /// <param name="mipLevel">The mip level.</param>
    /// <param name="arrayLayer">The array layer.</param>
    /// <param name="layout">The image layout.</param>
    void SetLayout(uint mipLevel, uint arrayLayer, ImageLayout layout);
}