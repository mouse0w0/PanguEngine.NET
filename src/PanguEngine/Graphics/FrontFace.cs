namespace PanguEngine.Graphics;

/// <summary>
/// Identifies vertex winding used to determine front-facing triangles.
/// </summary>
public enum FrontFace
{
    /// <summary>
    /// Clockwise vertex winding is front-facing.
    /// </summary>
    Clockwise,

    /// <summary>
    /// Counter-clockwise vertex winding is front-facing.
    /// </summary>
    CounterClockwise
}