namespace PanguEngine.Graphics;

/// <summary>
/// Describes the beginning of a rendering operation.
/// </summary>
public readonly record struct RenderingDescription
{
    public RenderingDescription()
    {
    }

    /// <summary>
    /// The rendering width in pixels.
    /// </summary>
    public required uint Width { get; init; }

    /// <summary>
    /// The rendering height in pixels.
    /// </summary>
    public required uint Height { get; init; }

    /// <summary>
    /// The color attachments.
    /// </summary>
    public required ColorAttachmentDescription[] ColorAttachments { get; init; }

    /// <summary>
    /// The depth/stencil attachment, or null when rendering without one.
    /// </summary>
    public DepthStencilAttachmentDescription? DepthStencilAttachment { get; init; } = null;
}