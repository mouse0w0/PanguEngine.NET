using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.World;

public sealed class FlatWorldGeneratorTests
{
    [Fact]
    public void GenerateCreatesCenteredNineByNineChunkRegion()
    {
        var world = new ClientWorld();

        FlatWorldGenerator.Generate(world);

        var positions = world.Chunks.EnumerateChunks()
            .Select(chunk => chunk.Position)
            .ToHashSet();
        Assert.Equal(81, positions.Count);
        for (var z = -4; z <= 4; z++)
        {
            for (var x = -4; x <= 4; x++)
                Assert.Contains(new ChunkPos(x, 0, z), positions);
        }
    }

    [Fact]
    public void GenerateFillsFixedLayersAndLeavesOutsideAir()
    {
        var world = new ClientWorld();

        FlatWorldGenerator.Generate(world);

        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(new BlockPos(-64, 0, -64)));
        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(new BlockPos(79, 11, 79)));
        Assert.Same(BuiltinBlocks.Dirt.DefaultState, world.GetBlock(new BlockPos(-64, 12, 79)));
        Assert.Same(BuiltinBlocks.Dirt.DefaultState, world.GetBlock(new BlockPos(79, 14, -64)));
        Assert.Same(BuiltinBlocks.Grass.DefaultState, world.GetBlock(new BlockPos(0, 15, 0)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(-65, 15, 0)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(80, 15, 0)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(0, 15, -65)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(0, 15, 80)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(0, -1, 0)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(new BlockPos(0, 16, 0)));
    }
}