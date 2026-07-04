namespace PanguEngine.World.Blocks;

/// <summary>
/// Identifies one axis-aligned direction in block space.
/// </summary>
public enum Direction
{
    /// <summary>The negative Y direction.</summary>
    Down,

    /// <summary>The positive Y direction.</summary>
    Up,

    /// <summary>The negative Z direction.</summary>
    North,

    /// <summary>The positive Z direction.</summary>
    South,

    /// <summary>The negative X direction.</summary>
    West,

    /// <summary>The positive X direction.</summary>
    East
}