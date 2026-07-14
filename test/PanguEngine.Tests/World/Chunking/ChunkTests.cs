using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Chunking;

public sealed class ChunkTests
{
    [Fact]
    public void NewChunkContainsAir()
    {
        var chunk = new Chunk(new ChunkPos(0, 0, 0));

        Assert.Same(BuiltinBlocks.Air.DefaultState, chunk.GetBlock(new BlockPos(0, 0, 0)));
    }

    [Fact]
    public void SetBlockStoresStateByLocalSlot()
    {
        var chunk = new Chunk(new ChunkPos(0, 0, 0));

        chunk.SetBlock(new BlockPos(1, 2, 3), BuiltinBlocks.Stone.DefaultState);

        Assert.Same(BuiltinBlocks.Stone.DefaultState, chunk.GetBlock(new BlockPos(1, 2, 3)));
    }

    [Fact]
    public void LocalSlotUsesLowBits()
    {
        var chunk = new Chunk(new ChunkPos(0, 0, 0));

        chunk.SetBlock(new BlockPos(16, -1, 32), BuiltinBlocks.Dirt.DefaultState);

        Assert.Same(BuiltinBlocks.Dirt.DefaultState, chunk.GetBlock(new BlockPos(0, 15, 0)));
    }

    [Fact]
    public void NegativeChunkBoundaryUsesLowBits()
    {
        var chunk = new Chunk(new ChunkPos(-1, 0, 0));

        chunk.SetBlock(new BlockPos(-1, 0, 0), BuiltinBlocks.Grass.DefaultState);

        Assert.Same(BuiltinBlocks.Grass.DefaultState, chunk.GetBlock(new BlockPos(15, 0, 0)));
    }

    [Fact]
    public void EnumerateBlocksReturnsLocalPositionsInStorageOrder()
    {
        var chunk = new Chunk(new ChunkPos(0, 0, 0));
        chunk.SetBlock(new BlockPos(1, 0, 0), BuiltinBlocks.Stone.DefaultState);
        chunk.SetBlock(new BlockPos(0, 0, 1), BuiltinBlocks.Dirt.DefaultState);
        chunk.SetBlock(new BlockPos(0, 1, 0), BuiltinBlocks.Grass.DefaultState);

        var blocks = chunk.EnumerateBlocks().ToArray();

        Assert.Equal(Chunk.Volume, blocks.Length);
        Assert.Equal(new BlockPos(0, 0, 0), blocks[0].LocalPosition);
        Assert.Equal(new BlockPos(1, 0, 0), blocks[1].LocalPosition);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, blocks[1].State);
        Assert.Equal(new BlockPos(0, 0, 1), blocks[Chunk.SizeX].LocalPosition);
        Assert.Same(BuiltinBlocks.Dirt.DefaultState, blocks[Chunk.SizeX].State);
        Assert.Equal(new BlockPos(0, 1, 0), blocks[Chunk.SizeX * Chunk.SizeZ].LocalPosition);
        Assert.Same(BuiltinBlocks.Grass.DefaultState, blocks[Chunk.SizeX * Chunk.SizeZ].State);
        Assert.Equal(new BlockPos(15, 15, 15), blocks[^1].LocalPosition);
    }
}