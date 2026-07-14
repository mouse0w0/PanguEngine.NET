using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Chunking;

public sealed class ChunkManagerTests
{
    [Fact]
    public void GetBlockReturnsAirForMissingChunkWithoutCreatingChunk()
    {
        var manager = new ChunkManager();

        Assert.Same(BuiltinBlocks.Air.DefaultState, manager.GetBlock(new BlockPos(64, 0, 0)));
        Assert.Empty(manager.EnumerateChunks());
    }

    [Fact]
    public void SetBlockCreatesTargetChunk()
    {
        var manager = new ChunkManager();

        manager.SetBlock(new BlockPos(16, 0, 0), BuiltinBlocks.Stone.DefaultState);

        var chunk = Assert.Single(manager.EnumerateChunks());
        Assert.Equal(new ChunkPos(1, 0, 0), chunk.Position);
    }

    [Fact]
    public void ReadsAndWritesAcrossChunks()
    {
        var manager = new ChunkManager();

        manager.SetBlock(new BlockPos(0, 0, 0), BuiltinBlocks.Stone.DefaultState);
        manager.SetBlock(new BlockPos(16, 0, 0), BuiltinBlocks.Dirt.DefaultState);
        manager.SetBlock(new BlockPos(-1, 0, 0), BuiltinBlocks.Grass.DefaultState);

        Assert.Same(BuiltinBlocks.Stone.DefaultState, manager.GetBlock(new BlockPos(0, 0, 0)));
        Assert.Same(BuiltinBlocks.Dirt.DefaultState, manager.GetBlock(new BlockPos(16, 0, 0)));
        Assert.Same(BuiltinBlocks.Grass.DefaultState, manager.GetBlock(new BlockPos(-1, 0, 0)));
        Assert.Equal(
            [new ChunkPos(-1, 0, 0), new ChunkPos(0, 0, 0), new ChunkPos(1, 0, 0)],
            manager.EnumerateChunks().Select(chunk => chunk.Position).OrderBy(pos => pos.X).ToArray());
    }
}