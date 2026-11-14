using PanguEngine.World;

namespace PanguEngine.Tests.World;

public sealed class DirectionExtensionsTests
{
    [Fact]
    public void CollectionsReturnExpectedDirectionsInEnumOrder()
    {
        Assert.Equal(
            new[] { Direction.Down, Direction.Up, Direction.North, Direction.South, Direction.West, Direction.East },
            Direction.Values.ToArray());
        Assert.Equal(
            new[] { Direction.North, Direction.South, Direction.West, Direction.East },
            Direction.Horizontals.ToArray());
        Assert.Equal(
            new[] { Direction.Down, Direction.Up },
            Direction.Verticals.ToArray());
    }

    [Theory]
    [InlineData(Direction.Down, Direction.Up)]
    [InlineData(Direction.Up, Direction.Down)]
    [InlineData(Direction.North, Direction.South)]
    [InlineData(Direction.South, Direction.North)]
    [InlineData(Direction.West, Direction.East)]
    [InlineData(Direction.East, Direction.West)]
    public void OppositeReturnsReverseDirection(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Opposite());
    }

    [Fact]
    public void OppositeRejectsUnknownDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>("direction", () => ((Direction)int.MaxValue).Opposite());
    }

    [Theory]
    [InlineData(Direction.Down, DirectionFlags.Down)]
    [InlineData(Direction.Up, DirectionFlags.Up)]
    [InlineData(Direction.North, DirectionFlags.North)]
    [InlineData(Direction.South, DirectionFlags.South)]
    [InlineData(Direction.West, DirectionFlags.West)]
    [InlineData(Direction.East, DirectionFlags.East)]
    public void ToFlagReturnsMatchingFlag(Direction direction, DirectionFlags expected)
    {
        Assert.Equal(expected, direction.ToFlag());
    }

    [Fact]
    public void ToFlagRejectsUnknownDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>("direction", () => ((Direction)int.MaxValue).ToFlag());
    }
}
