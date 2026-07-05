using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.World;

public sealed class ClientWorldTests
{
    [Fact]
    public void SetBlockStoresState()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 4, 32);

        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(position));
    }

    [Fact]
    public void IsAirReflectsStoredBlockState()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 4, 32);

        Assert.True(world.IsAir(position));

        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        Assert.False(world.IsAir(position));
    }
}