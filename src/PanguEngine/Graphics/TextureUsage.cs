namespace PanguEngine.Graphics;

/// <summary>
/// Flags describing the usage of a texture.
/// </summary>
[Flags]
public enum TextureUsage
{
    /// <summary>
    /// No usage flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Texture can be used as a source for GPU transfer operations.
    /// </summary>
    TransferSource = 1,

    /// <summary>
    /// Texture can be used as a destination for GPU transfer operations.
    /// </summary>
    TransferDestination = 2,

    /// <summary>
    /// Texture can be read by shaders through a sampler.
    /// </summary>
    Sampled = 4,

    /// <summary>
    /// Texture can be used as a color rendering attachment.
    /// </summary>
    ColorAttachment = 8,

    /// <summary>
    /// Texture can be used as a depth/stencil rendering attachment.
    /// </summary>
    DepthStencilAttachment = 16
}