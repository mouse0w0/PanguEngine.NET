using PanguEngine.Server;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;

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
            initial = Assert.IsType<InitialWorldStatePacket>(await Task.Run(connection.WaitForPacket).WaitAsync(Timeout));
            Assert.False(connection.TrySend(new InputStatePacket(0, 0, 0, 0)));
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }

        var closed = Assert.IsType<ServerClosedPacket>(connection.WaitForPacket());
        Assert.Equal(initial.Tick, closed.Tick);
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
            await Task.Run(connection.WaitForPacket).WaitAsync(Timeout);
            connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, double.NaN, 0));

            actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() =>
            {
                while (true)
                    connection.WaitForPacket();
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
            var initial = Assert.IsType<InitialWorldStatePacket>(await Task.Run(connection.WaitForPacket).WaitAsync(Timeout));
            var final = Assert.IsType<WorldUpdatePacket>(await Task.Run(connection.WaitForPacket).WaitAsync(Timeout));
            var closed = Assert.IsType<ServerClosedPacket>(await Task.Run(connection.WaitForPacket).WaitAsync(Timeout));

            Assert.Equal((input.Yaw, input.Pitch), (final.Player.Yaw, final.Player.Pitch));
            Assert.Equal(initial.Player.Position, final.Player.Position);
            Assert.Equal(initial.Tick, final.Tick);
            Assert.Equal(final.Tick, closed.Tick);
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
            await Task.Run(connection.WaitForPacket).WaitAsync(Timeout);
            var moving = Assert.IsType<WorldUpdatePacket>(await Task.Run(connection.WaitForPacket).WaitAsync(Timeout));
            server.IsPaused = true;
            var pauseTick = server.Tick;
            server.IsPaused = false;

            await Task.Run(() =>
            {
                var previous = moving;
                var resubmitted = false;
                while (true)
                {
                    var next = Assert.IsType<WorldUpdatePacket>(connection.WaitForPacket());
                    Assert.Equal(previous.Tick + 1, next.Tick);
                    if (!resubmitted && next.Tick > pauseTick + 1)
                    {
                        Assert.Equal(previous.Player.Position, next.Player.Position);
                        Assert.True(connection.TrySend(new InputStatePacket(1, 0, -90, 0)));
                        resubmitted = true;
                    }
                    else if (resubmitted && next.Player.Position != previous.Player.Position)
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
}
