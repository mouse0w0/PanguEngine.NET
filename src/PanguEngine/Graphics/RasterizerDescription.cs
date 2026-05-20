namespace PanguEngine.Graphics;

/// <summary>
/// Describes rasterization state for a graphics pipeline.
/// </summary>
/// <param name="CullMode">The triangle face cull mode.</param>
/// <param name="FrontFace">The front-facing triangle winding.</param>
/// <param name="LineWidth">The rasterized line width.</param>
public readonly record struct RasterizerDescription(
    CullMode CullMode = CullMode.Back,
    FrontFace FrontFace = FrontFace.Clockwise,
    float LineWidth = 1);