namespace PanguEngine.Graphics;

/// <summary>
/// Describes rasterization state for a graphics pipeline.
/// </summary>
public readonly record struct RasterizerDescription
{
    public RasterizerDescription()
    {
    }

    /// <summary>
    /// The triangle face cull mode.
    /// </summary>
    public CullMode CullMode { get; init; } = CullMode.Back;

    /// <summary>
    /// The front-facing triangle winding.
    /// </summary>
    public FrontFace FrontFace { get; init; } = FrontFace.Clockwise;

    /// <summary>
    /// The rasterized line width.
    /// </summary>
    public float LineWidth { get; init; } = 1;
}