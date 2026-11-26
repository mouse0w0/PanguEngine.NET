using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server;
using PanguEngine.Server.Game;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Tests.Server;

public sealed class BlockInteractionTests
{
    [Fact]
    public void MissingSelectionIsRejectedWithoutChanges()
    {
        var (game, player) = CreateGame();

        var update = Update(game, player, [new BlockInteractionPacket(BlockOperation.Place, -90, 0)]);

        Assert.Equal(InteractionStatus.NoHit, Assert.Single(update.Results).Status);
        Assert.Empty(update.Changes);
        game.Destroy();
    }

    [Fact]
    public void PlacementUsesAdjacentFaceAndDoesNotReplaceOccupiedTarget()
    {
        var (game, player) = CreateGame();
        var world = game.World;
        var hit = new BlockPos(8, 22, 22);
        var target = new BlockPos(8, 22, 23);
        world.SetBlock(hit, BuiltinBlocks.Grass.DefaultState);
        var request = new BlockInteractionPacket(BlockOperation.Place, -90, 0);

        var placed = Update(game, player, [request]);

        Assert.Equal(target, Assert.Single(placed.Changes).Position);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(target));
        var occupied = new UnselectableBlock().DefaultState;
        world.SetBlock(target, occupied);

        var refused = Update(game, player, [request]);

        Assert.Equal(InteractionStatus.Occupied, Assert.Single(refused.Results).Status);
        Assert.Empty(refused.Changes);
        Assert.Same(occupied, world.GetBlock(target));
        game.Destroy();
    }

    private static (ServerGame Game, Player Player) CreateGame()
    {
        var game = new ServerGame();
        var names = new PlayerNames();
        names.Reserve("Player");
        var session = new RemoteSession(new InMemoryConnection(), "Player", names);
        return (game, new Player("Player", game.SpawnPlayerEntity(), session));
    }

    private static WorldUpdatePacket Update(
        ServerGame game, Player player, IReadOnlyList<ServerboundPacket> packets)
    {
        game.BeginTick();
        var results = game.Apply(player, packets);
        return game.FinishTick() with { Results = results };
    }

    private sealed class UnselectableBlock : Block
    {
        public override IBlockShape GetSelectionShape(BlockState state, IReadOnlyBlockAccessor blockAccessor, BlockPos position) =>
            BlockShape.Empty;
    }
}
