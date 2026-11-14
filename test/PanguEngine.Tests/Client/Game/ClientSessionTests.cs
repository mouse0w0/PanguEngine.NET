using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using Silk.NET.Maths;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientSessionTests
{
    [Fact]
    public void QueuedUpdatesAndFeedbackAreAppliedBeforeDisconnectReason()
    {
        var connection = new LocalConnection();
        var world = new ClientWorld();
        var id = 1u;
        var initial = new InitialWorldStatePacket(0, id, 0.05, [new(id, default)], 0);
        world.ApplyInitial(initial);
        var session = new ClientSession(connection, world, initial, 0);
        var first = new BlockPos(1, 1, 1);
        var second = new BlockPos(2, 1, 1);
        var notifications = 0;
        world.BlockChanged += _ =>
        {
            var expectedTick = notifications < 2 ? 1L : 2L;
            Assert.Equal(expectedTick, world.Tick);
            Assert.Equal(expectedTick == 2, world.GetBlock(first).IsAir);
            Assert.False(world.GetBlock(second).IsAir);
            notifications++;
        };
        var results = new List<InteractionResult>();
        session.OperationCompleted += results.Add;
        connection.Publish(new WorldUpdatePacket(1, [new(id, default)],
            [new(first, BuiltinBlocks.Stone.DefaultState), new(second, BuiltinBlocks.Stone.DefaultState)],
            [new(BlockOperation.Place, InteractionStatus.Success, second),
             new(BlockOperation.Break, InteractionStatus.NoHit, default)]));
        connection.Publish(new WorldUpdatePacket(2, [new(id, default)],
            [new(first, BuiltinBlocks.Air.DefaultState)],
            [new(BlockOperation.Break, InteractionStatus.Success, first)]));
        var reason = "This session has ended.";
        connection.Publish(new DisconnectPacket(reason));

        var error = Assert.Throws<ConnectionClosedException>(() => session.ReceivePending(1));

        Assert.Equal(reason, error.Message);
        Assert.Equal(3, notifications);
        Assert.Equal([second, first], results.Select(result => result.Position));
        world.Destroy();
    }

    [Fact]
    public void ReceivedStateUpdatesWorldAndResetsInterpolationClock()
    {
        var connection = new LocalConnection();
        var world = new ClientWorld();
        var id = 1u;
        var initial = new InitialWorldStatePacket(0, id, 0.05, [new(id, default)], 0);
        world.ApplyInitial(initial);
        var session = new ClientSession(connection, world, initial, 0);
        var position = new Vector3D<double>(1, 0, 0);
        connection.Publish(new WorldUpdatePacket(1, [new(id, new PlayerPose(position, 45, 10))], [], []));

        session.ReceivePending(10);

        Assert.Equal(position, world.Player.Position.Current);
        Assert.Equal(0, session.GetInterpolationAlpha(9));
        Assert.Equal(0.5, session.GetInterpolationAlpha(10.025), 8);
        Assert.Equal(1, session.GetInterpolationAlpha(11));
    }

}
