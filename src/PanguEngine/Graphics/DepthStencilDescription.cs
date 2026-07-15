using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes depth and stencil state for a graphics pipeline.
/// </summary>
public readonly record struct DepthStencilDescription
{
    /// <summary>
    /// Creates a depth and stencil state description.
    /// </summary>
    /// <param name="depthTestEnabled">Whether depth testing is enabled.</param>
    /// <param name="depthWriteEnabled">Whether passing fragments write depth values.</param>
    /// <param name="depthCompareOperation">The depth comparison operation.</param>
    /// <param name="stencilTestEnabled">Whether stencil testing is enabled.</param>
    /// <param name="frontFace">The stencil state used for front-facing triangles.</param>
    /// <param name="backFace">The stencil state used for back-facing triangles.</param>
    [SetsRequiredMembers]
    public DepthStencilDescription(
        bool depthTestEnabled,
        bool depthWriteEnabled,
        CompareOperation depthCompareOperation,
        bool stencilTestEnabled,
        StencilFaceDescription frontFace,
        StencilFaceDescription backFace)
    {
        DepthTestEnabled = depthTestEnabled;
        DepthWriteEnabled = depthWriteEnabled;
        DepthCompareOperation = depthCompareOperation;
        StencilTestEnabled = stencilTestEnabled;
        FrontFace = frontFace;
        BackFace = backFace;
    }

    /// <summary>
    /// Whether depth testing is enabled.
    /// </summary>
    public required bool DepthTestEnabled { get; init; }

    /// <summary>
    /// Whether passing fragments write depth values.
    /// </summary>
    public required bool DepthWriteEnabled { get; init; }

    /// <summary>
    /// The depth comparison operation.
    /// </summary>
    public required CompareOperation DepthCompareOperation { get; init; }

    /// <summary>
    /// Whether stencil testing is enabled.
    /// </summary>
    public required bool StencilTestEnabled { get; init; }

    /// <summary>
    /// The stencil state used for front-facing triangles.
    /// </summary>
    public required StencilFaceDescription FrontFace { get; init; }

    /// <summary>
    /// The stencil state used for back-facing triangles.
    /// </summary>
    public required StencilFaceDescription BackFace { get; init; }
}