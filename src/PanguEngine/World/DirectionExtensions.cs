namespace PanguEngine.World;

/// <summary>
/// Provides operations for directions.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Converts a direction to its corresponding flag.
    /// </summary>
    /// <param name="direction">The direction to convert.</param>
    /// <returns>The corresponding direction flag.</returns>
    public static DirectionFlags ToFlag(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => DirectionFlags.Down,
            Direction.Up => DirectionFlags.Up,
            Direction.North => DirectionFlags.North,
            Direction.South => DirectionFlags.South,
            Direction.West => DirectionFlags.West,
            Direction.East => DirectionFlags.East,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    /// <summary>
    /// Gets the opposite direction.
    /// </summary>
    /// <param name="direction">The direction to reverse.</param>
    /// <returns>The opposite direction.</returns>
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => Direction.Up,
            Direction.Up => Direction.Down,
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            Direction.East => Direction.West,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
}