namespace PanguEngine.Graphics;

/// <summary>
/// Describes depth and stencil state for a graphics pipeline.
/// </summary>
/// <param name="DepthTestEnabled">Whether depth testing is enabled.</param>
/// <param name="DepthWriteEnabled">Whether passing fragments write depth values.</param>
/// <param name="DepthCompareOperation">The depth comparison operation.</param>
/// <param name="StencilTestEnabled">Whether stencil testing is enabled.</param>
/// <param name="FrontFace">The stencil state used for front-facing triangles.</param>
/// <param name="BackFace">The stencil state used for back-facing triangles.</param>
public readonly record struct DepthStencilDescription(
    bool DepthTestEnabled,
    bool DepthWriteEnabled,
    CompareOperation DepthCompareOperation,
    bool StencilTestEnabled,
    StencilFaceDescription FrontFace,
    StencilFaceDescription BackFace);