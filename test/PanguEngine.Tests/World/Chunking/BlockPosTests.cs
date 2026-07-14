using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Tests.World.Chunking;

public sealed class BlockPosTests
{
    [Fact]
    public void EqualComponentsAreEqual()
    {
        var first = new BlockPos(1, 2, 3);
        var second = new BlockPos(1, 2, 3);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(15, 15, 15, 0, 0, 0)]
    [InlineData(16, 0, 0, 1, 0, 0)]
    [InlineData(-1, 0, 0, -1, 0, 0)]
    [InlineData(-16, 0, 0, -1, 0, 0)]
    [InlineData(-17, 0, 0, -2, 0, 0)]
    [InlineData(0, -17, 17, 0, -2, 1)]
    public void ToChunkPosUsesFloorDivision(
        int x,
        int y,
        int z,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        var blockPos = new BlockPos(x, y, z);

        Assert.Equal(new ChunkPos(expectedX, expectedY, expectedZ), blockPos.ToChunkPos());
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(15, 15, 15, 15, 15, 15)]
    [InlineData(16, -1, 32, 0, 15, 0)]
    [InlineData(-17, 17, -16, 15, 1, 0)]
    public void ToChunkLocalPosUsesLocalMask(
        int x,
        int y,
        int z,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        var blockPos = new BlockPos(x, y, z);

        Assert.Equal(new BlockPos(expectedX, expectedY, expectedZ), blockPos.ToChunkLocalPos());
    }

    [Fact]
    public void ToVector3DReturnsMatchingComponents()
    {
        var blockPos = new BlockPos(1, -2, 3);

        Assert.Equal(new Vector3D<int>(1, -2, 3), blockPos.ToVector3D());
    }

    [Fact]
    public void FromVector3DReturnsMatchingComponents()
    {
        var vector = new Vector3D<int>(1, -2, 3);

        Assert.Equal(new BlockPos(1, -2, 3), BlockPos.FromVector3D(vector));
    }

    [Fact]
    public void OffsetAddsComponents()
    {
        var blockPos = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(5, 7, 9), blockPos.Offset(4, 5, 6));
        Assert.Equal(new BlockPos(5, 7, 9), blockPos.Offset(new BlockPos(4, 5, 6)));
    }

    [Fact]
    public void OffsetDirectionUsesDefaultDistance()
    {
        var blockPos = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(1, 3, 3), blockPos.Offset(Direction.Up));
    }

    [Theory]
    [InlineData(Direction.Down, 2, 1, 0, 3)]
    [InlineData(Direction.Up, 2, 1, 4, 3)]
    [InlineData(Direction.North, 2, 1, 2, 1)]
    [InlineData(Direction.South, 2, 1, 2, 5)]
    [InlineData(Direction.West, 2, -1, 2, 3)]
    [InlineData(Direction.East, 2, 3, 2, 3)]
    [InlineData(Direction.East, -2, -1, 2, 3)]
    public void OffsetDirectionMovesByDistance(
        Direction direction,
        int distance,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        var blockPos = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(expectedX, expectedY, expectedZ), blockPos.Offset(direction, distance));
    }

    [Fact]
    public void DirectionMethodsUseDefaultDistance()
    {
        var blockPos = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(1, 3, 3), blockPos.Up());
        Assert.Equal(new BlockPos(1, 1, 3), blockPos.Down());
        Assert.Equal(new BlockPos(1, 2, 2), blockPos.North());
        Assert.Equal(new BlockPos(1, 2, 4), blockPos.South());
        Assert.Equal(new BlockPos(0, 2, 3), blockPos.West());
        Assert.Equal(new BlockPos(2, 2, 3), blockPos.East());
    }

    [Fact]
    public void OperatorsAddAndSubtractComponents()
    {
        var left = new BlockPos(8, 7, 6);
        var right = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(9, 9, 9), left + right);
        Assert.Equal(new BlockPos(7, 5, 3), left - right);
    }
}