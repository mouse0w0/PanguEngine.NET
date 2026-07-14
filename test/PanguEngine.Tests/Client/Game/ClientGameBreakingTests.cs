using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Interaction;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientGameBreakingTests
{
    [Fact]
    public void TryBreakBlockReplacesSelectedBlockWithAir()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        var broken = ClientGame.TryBreakBlock(world, CreateHit(position));

        Assert.True(broken);
        Assert.Same(BuiltinBlocks.Air.DefaultState, world.GetBlock(position));
    }

    [Fact]
    public void TryBreakBlockReturnsFalseWithoutSelection()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunksBefore = GetChunkPositions(world);

        var broken = ClientGame.TryBreakBlock(world, null);

        Assert.False(broken);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(position));
        Assert.Equal(chunksBefore, GetChunkPositions(world));
    }

    private static BlockHit CreateHit(BlockPos position)
    {
        return new BlockHit(
            position,
            BuiltinBlocks.Stone.DefaultState,
            default,
            Direction.Up,
            0,
            false);
    }

    private static ChunkPos[] GetChunkPositions(ClientWorld world)
    {
        return world.Chunks.EnumerateChunks()
            .Select(chunk => chunk.Position)
            .OrderBy(position => position.X)
            .ThenBy(position => position.Y)
            .ThenBy(position => position.Z)
            .ToArray();
    }
}