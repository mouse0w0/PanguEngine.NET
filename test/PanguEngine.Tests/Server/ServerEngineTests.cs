using PanguEngine.Server;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Network;

namespace PanguEngine.Tests.Server;

public sealed class ServerEngineTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PausedServerDiscardsPendingInputAndCanCloseWithoutAdvancing()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectLocal();
        connection.TrySend(new InputStatePacket(1, 0, 0, 0));
        connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, 0, 0));
        server.IsPaused = true;
        server.Start();
        InitialWorldStatePacket initial;
        try
        {
            Assert.Same(TestRegistryBootstrap.CustomIdValue, TestRegistryBootstrap.CustomIdTable.Get(0));
            Assert.Same(BuiltinBlocks.Stone, BuiltinIdTables.Block.Get(BuiltinIdTables.Block.GetId(BuiltinBlocks.Stone)));
            initial = await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
            Assert.False(connection.TrySend(new InputStatePacket(0, 0, 0, 0)));
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }

        var closed = Assert.IsType<DisconnectPacket>(connection.WaitForPacket());
        Assert.NotEmpty(closed.Reason);
        Assert.Equal(initial.Tick, server.Tick);
        Assert.Same(TestRegistryBootstrap.CustomIdValue, TestRegistryBootstrap.CustomIdTable.Get(0));
    }

    [Fact]
    public async Task RuntimeFailureIsRethrownByReceiverAndShutdown()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectLocal();
        server.Start();
        ArgumentOutOfRangeException actual;
        ArgumentOutOfRangeException shutdownError;
        try
        {
            await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
            connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, double.NaN, 0));

            actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() =>
            {
                while (true)
                    connection.WaitForPacket(Timeout);
            }).WaitAsync(Timeout));
        }
        finally
        {
            shutdownError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                Task.Run(server.Destroy).WaitAsync(Timeout));
        }

        Assert.Same(actual, shutdownError);
    }

    [Fact]
    public async Task ShutdownDrainsAcceptedInputWithoutExtraSimulation()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectLocal();
        var input = new InputStatePacket(0, 0, 42, 12);
        connection.TrySend(input);
        server.RequestShutdown();
        server.Start();
        try
        {
            var initial = await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
            var final = Assert.IsType<WorldUpdatePacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));
            var closed = Assert.IsType<DisconnectPacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));

            var player = Assert.Single(final.Players).Pose;
            Assert.Equal((input.Yaw, input.Pitch), (player.Yaw, player.Pitch));
            Assert.Equal(initial.Player.Position, player.Position);
            Assert.Equal(initial.Tick, final.Tick);
            Assert.NotEmpty(closed.Reason);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task QuickPauseResumeClearsOldMovementAndAcceptsNewInput()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectLocal();
        connection.TrySend(new InputStatePacket(1, 0, -90, 0));
        server.Start();
        try
        {
            await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
            var moving = Assert.IsType<WorldUpdatePacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));
            server.IsPaused = true;
            var pauseTick = server.Tick;
            server.IsPaused = false;

            await Task.Run(() =>
            {
                var previous = moving;
                var resubmitted = false;
                while (true)
                {
                    var next = Assert.IsType<WorldUpdatePacket>(connection.WaitForPacket(Timeout));
                    Assert.Equal(previous.Tick + 1, next.Tick);
                    if (!resubmitted && next.Tick > pauseTick + 1)
                    {
                        Assert.Equal(previous.Players[0].Pose.Position, next.Players[0].Pose.Position);
                        Assert.True(connection.TrySend(new InputStatePacket(1, 0, -90, 0)));
                        resubmitted = true;
                    }
                    else if (resubmitted && next.Players[0].Pose.Position != previous.Players[0].Pose.Position)
                        return;
                    previous = next;
                }
            }).WaitAsync(Timeout);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }
    }
    private static InitialWorldStatePacket ReceiveInitial(LocalConnection connection)
    {
        var initial = Assert.IsType<InitialWorldStatePacket>(connection.WaitForPacket(Timeout));
        for (var i = 0; i < initial.ChunkCount; i++)
            Assert.IsType<ChunkDataPacket>(connection.WaitForPacket(Timeout));
        Assert.IsType<InitialWorldCompletePacket>(connection.WaitForPacket(Timeout));
        return initial;
    }
}
