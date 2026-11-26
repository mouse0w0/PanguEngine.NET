using System.Diagnostics;
using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Registries;
using PanguEngine.Server;
using PanguEngine.Tests.Client.Game;
using PanguEngine.Tests.Network;
using PanguEngine.World.Blocks;

namespace PanguEngine.Tests.Server;

public sealed class ServerEngineTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void InvalidLocalUsernameDoesNotConsumeTheConnectionSlot()
    {
        var server = new ServerEngine(false);
        Assert.Throws<InvalidDataException>(() => server.ConnectInMemory("invalid name"));
        server.ConnectInMemory();
    }

    [Fact]
    public async Task PausedShutdownDoesNotApplyPendingInput()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory();
        server.IsPaused = true;
        Assert.True(connection.TrySend(new InputStatePacket(0, 0, 42, 12)));
        Assert.True(connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, 0, 0)));
        server.RequestShutdown();
        server.IsPaused = false;
        server.Start();
        InitialWorldStatePacket initial;
        try
        {
            Assert.Same(TestRegistryBootstrap.CustomIdValue, TestRegistryBootstrap.CustomIdTable.Get(0));
            Assert.Same(BuiltinBlocks.Stone, BuiltinIdTables.Block.Get(BuiltinIdTables.Block.GetId(BuiltinBlocks.Stone)));
            initial = await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }

        Assert.True(connection.IsClosed);
        var closed = Assert.IsType<DisconnectPacket>(connection.WaitForPacket());
        Assert.NotEmpty(closed.Reason);
        Assert.Throws<ConnectionClosedException>(() => connection.WaitForPacket(TimeSpan.Zero));
        Assert.Throws<ConnectionClosedException>(() => connection.TryReceive(out _));
        Assert.Equal(initial.Tick, server.Tick);
        Assert.Same(TestRegistryBootstrap.CustomIdValue, TestRegistryBootstrap.CustomIdTable.Get(0));
    }

    [Fact]
    public async Task RuntimeFailureIsRethrownByReceiverAndShutdown()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownDrainsAcceptedInputWithoutExtraSimulation(bool pauseAfterShutdown)
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory();
        var input = new InputStatePacket(0, 0, 42, 12);
        Assert.True(connection.TrySend(input));
        Assert.True(connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, 0, 0)));
        server.RequestShutdown();
        if (pauseAfterShutdown)
            server.IsPaused = true;
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
            Assert.Equal(BlockOperation.Break, Assert.Single(final.Results).Operation);
            Assert.NotEmpty(closed.Reason);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task PausedRoundDiscardsInputAndMovementRequiresNewInputOnResume()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory();
        var serverEndpoint = (IServerConnection)connection;
        server.IsPaused = true;
        Assert.True(connection.TrySend(new InputStatePacket(1, 0, -90, 0)));
        server.IsPaused = false;
        server.Start();
        try
        {
            var initial = await Task.Run(() => ReceiveInitial(connection)).WaitAsync(Timeout);
            var moving = Assert.IsType<WorldUpdatePacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));
            var movingPose = Assert.Single(moving.Players).Pose;
            Assert.NotEqual(initial.Player.Position, movingPose.Position);
            Assert.Equal((-90d, 0d), (movingPose.Yaw, movingPose.Pitch));
            server.IsPaused = true;
            Assert.True(connection.TrySend(new InputStatePacket(-1, 0, 42, 12)));
            Assert.True(connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, double.NaN, 0)));
            using var deadline = new CancellationTokenSource(Timeout);
            while (serverEndpoint.IncomingCount != 0)
                await Task.Delay(1, deadline.Token);

            var lastRunningUpdate = moving;
            while (connection.TryReceive(out var packet))
            {
                var queued = Assert.IsType<WorldUpdatePacket>(packet);
                Assert.Equal(lastRunningUpdate.Tick + 1, queued.Tick);
                var pose = Assert.Single(queued.Players).Pose;
                Assert.Equal((-90d, 0d), (pose.Yaw, pose.Pitch));
                Assert.Empty(queued.Results);
                lastRunningUpdate = queued;
            }
            var lastPose = Assert.Single(lastRunningUpdate.Players).Pose;

            server.IsPaused = false;
            var update = Assert.IsType<WorldUpdatePacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));
            Assert.Equal(lastRunningUpdate.Tick + 1, update.Tick);
            Assert.Equal(lastPose, Assert.Single(update.Players).Pose);
            Assert.Empty(update.Results);

            Assert.True(connection.TrySend(new InputStatePacket(1, 0, -90, 0)));
            var started = Stopwatch.GetTimestamp();
            while (Assert.Single(update.Players).Pose.Position == lastPose.Position && Stopwatch.GetElapsedTime(started) < Timeout)
                update = Assert.IsType<WorldUpdatePacket>(await Task.Run(() => connection.WaitForPacket(Timeout)).WaitAsync(Timeout));
            Assert.NotEqual(lastPose.Position, Assert.Single(update.Players).Pose.Position);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task InMemoryClientLoadsWorldAndPlaysWithoutAHandshake()
    {
        var server = new ServerEngine(false) { IsPaused = true };
        server.Start();
        var connection = server.ConnectInMemory();
        try
        {
            await Task.Run(() =>
            {
                var world = new ClientWorld();
                ClientSession? session = null;
                try
                {
                    var initial = ClientGameTests.LoadInitialWorld(connection, world);
                    Assert.Equal(0L, initial.Tick);
                    session = new ClientSession(connection, world, initial, 0);
                    Assert.Throws<InvalidOperationException>(() => server.ConnectInMemory("AnotherPlayer"));
                    server.IsPaused = false;
                    session.SubmitInput(0, 0, 42, 12);

                    var started = Stopwatch.GetTimestamp();
                    while (world.Player.Rotation.Yaw != 42 && Stopwatch.GetElapsedTime(started) < Timeout)
                    {
                        Thread.Sleep(1);
                        session.ReceivePending(1);
                    }
                    Assert.Equal((42d, 12d), (world.Player.Rotation.Yaw, world.Player.Rotation.Pitch));
                }
                finally
                {
                    session?.BeginClose();
                    world.Destroy();
                }
            }).WaitAsync(Timeout);
        }
        finally
        {
            await Task.Run(server.Destroy).WaitAsync(Timeout);
            connection.Close();
        }
    }

    [Fact]
    public void ShutdownWithOnlyCustomInputDoesNotPublishAFinalUpdate()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory();
        Assert.True(connection.TrySend(new TestPlayPackets.EchoRequest(42)));
        server.RequestShutdown();
        server.Start();
        try
        {
            ReceiveInitial(connection);
            Assert.IsType<DisconnectPacket>(connection.WaitForPacket(Timeout));
        }
        finally
        {
            server.Destroy();
            connection.Close();
        }
    }

    [Fact]
    public void InMemoryPlayDispatchesCustomRequestToTheSendingPlayer()
    {
        var server = new ServerEngine(false);
        var connection = server.ConnectInMemory("Alice");
        server.Start();
        try
        {
            ReceiveInitial(connection);
            Assert.True(connection.TrySend(new TestPlayPackets.EchoRequest(42)));

            var started = Stopwatch.GetTimestamp();
            ClientboundPacket packet;
            do
            {
                var remaining = Timeout - Stopwatch.GetElapsedTime(started);
                Assert.True(remaining > TimeSpan.Zero);
                packet = connection.WaitForPacket(remaining);
            } while (packet is WorldUpdatePacket);

            Assert.Equal(new TestPlayPackets.EchoReply("Alice", 42), Assert.IsType<TestPlayPackets.EchoReply>(packet));
        }
        finally
        {
            server.Destroy();
            connection.Close();
        }
    }

    private static InitialWorldStatePacket ReceiveInitial(IClientConnection connection)
    {
        var initial = Assert.IsType<InitialWorldStatePacket>(connection.WaitForPacket(Timeout));
        for (var i = 0; i < initial.ChunkCount; i++)
            Assert.IsType<ChunkDataPacket>(connection.WaitForPacket(Timeout));
        Assert.IsType<InitialWorldCompletePacket>(connection.WaitForPacket(Timeout));
        return initial;
    }
}
