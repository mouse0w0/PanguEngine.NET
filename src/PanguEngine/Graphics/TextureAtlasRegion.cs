namespace PanguEngine.Graphics;

/// <summary>
/// Describes a source image region within a texture atlas.
/// </summary>
/// <param name="X">The left pixel coordinate, excluding gutter pixels.</param>
/// <param name="Y">The top pixel coordinate, excluding gutter pixels.</param>
/// <param name="Width">The source image width in pixels.</param>
/// <param name="Height">The source image height in pixels.</param>
/// <param name="U0">The normalized left texture coordinate, including any sampling inset.</param>
/// <param name="V0">The normalized top texture coordinate, including any sampling inset.</param>
/// <param name="U1">The normalized right texture coordinate, including any sampling inset.</param>
/// <param name="V1">The normalized bottom texture coordinate, including any sampling inset.</param>
public readonly record struct TextureAtlasRegion(
    int X,
    int Y,
    int Width,
    int Height,
    float U0,
    float V0,
    float U1,
    float V1);