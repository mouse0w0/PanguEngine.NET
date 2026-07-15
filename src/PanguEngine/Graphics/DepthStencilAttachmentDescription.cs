namespace PanguEngine.Graphics;

/// <summary>
/// Describes a depth/stencil attachment used by a rendering operation.
/// </summary>
/// <param name="Attachment">The depth/stencil attachment texture view.</param>
/// <param name="DepthClearValue">The depth clear value.</param>
/// <param name="StencilClearValue">The stencil clear value.</param>
/// <param name="DepthLoadOperation">The depth attachment load operation.</param>
/// <param name="DepthStoreOperation">The depth attachment store operation.</param>
/// <param name="StencilLoadOperation">The stencil attachment load operation.</param>
/// <param name="StencilStoreOperation">The stencil attachment store operation.</param>
public readonly record struct DepthStencilAttachmentDescription(
    TextureView Attachment,
    float DepthClearValue = 1,
    uint StencilClearValue = 0,
    LoadOperation DepthLoadOperation = LoadOperation.Clear,
    StoreOperation DepthStoreOperation = StoreOperation.Store,
    LoadOperation StencilLoadOperation = LoadOperation.Clear,
    StoreOperation StencilStoreOperation = StoreOperation.Store);