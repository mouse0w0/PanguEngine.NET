namespace PanguEngine.Graphics;

/// <summary>
/// Describes the beginning of a rendering operation.
/// </summary>
/// <param name="Width">The rendering width in pixels.</param>
/// <param name="Height">The rendering height in pixels.</param>
/// <param name="ColorAttachments">The color attachments.</param>
/// <param name="DepthStencilAttachment">The depth/stencil attachment, or null when rendering without one.</param>
public readonly record struct RenderingDescription(
    uint Width,
    uint Height,
    ColorAttachmentDescription[] ColorAttachments,
    DepthStencilAttachmentDescription? DepthStencilAttachment = null);