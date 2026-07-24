namespace PanguEngine.World.Blocks;

/// <summary>
/// Provides commonly-used block properties that can be shared across multiple block types.
/// </summary>
public static class BuiltinBlockProperties
{
    /// <summary>
    /// The direction a block is facing along all six axis-aligned directions.
    /// Allowed values: Down, Up, North, South, West, East.
    /// </summary>
    public static readonly BlockProperty<Direction> Facing = BlockProperty.CreateEnum(
        "facing",
        Direction.Down,
        Direction.Up,
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East
    );

    /// <summary>
    /// The direction a block is facing along the four horizontal directions.
    /// Allowed values: North, South, West, East.
    /// </summary>
    public static readonly BlockProperty<Direction> HorizontalFacing = BlockProperty.CreateEnum(
        "horizontal_facing",
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East
    );
}