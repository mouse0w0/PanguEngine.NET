using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server.Game;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;

namespace PanguEngine.Tests.Server;

public sealed class MultiplayerGameTests
{
    [Fact]
    public void SeparatePlayersMoveWithinOneWorldStepAndLeaveIndependently()
    {
        var game = new ServerGame();
        var first = game.JoinPlayer();
        var second = game.JoinPlayer();
        var before = game.CreateInitial(first).Player.Position;
        game.BeginTick();
        game.Apply(first, [new InputStatePacket(1, 0, -90, 0)]);
        game.Apply(second, [new InputStatePacket(-1, 0, -90, 0)]);
        var update = game.FinishTick();
        Assert.Equal(1L, update.Tick);
        Assert.Equal(before.Z - 0.4, update.Players.Single(p => p.NetworkId == first).Pose.Position.Z, 8);
        Assert.Equal(before.Z + 0.4, update.Players.Single(p => p.NetworkId == second).Pose.Position.Z, 8);
        game.LeavePlayer(first);
        Assert.Equal(second, Assert.Single(game.FinishTick(false).Players).NetworkId);
        game.Destroy();
    }

    [Fact]
    public void ExtremeFiniteYawDoesNotPoisonMovementOrInteraction()
    {
        var game = new ServerGame();
        var player = game.JoinPlayer();
        game.BeginTick();
        game.Apply(player,
        [
            new InputStatePacket(1, 0, 1e308, 0),
            new BlockInteractionPacket(PanguEngine.Network.Packets.BlockOperation.Break, 1e308, 0)
        ]);
        var position = Assert.Single(game.FinishTick().Players).Pose.Position;
        Assert.True(double.IsFinite(position.X) && double.IsFinite(position.Y) && double.IsFinite(position.Z));
        game.Destroy();
    }

    [Fact]
    public void PlayersShareOrderedEditsButKeepTheirOwnOperationResults()
    {
        var game = new ServerGame();
        var first = game.JoinPlayer();
        var second = game.JoinPlayer();
        var target = new BlockPos(8, 22, 22);
        game.World.SetBlock(target, BuiltinBlocks.Stone.DefaultState);
        game.BeginTick();
        var firstResults = game.Apply(first, [new BlockInteractionPacket(BlockOperation.Break, -90, 0)]);
        var secondResults = game.Apply(second, [new BlockInteractionPacket(BlockOperation.Break, -90, 0)]);
        var update = game.FinishTick();
        Assert.Equal(WorldUpdatePacket.InteractionStatus.Success, Assert.Single(firstResults).Status);
        Assert.Equal(WorldUpdatePacket.InteractionStatus.NoHit, Assert.Single(secondResults).Status);
        Assert.Equal(target, Assert.Single(update.Changes).Position);
        var third = game.JoinPlayer();
        var chunk = game.CaptureChunks().Single(c => c.Position == new ChunkPos(0, 1, 1));
        var index = 8 | (6 << Chunk.BitsX) | (6 << (Chunk.BitsX + Chunk.BitsZ));
        Assert.True(chunk.GetBlock(index).IsAir);
        Assert.Equal(3, game.CreateInitial(third).Players.Count);
        game.Destroy();
    }
}
