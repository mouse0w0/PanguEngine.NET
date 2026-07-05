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
    public void OperatorsAddAndSubtractComponents()
    {
        var left = new BlockPos(8, 7, 6);
        var right = new BlockPos(1, 2, 3);

        Assert.Equal(new BlockPos(9, 9, 9), left + right);
        Assert.Equal(new BlockPos(7, 5, 3), left - right);
    }
}