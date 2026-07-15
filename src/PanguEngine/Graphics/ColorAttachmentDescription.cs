using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a color attachment used by a rendering operation.
/// </summary>
public readonly record struct ColorAttachmentDescription
{
    public ColorAttachmentDescription()
    {
    }

    /// <summary>
    /// Creates a color attachment description.
    /// </summary>
    /// <param name="attachment">The color attachment texture view.</param>
    /// <param name="clearColor">The clear color.</param>
    [SetsRequiredMembers]
    public ColorAttachmentDescription(TextureView attachment, ClearColor clearColor)
    {
        Attachment = attachment;
        ClearColor = clearColor;
    }

    /// <summary>
    /// The color attachment texture view.
    /// </summary>
    public required TextureView Attachment { get; init; }

    /// <summary>
    /// The clear color.
    /// </summary>
    public required ClearColor ClearColor { get; init; }

    /// <summary>
    /// The color attachment load operation.
    /// </summary>
    public LoadOperation LoadOperation { get; init; } = LoadOperation.Clear;

    /// <summary>
    /// The color attachment store operation.
    /// </summary>
    public StoreOperation StoreOperation { get; init; } = StoreOperation.Store;
}