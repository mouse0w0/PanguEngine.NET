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

        var update = Update(game, initial.PlayerNetworkId, [input, input with { Forward = -1 }]);

        Assert.Equal(initial.Tick + 1, update.Tick);
        Assert.Equal(0.4, update.Players[0].Pose.Position.Z - initial.Player.Position.Z, 8);
        game.Destroy();
    }

    [Fact]
    public void RepeatedClicksUseCapturedDirectionAndSeePriorEdits()
    {
        var (game, initial) = CreateGame();
        var world = game.World;
        var first = new BlockPos(8, 22, 22);
        var second = new BlockPos(8, 22, 21);
        world.SetBlock(first, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(second, BuiltinBlocks.Stone.DefaultState);
        var click = new BlockInteractionPacket(BlockOperation.Break, -90, 0);

        var update = Update(game, initial.PlayerNetworkId, [
            click,
            click,
            new InputStatePacket(0, 0, 0, 0)]);

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
        var (game, initial) = CreateGame();
        var world = game.World;
        world.SetBlock(new BlockPos(8, 22, 22), BuiltinBlocks.Stone.DefaultState);

        var update = Update(game, initial.PlayerNetworkId, [
            new BlockInteractionPacket(BlockOperation.Place, -90, 0),
            new BlockInteractionPacket(BlockOperation.Break, -90, 0)]);

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
        var (game, initial) = CreateGame();
        var world = game.World;
        var target = new BlockPos(8, 22, 18);
        world.SetBlock(target, BuiltinBlocks.Stone.DefaultState);

        var update = Update(game, initial.PlayerNetworkId, [
            new InputStatePacket(-1, 0, -90, 0),
            new BlockInteractionPacket(BlockOperation.Break, -90, 0)]);

        Assert.True(Assert.Single(update.Results).IsSuccess);
        Assert.Equal(target, Assert.Single(update.Changes).Position);
        Assert.Equal(24.4, update.Players[0].Pose.Position.Z, 8);
        game.Destroy();
    }

    [Fact]
    public void InitialSnapshotMatchesCurrentWorldWhenPlayerJoinsLater()
    {
        var game = new ServerGame();
        Assert.Equal(0, game.World.Entities.Count);
        game.BeginTick();
        Assert.Empty(game.FinishTick().Players);
        var playerNetworkId = game.JoinPlayer();
        var initial = game.CreateInitial(playerNetworkId);

        Assert.Equal(1L, initial.Tick);
        Assert.Equal(game.World.Chunks.Count, initial.ChunkCount);
        Assert.Equal(game.FinishTick(advance: false).Players[0].Pose, initial.Player);
        game.Destroy();
    }

    private static (ServerGame Game, InitialWorldStatePacket Initial) CreateGame()
    {
        var game = new ServerGame();
        var playerNetworkId = game.JoinPlayer();
        return (game, game.CreateInitial(playerNetworkId));
    }

    private static WorldUpdatePacket Update(ServerGame game, uint playerNetworkId, IReadOnlyList<ServerboundPacket> packets)
    {
        game.BeginTick();
        var results = game.Apply(playerNetworkId, packets);
        return game.FinishTick() with { Results = results };
    }
}
