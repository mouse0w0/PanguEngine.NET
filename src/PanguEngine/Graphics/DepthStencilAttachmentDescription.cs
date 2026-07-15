using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a depth/stencil attachment used by a rendering operation.
/// </summary>
public readonly record struct DepthStencilAttachmentDescription
{
    public DepthStencilAttachmentDescription()
    {
    }

    /// <summary>
    /// Creates a depth/stencil attachment description.
    /// </summary>
    /// <param name="attachment">The depth/stencil attachment texture view.</param>
    [SetsRequiredMembers]
    public DepthStencilAttachmentDescription(TextureView attachment)
    {
        Attachment = attachment;
    }

    /// <summary>
    /// The depth/stencil attachment texture view.
    /// </summary>
    public required TextureView Attachment { get; init; }

    /// <summary>
    /// The depth clear value.
    /// </summary>
    public float DepthClearValue { get; init; } = 1;

    /// <summary>
    /// The stencil clear value.
    /// </summary>
    public uint StencilClearValue { get; init; } = 0;

    /// <summary>
    /// The depth attachment load operation.
    /// </summary>
    public LoadOperation DepthLoadOperation { get; init; } = LoadOperation.Clear;

    /// <summary>
    /// The depth attachment store operation.
    /// </summary>
    public StoreOperation DepthStoreOperation { get; init; } = StoreOperation.Store;

    /// <summary>
    /// The stencil attachment load operation.
    /// </summary>
    public LoadOperation StencilLoadOperation { get; init; } = LoadOperation.Clear;

    /// <summary>
    /// The stencil attachment store operation.
    /// </summary>
    public StoreOperation StencilStoreOperation { get; init; } = StoreOperation.Store;
}