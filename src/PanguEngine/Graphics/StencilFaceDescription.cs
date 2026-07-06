namespace PanguEngine.Graphics;

/// <summary>
/// Describes stencil state for one triangle face orientation.
/// </summary>
/// <param name="CompareOperation">The stencil comparison operation.</param>
/// <param name="StencilFailOperation">The operation used when the stencil test fails.</param>
/// <param name="DepthFailOperation">The operation used when the stencil test passes and the depth test fails.</param>
/// <param name="PassOperation">The operation used when both stencil and depth tests pass.</param>
/// <param name="CompareMask">The mask applied to stencil values before comparison.</param>
/// <param name="WriteMask">The mask applied when writing stencil values.</param>
/// <param name="Reference">The stencil reference value.</param>
public readonly record struct StencilFaceDescription(
    CompareOperation CompareOperation,
    StencilOperation StencilFailOperation = StencilOperation.Keep,
    StencilOperation DepthFailOperation = StencilOperation.Keep,
    StencilOperation PassOperation = StencilOperation.Keep,
    uint CompareMask = 0xff,
    uint WriteMask = 0xff,
    uint Reference = 0);