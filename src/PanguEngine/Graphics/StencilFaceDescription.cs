namespace PanguEngine.Graphics;

/// <summary>
/// Describes stencil state for one triangle face orientation.
/// </summary>
public readonly record struct StencilFaceDescription
{
    public StencilFaceDescription()
    {
    }

    /// <summary>
    /// The stencil comparison operation.
    /// </summary>
    public CompareOperation CompareOperation { get; init; } = CompareOperation.Always;

    /// <summary>
    /// The operation used when the stencil test fails.
    /// </summary>
    public StencilOperation StencilFailOperation { get; init; } = StencilOperation.Keep;

    /// <summary>
    /// The operation used when the stencil test passes and the depth test fails.
    /// </summary>
    public StencilOperation DepthFailOperation { get; init; } = StencilOperation.Keep;

    /// <summary>
    /// The operation used when both stencil and depth tests pass.
    /// </summary>
    public StencilOperation PassOperation { get; init; } = StencilOperation.Keep;

    /// <summary>
    /// The mask applied to stencil values before comparison.
    /// </summary>
    public uint CompareMask { get; init; } = 0xff;

    /// <summary>
    /// The mask applied when writing stencil values.
    /// </summary>
    public uint WriteMask { get; init; } = 0xff;

    /// <summary>
    /// The stencil reference value.
    /// </summary>
    public uint Reference { get; init; } = 0;
}