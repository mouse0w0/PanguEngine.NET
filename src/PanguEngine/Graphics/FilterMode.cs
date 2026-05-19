namespace PanguEngine.Graphics;

/// <summary>
/// Texture filtering mode.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// Selects the nearest texel.
    /// </summary>
    Nearest,

    /// <summary>
    /// Linearly blends neighboring texels.
    /// </summary>
    Linear
}