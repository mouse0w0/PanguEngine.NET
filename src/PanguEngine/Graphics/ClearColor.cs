namespace PanguEngine.Graphics;

/// <summary>
/// Represents a floating-point color used for clearing a render target.
/// </summary>
/// <param name="R">The red channel.</param>
/// <param name="G">The green channel.</param>
/// <param name="B">The blue channel.</param>
/// <param name="A">The alpha channel.</param>
public readonly record struct ClearColor(float R, float G, float B, float A);