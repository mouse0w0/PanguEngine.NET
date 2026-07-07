namespace PanguEngine.Graphics;

/// <summary>
/// Describes the beginning of a rendering operation.
/// </summary>
/// <param name="ColorAttachments">The color attachments.</param>
/// <param name="DepthStencilAttachment">The depth/stencil attachment, or null when rendering without one.</param>
public readonly record struct RenderingDescription(
    ReadOnlyMemory<ColorAttachmentDescription> ColorAttachments,
    DepthStencilAttachmentDescription? DepthStencilAttachment = null);