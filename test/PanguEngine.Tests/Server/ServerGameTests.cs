using PanguEngine.Server.Game;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;

namespace PanguEngine.Tests.Server;

public sealed class ServerGameTests
{
    [Fact]
    public void LatestInputAdvancesMovementOnlyOnce()
    {
        var (game, initial) = CreateGame();
        var input = new InputStatePacket(1, 0, -90, 0);

        var update = game.Update([input, input with { Forward = -1 }])!;

        Assert.Equal(initial.Tick + 1, update.Tick);
        Assert.Equal(0.4, update.Player.Position.Z - initial.Player.Position.Z, 8);
        game.Destroy();
    }

    [Fact]
    public void RepeatedClicksUseCapturedDirectionAndSeePriorEdits()
    {
        var (game, _) = CreateGame();
        var world = game.World;
        var first = new BlockPos(8, 22, 22);
        var second = new BlockPos(8, 22, 21);
        world.SetBlock(first, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(second, BuiltinBlocks.Stone.DefaultState);
        var click = new BlockInteractionPacket(BlockOperation.Break, -90, 0);

        var update = game.Update([
            click,
            click,
            new InputStatePacket(0, 0, 0, 0)])!;

        Assert.Equal([first, second], update.Results.Select(result => result.Position));
        Assert.All(update.Results, result => Assert.True(result.IsSuccess));
        Assert.Equal([first, second], update.Changes.Select(change => change.Position));
        Assert.True(world.GetBlock(first).IsAir);
        Assert.True(world.GetBlock(second).IsAir);
        game.Destroy();
    }

    [Fact]
    public void MixedInteractionsPreserveOrderAndIntermediateChanges()
    {
        var (game, _) = CreateGame();
        var world = game.World;
        world.SetBlock(new BlockPos(8, 22, 22), BuiltinBlocks.Stone.DefaultState);

        var update = game.Update([
            new BlockInteractionPacket(BlockOperation.Place, -90, 0),
            new BlockInteractionPacket(BlockOperation.Break, -90, 0)])!;

        Assert.Equal([BlockOperation.Place, BlockOperation.Break], update.Results.Select(result => result.Operation));
        Assert.All(update.Results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, update.Changes.Count);
        Assert.Equal(update.Changes[0].Position, update.Changes[1].Position);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, update.Changes[0].State);
        Assert.True(update.Changes[1].State.IsAir);
        game.Destroy();
    }

    [Fact]
    public void InteractionUsesPositionBeforeThisRoundsMovement()
    {
        var (game, _) = CreateGame();
        var world = game.World;
        var target = new BlockPos(8, 22, 18);
        world.SetBlock(target, BuiltinBlocks.Stone.DefaultState);

        var update = game.Update([
            new InputStatePacket(-1, 0, -90, 0),
            new BlockInteractionPacket(BlockOperation.Break, -90, 0)])!;

        Assert.True(Assert.Single(update.Results).IsSuccess);
        Assert.Equal(target, Assert.Single(update.Changes).Position);
        Assert.Equal(24.4, update.Player.Position.Z, 8);
        game.Destroy();
    }

    [Fact]
    public void InitialSnapshotMatchesCurrentWorldWhenPlayerJoinsLater()
    {
        var game = new ServerGame();
        Assert.Equal(0, game.World.Entities.Count);
        Assert.Null(game.Update([]));
        var connection = new LocalConnection();
        game.JoinLocal(connection);

        var initial = Assert.IsType<InitialWorldStatePacket>(connection.WaitForPacket());

        Assert.Equal(1L, initial.Tick);
        Assert.Equal(game.World.Chunks.Count, initial.Chunks.Count);
        Assert.Equal(game.Update([], advance: false)!.Player, initial.Player);
        game.Destroy();
    }

    private static (ServerGame Game, InitialWorldStatePacket Initial) CreateGame()
    {
        var game = new ServerGame();
        var connection = new LocalConnection();
        game.JoinLocal(connection);
        return (game, Assert.IsType<InitialWorldStatePacket>(connection.WaitForPacket()));
    }
}
