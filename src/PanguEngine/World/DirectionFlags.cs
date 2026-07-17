namespace PanguEngine.World;

/// <summary>
/// Identifies directions as combinable flags.
/// </summary>
[Flags]
public enum DirectionFlags
{
    /// <summary>No directions.</summary>
    None = 0,

    /// <summary>The negative Y direction.</summary>
    Down = 1 << 0,

    /// <summary>The positive Y direction.</summary>
    Up = 1 << 1,

    /// <summary>The negative Z direction.</summary>
    North = 1 << 2,

    /// <summary>The positive Z direction.</summary>
    South = 1 << 3,

    /// <summary>The negative X direction.</summary>
    West = 1 << 4,

    /// <summary>The positive X direction.</summary>
    East = 1 << 5
}