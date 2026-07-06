namespace PanguEngine.Graphics;

/// <summary>
/// Describes the beginning of a rendering operation.
/// </summary>
/// <param name="ClearColor">The clear color.</param>
/// <param name="LoadOperation">The color attachment load operation.</param>
/// <param name="StoreOperation">The color attachment store operation.</param>
/// <param name="DepthStencilAttachment">The depth/stencil attachment, or null when rendering without one.</param>
/// <param name="DepthClearValue">The depth clear value.</param>
/// <param name="StencilClearValue">The stencil clear value.</param>
/// <param name="DepthLoadOperation">The depth attachment load operation.</param>
/// <param name="DepthStoreOperation">The depth attachment store operation.</param>
/// <param name="StencilLoadOperation">The stencil attachment load operation.</param>
/// <param name="StencilStoreOperation">The stencil attachment store operation.</param>
public readonly record struct RenderingDescription(
    ClearColor ClearColor,
    LoadOperation LoadOperation = LoadOperation.Clear,
    StoreOperation StoreOperation = StoreOperation.Store,
    Texture? DepthStencilAttachment = null,
    float DepthClearValue = 1,
    uint StencilClearValue = 0,
    LoadOperation DepthLoadOperation = LoadOperation.Clear,
    StoreOperation DepthStoreOperation = StoreOperation.Store,
    LoadOperation StencilLoadOperation = LoadOperation.Clear,
    StoreOperation StencilStoreOperation = StoreOperation.Store);