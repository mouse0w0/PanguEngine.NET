using System.Net;
using System.Net.Sockets;
using System.Diagnostics.CodeAnalysis;
using PanguEngine.Client.World;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server.Game;
using PanguEngine.Server;
using PanguEngine.Tests.Client.Game;
using PanguEngine.Modding;
using PanguEngine.Versioning;
using PanguEngine.World.Blocks;

namespace PanguEngine.Tests.Network;

[Collection("Network globals")]
public sealed class RemoteSessionTests
{
    [Fact]
    public void ClosingInMemoryClientLeavesThePlayingSession()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var game = new ServerGame();
        var connection = new InMemoryConnection();
        var players = new PlayerManager();
        var names = new PlayerNames();
        names.Reserve("Alice");
        try
        {
            var session = new RemoteSession(connection, "Alice", names);
            Player? player = null;
            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, session, ref player));
            var initial = Assert.IsType<InitialWorldStatePacket>(connection.WaitForPacket(TimeSpan.Zero));
            Assert.Equal(initial.PlayerNetworkId, Assert.Single(initial.Players).NetworkId);
            var joinedPlayer = Assert.Single(players.OnlinePlayers);
            Assert.Same(player, joinedPlayer);
            Assert.Same(session, joinedPlayer.Session);

            connection.Close();
            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, session, ref player));

            Assert.Null(player);
            Assert.Empty(game.CreateInitial(initial.PlayerNetworkId).Players);
            Assert.Empty(players.OnlinePlayers);
            Assert.Same(session, joinedPlayer.Session);
            names.Reserve("ALICE");
        }
        finally
        {
            game.Destroy();
        }
    }

    [Fact]
    public void IncompatibleProtocolIsRejectedBeforeLogin()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var peer = new SessionPeer(listener, new PlayerNames());
            peer.Send(ConnectionPhase.Handshake, new ClientHelloPacket(PacketCodec.ProtocolVersion - 1));
            Assert.NotEmpty(Assert.IsType<DisconnectPacket>(peer.Read(ConnectionPhase.Handshake)).Reason);
            Assert.False(peer.Session.Preparation.WaitAsync(peer.Token).GetAwaiter().GetResult().IsReady);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void InMemoryRequestsKeepTheirOrderAndBatchBoundary()
    {
        var connection = new InMemoryConnection();
        var names = new PlayerNames();
        names.Reserve("Alice");
        var session = new RemoteSession(connection, "Alice", names);
        var place = new BlockInteractionPacket(BlockOperation.Place, 0, 0);
        var remove = new BlockInteractionPacket(BlockOperation.Break, 0, 0);
        var input = new InputStatePacket(0, 0, 0, 0);
        Assert.True(connection.TrySend(place));
        Assert.True(connection.TrySend(remove));
        Assert.True(session.TryReceiveBatch(out var first));
        Assert.True(connection.TrySend(input));
        Assert.True(session.TryReceiveBatch(out var second));

        Assert.Equal<ServerboundPacket>([place, remove], first);
        Assert.Same(input, Assert.Single(second));
    }

    [Theory]
    [InlineData(typeof(InitialWorldStatePacket))]
    [InlineData(typeof(ChunkDataPacket))]
    [InlineData(typeof(InitialWorldCompletePacket))]
    public void RejectedInitialWorldPacketDoesNotHandOffPlayInput(Type rejectedType)
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var game = new ServerGame();
        try
        {
            var connection = new RejectingInitialWorldConnection(rejectedType);
            var players = new PlayerManager();
            var names = new PlayerNames();
            names.Reserve("Alice");
            var session = new RemoteSession(connection, "Alice", names);
            var initialEntityCount = game.World.Entities.Count;
            Player? player = null;

            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, session, ref player));

            Assert.Equal(rejectedType, connection.LastSendAttempt);
            Assert.Equal(0, connection.ReceiveAttempts);
            Assert.Null(player);
            Assert.Empty(players.OnlinePlayers);
            Assert.Equal(initialEntityCount, game.World.Entities.Count);
            Assert.Throws<InvalidDataException>(() => names.Reserve("ALICE"));

            connection.IsClosed = true;
            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, session, ref player));
            names.Reserve("ALICE");
        }
        finally { game.Destroy(); }
    }

    [Fact]
    public async Task ClientSetupPreservesTheServerDisconnectReason()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        var timeout = TimeSpan.FromSeconds(5);
        using var stop = new CancellationTokenSource(timeout);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var address = (IPEndPoint)listener.LocalEndpoint;
            var connecting = Task.Run(() => TcpClientConnection.Connect(address, "invalid name", TestRegistryBootstrap.Manager, []));
            var connection = new TcpConnection(await listener.AcceptTcpClientAsync(stop.Token));
            try
            {
                var endpoint = new TcpServerConnection(connection);
                var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
                var preparation = endpoint.PrepareAsync(manifest, [], new PlayerNames());
                var error = await Assert.ThrowsAsync<ConnectionClosedException>(() => connecting.WaitAsync(timeout));
                Assert.Contains("Username", error.Message, StringComparison.Ordinal);
                Assert.False((await preparation.WaitAsync(timeout)).IsReady);
            }
            finally { connection.Destroy(); }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void LoginOwnsNameAcrossConfigurationAndGameThreadHandoff()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var game = new ServerGame();
        var players = new PlayerManager();
        var names = new PlayerNames();
        try
        {
            using var first = new SessionPeer(listener, names);
            Player? connectedPlayer = null;
            Assert.IsType<LoginSuccessPacket>(Login(first.Client, "Alice", first.Token));
            Assert.False(first.Session.Preparation.IsCompleted);
            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, first.Session, ref connectedPlayer));
            Assert.Empty(players.OnlinePlayers);
            Assert.Throws<InvalidDataException>(() => names.Reserve("alice"));

            using (var duplicate = new SessionPeer(listener, names))
            {
                Assert.IsType<DisconnectPacket>(Login(duplicate.Client, "ALICE", duplicate.Token));
                duplicate.Session.Preparation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            Assert.Throws<InvalidDataException>(() => names.Reserve("alice"));

            first.Send(ConnectionPhase.Configuration, new ClientModsPacket([]));
            Assert.IsType<ContentManifestPacket>(first.Read(ConnectionPhase.Configuration));
            first.Send(ConnectionPhase.Configuration, new ContentReadyPacket());
            var input = new InputStatePacket(0, 0, 42, 12);
            first.Send(ConnectionPhase.Play, input);
            first.Session.Preparation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            first.Connection.WaitForFrame(TimeSpan.FromSeconds(5));
            Assert.Throws<InvalidDataException>(() => names.Reserve("alice"));
            Assert.Empty(players.OnlinePlayers);
            game.BeginTick();
            Assert.Equal(input, Assert.Single(ServerEngine.CollectPlayPackets(game, players, first.Session, ref connectedPlayer)));
            var player = Assert.Single(players.OnlinePlayers);
            Assert.Same(connectedPlayer, player);
            Assert.True(players.TryGetPlayer("alice", out var found));
            Assert.Same(player, found);
            Assert.Same(first.Session, player.Session);
            var handle = player.Entity.Handle;
            Assert.True(game.World.Entities.TryGetEntity(handle, out var entity));
            Assert.Same(player.Entity, entity);
            Assert.Equal(player.Entity.NetworkId, Assert.IsType<InitialWorldStatePacket>(first.Read(ConnectionPhase.Play)).PlayerNetworkId);
            Assert.Throws<InvalidDataException>(() => names.Reserve("alice"));
            game.FinishTick();

            first.Send(ConnectionPhase.Play, input);
            first.Send(ConnectionPhase.Play, new InputStatePacket(2, 0, 42, 12));
            Assert.True(SpinWait.SpinUntil(() => first.Connection.IncomingCount == 2, TimeSpan.FromSeconds(5)));
            Assert.Empty(ServerEngine.CollectPlayPackets(game, players, first.Session, ref connectedPlayer));
            Assert.Null(connectedPlayer);
            Assert.Empty(players.OnlinePlayers);
            Assert.False(game.World.Entities.TryGetEntity(handle, out _));
            first.Connection.Close("The test stopped.");
            first.Session.ReleaseNameIfClosed();
            names.Reserve("alice");
            first.Session.ReleaseNameIfClosed();
            Assert.Throws<InvalidDataException>(() => names.Reserve("ALICE"));
        }
        finally
        {
            game.Destroy();
            listener.Stop();
        }
    }

    [Fact]
    public void ClosingAfterConfigurationFailureReleasesTheUsername()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var names = new PlayerNames();
        try
        {
            using (var peer = new SessionPeer(listener, names))
            {
                Assert.IsType<LoginSuccessPacket>(Login(peer.Client, "Alice", peer.Token));
                peer.Send(ConnectionPhase.Configuration, new ContentReadyPacket());
                Assert.IsType<DisconnectPacket>(peer.Read(ConnectionPhase.Configuration));
                var (username, isReady) = peer.Session.Preparation.WaitAsync(peer.Token).GetAwaiter().GetResult();
                Assert.Equal("Alice", username);
                Assert.False(isReady);
            }
            names.Reserve("ALICE");
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [InlineData("1.2.3+build1", false, true)]
    [InlineData("1.2.3+build2", false, false)]
    [InlineData(null, false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("1.2.3+build1", true, false)]
    public void ConfigurationChecksServerFullVersionsAndAllowsExtraClientMods(
        string? requiredVersion, bool duplicate, bool acceptedHello)
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
            var connection = new TcpConnection(accepted);
            ModInfo[] mods =
            [
                new("required", SemVersion.Parse("1.2.3+build1"), "TestMod.dll", "TestMod.Entry", [])
            ];
            var endpoint = new TcpServerConnection(connection);
            var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
            var names = new PlayerNames();
            var session = new RemoteSession(endpoint, endpoint.PrepareAsync(manifest, mods, names), names);
            try
            {
                var clientMods = new List<ModVersionEntry> { new("extra", "9.0.0") };
                if (requiredVersion is not null)
                {
                    clientMods.Add(new ModVersionEntry("required", requiredVersion));
                    if (duplicate)
                        clientMods.Add(new ModVersionEntry("required", requiredVersion));
                }
                Assert.IsType<LoginSuccessPacket>(Login(client, "Alice", deadline.Token));
                TcpFraming.WriteAsync(client.GetStream(), PacketCodec.Encode(ConnectionPhase.Configuration, new ClientModsPacket(clientMods)), deadline.Token).GetAwaiter().GetResult();
                var frame = TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult();
                var response = PacketCodec.ReadClientbound(ConnectionPhase.Configuration, frame!.Value.Span);

                if (acceptedHello)
                    Assert.IsType<ContentManifestPacket>(response);
                else
                    Assert.NotEmpty(Assert.IsType<DisconnectPacket>(response).Reason);
            }
            finally
            {
                connection.Close("The test stopped.");
                session.Preparation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                session.ReleaseNameIfClosed();
                connection.Destroy();
            }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void ServerDisconnectBeforeHelloSendsOnlyFirstReason()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
            var connection = new TcpConnection(accepted);
            var endpoint = new TcpServerConnection(connection);
            var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
            var names = new PlayerNames();
            var session = new RemoteSession(endpoint, endpoint.PrepareAsync(manifest, [], names), names);
            try
            {
                var reason = "The server stopped before the handshake.";
                session.Disconnect(reason);
                endpoint.Disconnect("A later close reason.");

                var frame = TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult();
                var packet = Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Handshake, frame!.Value.Span));
                Assert.Equal(reason, packet.Reason);
                Assert.Null(TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult());
            }
            finally
            {
                connection.Close("The test stopped.");
                session.Preparation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                connection.Destroy();
            }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void ProductionHandshakeBootstrapsTwoPlayersAndIsolatesDisconnect()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = listener.LocalEndpoint;
        var host = Task.Run(() =>
        {
            var game = new ServerGame();
            var sessions = new List<ServerEngine.DedicatedSession>();
            long tick = 0;
            game.World.SetBlock(new(8, 22, 22), BuiltinBlocks.Stone.DefaultState);
            var players = new PlayerManager();
            var names = new PlayerNames();
            var accepted = new List<TcpConnection>();
            var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
            ModInfo[] mods = [new("a", SemVersion.Parse("1.0.0"), "TestMod.dll", "TestMod.Entry", [])];
            while (!stop.IsCancellationRequested)
            {
                accepted.Clear();
                listener.DrainAccepted(accepted);
                foreach (var connection in accepted)
                {
                    var serverConnection = new TcpServerConnection(connection);
                    sessions.Add(new ServerEngine.DedicatedSession(connection, new RemoteSession(serverConnection,
                        serverConnection.PrepareAsync(manifest, mods, names), names)));
                }
                ServerEngine.RunDedicatedPlayRound(game, players, sessions, paused: false, shutdown: false, ref tick);
                ServerEngine.RemoveClosedDedicatedSessions(game, players, listener, sessions);
                Thread.Sleep(10);
            }
            listener.StopAccepting();
            foreach (var entry in sessions)
            {
                entry.Connection.Close("The test host stopped.");
                entry.Session.Preparation.GetAwaiter().GetResult();
            }
            ServerEngine.RemoveClosedDedicatedSessions(game, players, listener, sessions);
            game.Destroy();
        });
        TcpClientConnection? first = null;
        TcpClientConnection? second = null;
        var firstWorld = new ClientWorld();
        var secondWorld = new ClientWorld();
        try
        {
            first = TcpClientConnection.Connect(endpoint, "Alice", TestRegistryBootstrap.Manager, [new("a", "1.0.0")]);
            var initial = ClientGameTests.LoadInitialWorld(first, firstWorld);
            second = TcpClientConnection.Connect(endpoint, "Bob", TestRegistryBootstrap.Manager, [new("a", "1.0.0")]);
            var other = ClientGameTests.LoadInitialWorld(second, secondWorld);
            Assert.NotEqual(initial.PlayerNetworkId, other.PlayerNetworkId);
            Assert.Equal(2, secondWorld.Players.Count);
            Assert.Equal(firstWorld.Chunks.Count, secondWorld.Chunks.Count);
            Assert.True(first.TrySend(new BlockInteractionPacket(BlockOperation.Break, -90, 0)));
            WorldUpdatePacket update;
            do { update = Assert.IsType<WorldUpdatePacket>(first.WaitForPacket(TimeSpan.FromSeconds(5))); }
            while (update.Results.Count == 0);
            Assert.True(Assert.Single(update.Results).IsSuccess);
            WorldUpdatePacket otherUpdate;
            do { otherUpdate = Assert.IsType<WorldUpdatePacket>(second.WaitForPacket(TimeSpan.FromSeconds(5))); }
            while (otherUpdate.Tick < update.Tick);
            Assert.Equal(update.Tick, otherUpdate.Tick);
            Assert.Equal(Assert.Single(update.Changes), Assert.Single(otherUpdate.Changes));
            Assert.Empty(otherUpdate.Results);
            Assert.Empty(Assert.IsType<WorldUpdatePacket>(first.WaitForPacket(TimeSpan.FromSeconds(5))).Results);

            first.TrySend(new InputStatePacket(1, 0, -90, 0));
            do { update = Assert.IsType<WorldUpdatePacket>(first.WaitForPacket(TimeSpan.FromSeconds(5))); }
            while (update.Players.Single(p => p.NetworkId == initial.PlayerNetworkId).Pose.Position == initial.Player.Position);
            Assert.Equal(other.Player.Position, update.Players.Single(p => p.NetworkId == other.PlayerNetworkId).Pose.Position);
            first.Close();
            first = null;
            do { update = Assert.IsType<WorldUpdatePacket>(second.WaitForPacket(TimeSpan.FromSeconds(5))); }
            while (update.Players.Count != 1);
            Assert.Equal(other.PlayerNetworkId, update.Players[0].NetworkId);
            using var invalid = new TcpClient();
            invalid.Connect(endpoint);
            TcpFraming.WriteAsync(invalid.GetStream(), PacketCodec.Encode(ConnectionPhase.Play, new InputStatePacket(1, 0, 0, 0)), stop.Token)
                .GetAwaiter().GetResult();
            var frame = TcpFraming.ReadAsync(invalid.GetStream(), stop.Token).GetAwaiter().GetResult();
            Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Handshake, frame!.Value.Span));
            using var mismatched = new TcpClient();
            mismatched.Connect(endpoint);
            Assert.IsType<LoginSuccessPacket>(Login(mismatched, "Charlie", stop.Token));
            var version = "1.0.0+" + new string('a', NetworkLimits.MaxFrameBytes - 13);
            var clientMods = PacketCodec.Encode(ConnectionPhase.Configuration, new ClientModsPacket([new("a", version)]));
            Assert.Equal(NetworkLimits.MaxFrameBytes, clientMods.Length);
            TcpFraming.WriteAsync(mismatched.GetStream(), clientMods, stop.Token).GetAwaiter().GetResult();
            frame = TcpFraming.ReadAsync(mismatched.GetStream(), stop.Token).GetAwaiter().GetResult();
            Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Configuration, frame!.Value.Span));

            var inputAfterRejection = new InputStatePacket(0, 0, 42, 12);
            Assert.True(second.TrySend(inputAfterRejection));
            PlayerState remaining;
            do
            {
                stop.Token.ThrowIfCancellationRequested();
                update = Assert.IsType<WorldUpdatePacket>(second.WaitForPacket(TimeSpan.FromSeconds(5)));
                remaining = Assert.Single(update.Players);
                Assert.Equal(other.PlayerNetworkId, remaining.NetworkId);
            } while (remaining.Pose.Yaw != inputAfterRejection.Yaw);
            Assert.Equal(inputAfterRejection.Pitch, remaining.Pose.Pitch);

            using var reconnected = new TcpClient();
            reconnected.ConnectAsync(endpoint, stop.Token).AsTask().GetAwaiter().GetResult();
            Assert.IsType<LoginSuccessPacket>(Login(reconnected, "Alice", stop.Token));
            TcpFraming.WriteAsync(reconnected.GetStream(),
                PacketCodec.Encode(ConnectionPhase.Configuration, new ClientModsPacket([new("a", "1.0.0")])), stop.Token)
                .GetAwaiter().GetResult();
            frame = TcpFraming.ReadAsync(reconnected.GetStream(), stop.Token).GetAwaiter().GetResult();
            Assert.IsType<ContentManifestPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Configuration, frame!.Value.Span));
            TcpFraming.WriteAsync(reconnected.GetStream(),
                PacketCodec.Encode(ConnectionPhase.Configuration, new ContentReadyPacket()), stop.Token).GetAwaiter().GetResult();
            frame = TcpFraming.ReadAsync(reconnected.GetStream(), stop.Token).GetAwaiter().GetResult();
            var rejoined = Assert.IsType<InitialWorldStatePacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, frame!.Value.Span));
            Assert.NotEqual(initial.PlayerNetworkId, rejoined.PlayerNetworkId);
        }
        finally
        {
            first?.Close();
            second?.Close();
            stop.Cancel();
            host.GetAwaiter().GetResult();
            firstWorld.Destroy();
            secondWorld.Destroy();
        }
    }

    private static ClientboundPacket Login(TcpClient client, string username, CancellationToken cancellationToken)
    {
        TcpFraming.WriteAsync(client.GetStream(), PacketCodec.Encode(ConnectionPhase.Handshake, new ClientHelloPacket(PacketCodec.ProtocolVersion)), cancellationToken)
            .GetAwaiter().GetResult();
        var frame = TcpFraming.ReadAsync(client.GetStream(), cancellationToken).GetAwaiter().GetResult();
        Assert.IsType<LoginStartPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Handshake, frame!.Value.Span));
        TcpFraming.WriteAsync(client.GetStream(), PacketCodec.Encode(ConnectionPhase.Login, new LoginRequestPacket(username)), cancellationToken)
            .GetAwaiter().GetResult();
        frame = TcpFraming.ReadAsync(client.GetStream(), cancellationToken).GetAwaiter().GetResult();
        return PacketCodec.ReadClientbound(ConnectionPhase.Login, frame!.Value.Span);
    }

    private sealed class RejectingInitialWorldConnection(Type rejectedType) : IServerConnection
    {
        private bool _sendRejected;

        public bool IsClosed { get; set; }
        public int IncomingCount => 1;
        internal int ReceiveAttempts { get; private set; }
        internal Type? LastSendAttempt { get; private set; }

        public bool TryReceive([NotNullWhen(true)] out ServerboundPacket? packet)
        {
            ReceiveAttempts++;
            packet = new InputStatePacket(1, 0, 0, 0);
            return true;
        }

        public bool TrySend(ClientboundPacket packet)
        {
            Assert.False(_sendRejected, "No packet should be sent after an initial world packet is rejected.");
            LastSendAttempt = packet.GetType();
            _sendRejected = LastSendAttempt == rejectedType;
            return !_sendRejected;
        }

        public void Disconnect(string reason) => IsClosed = true;
        public void ThrowIfFaulted() { }
    }

    private sealed class SessionPeer : IDisposable
    {
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(5));
        internal SessionPeer(TcpListener listener, PlayerNames playerNames)
        {
            Client = new TcpClient();
            Client.Connect((IPEndPoint)listener.LocalEndpoint);
            Connection = new TcpConnection(listener.AcceptTcpClient());
            var endpoint = new TcpServerConnection(Connection);
            var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
            Session = new RemoteSession(endpoint, endpoint.PrepareAsync(manifest, [], playerNames), playerNames);
        }

        internal TcpClient Client { get; }
        internal TcpConnection Connection { get; }
        internal RemoteSession Session { get; }
        internal CancellationToken Token => _stop.Token;

        internal void Send(ConnectionPhase phase, ServerboundPacket packet) =>
            TcpFraming.WriteAsync(Client.GetStream(), PacketCodec.Encode(phase, packet), Token).GetAwaiter().GetResult();

        internal ClientboundPacket Read(ConnectionPhase phase)
        {
            var frame = TcpFraming.ReadAsync(Client.GetStream(), Token).GetAwaiter().GetResult();
            return PacketCodec.ReadClientbound(phase, frame!.Value.Span);
        }

        public void Dispose()
        {
            Connection.Close("The test peer stopped.");
            Session.Preparation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Session.ReleaseNameIfClosed();
            Connection.Destroy();
            Client.Dispose();
            _stop.Dispose();
        }
    }
}
