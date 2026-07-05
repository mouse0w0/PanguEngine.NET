using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Chunking;

public sealed class ChunkPosTests
{
    [Fact]
    public void EqualComponentsAreEqual()
    {
        var first = new ChunkPos(-1, 2, 3);
        var second = new ChunkPos(-1, 2, 3);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var chunks = new Dictionary<ChunkPos, string>
        {
            [new ChunkPos(1, 2, 3)] = "created"
        };

        Assert.True(chunks.TryGetValue(new ChunkPos(1, 2, 3), out var value));
        Assert.Equal("created", value);
    }

    [Fact]
    public void OffsetAddsComponents()
    {
        var chunkPos = new ChunkPos(1, 2, 3);

        Assert.Equal(new ChunkPos(5, 7, 9), chunkPos.Offset(4, 5, 6));
        Assert.Equal(new ChunkPos(5, 7, 9), chunkPos.Offset(new ChunkPos(4, 5, 6)));
    }

    [Fact]
    public void OperatorsAddAndSubtractComponents()
    {
        var left = new ChunkPos(8, 7, 6);
        var right = new ChunkPos(1, 2, 3);

        Assert.Equal(new ChunkPos(9, 9, 9), left + right);
        Assert.Equal(new ChunkPos(7, 5, 3), left - right);
    }
}