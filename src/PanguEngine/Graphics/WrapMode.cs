namespace PanguEngine.Graphics;

/// <summary>
/// Texture coordinate wrapping mode.
/// </summary>
public enum WrapMode
{
    /// <summary>
    /// Repeats texture coordinates outside the normalized range.
    /// </summary>
    Repeat,

    /// <summary>
    /// Repeats texture coordinates with mirrored orientation on alternating intervals.
    /// </summary>
    MirroredRepeat,

    /// <summary>
    /// Clamps texture coordinates to the nearest edge.
    /// </summary>
    ClampToEdge,

    /// <summary>
    /// Uses the configured border color outside the normalized range.
    /// </summary>
    ClampToBorder
}