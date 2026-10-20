using PanguEngine.Client.World;
using PanguEngine.Tests.World.Entities;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities;

namespace PanguEngine.Tests.Client.World;

public sealed class ClientWorldTests
{
    [Fact]
    public void ConstructorCreatesEmptyWorld()
    {
        var world = new ClientWorld();

        Assert.Empty(world.Chunks.EnumerateChunks());
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

    [Fact]
    public void SetBlockNotifiesAfterStateIsStored()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 4, 32);
        var notifications = new List<BlockPos>();
        BlockState? observedState = null;
        world.BlockChanged += changedPosition =>
        {
            notifications.Add(changedPosition);
            observedState = world.GetBlock(changedPosition);
        };

        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        Assert.Equal([position], notifications);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, observedState);
    }

    [Fact]
    public void DestroyInvalidatesWorldEntities()
    {
        var world = new ClientWorld();
        var player = world.Entities.Spawn(
            BuiltinEntityTypes.Player,
            EntityTestContext.CreatePlayerValues(default));

        world.Destroy();

        Assert.False(player.IsAlive);
        world.Destroy();
    }
}
