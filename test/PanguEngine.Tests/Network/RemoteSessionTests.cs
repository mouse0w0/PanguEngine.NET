using System.Net;
using System.Net.Sockets;
using PanguEngine.Client.World;
using PanguEngine.Network;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server.Game;
using PanguEngine.Tests.Client.Game;
using PanguEngine.Modding;
using PanguEngine.Versioning;

namespace PanguEngine.Tests.Network;

[Collection("Network globals")]
public sealed class RemoteSessionTests
{
    [Theory]
    [InlineData("1.2.3+build1", false, true)]
    [InlineData("1.2.3+build2", false, false)]
    [InlineData(null, false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("1.2.3+build1", true, false)]
    public void HelloChecksServerFullVersionsAndAllowsExtraClientMods(
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
            var game = new ServerGame();
            try
            {
                ModInfo[] mods =
                [
                    new("required", SemVersion.Parse("1.2.3+build1"), "TestMod.dll", "TestMod.Entry", [])
                ];
                var session = new RemoteSession(connection,
                    new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings()), mods);
                var clientMods = new List<ModVersionEntry> { new("extra", "9.0.0") };
                if (requiredVersion is not null)
                {
                    clientMods.Add(new ModVersionEntry("required", requiredVersion));
                    if (duplicate)
                        clientMods.Add(new ModVersionEntry("required", requiredVersion));
                }
                var hello = new ClientHelloPacket(PacketCodec.ProtocolVersion, clientMods);
                TcpFraming.WriteAsync(client.GetStream(), PacketCodec.Encode(hello), deadline.Token).GetAwaiter().GetResult();
                connection.WaitForFrame(TimeSpan.FromSeconds(5));
                session.Receive(game);
                var frame = TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult();
                var response = PacketCodec.ReadClientbound(frame!.Value.Span);

                if (acceptedHello)
                    Assert.IsType<ContentManifestPacket>(response);
                else
                    Assert.NotEmpty(Assert.IsType<DisconnectPacket>(response).Reason);
            }
            finally
            {
                connection.Destroy();
                game.Destroy();
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
            try
            {
                var session = new RemoteSession(connection,
                    new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings()), []);
                var reason = "The server stopped before the handshake.";
                session.Disconnect(reason);
                session.Disconnect("A later close reason.");

                var frame = TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult();
                var packet = Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(frame!.Value.Span));
                Assert.Equal(reason, packet.Reason);
                Assert.Null(TcpFraming.ReadAsync(client.GetStream(), deadline.Token).GetAwaiter().GetResult());
            }
            finally { connection.Destroy(); }
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
            var sessions = new List<RemoteSession>();
            var accepted = new List<TcpConnection>();
            var manifest = new ContentManifestPacket(TestRegistryBootstrap.Manager.ExportIdMappings());
            ModInfo[] mods = [new("a", SemVersion.Parse("1.0.0"), "TestMod.dll", "TestMod.Entry", [])];
            while (!stop.IsCancellationRequested)
            {
                accepted.Clear();
                listener.DrainAccepted(accepted);
                sessions.AddRange(accepted.Select(connection => new RemoteSession(connection, manifest, mods)));
                game.BeginTick();
                var batches = sessions.Select(session => (Session: session, Packets: session.Receive(game))).ToArray();
                foreach (var batch in batches)
                    batch.Session.Apply(game, batch.Packets);
                var update = game.FinishTick();
                foreach (var session in sessions)
                    session.Publish(update);
                foreach (var session in sessions.Where(session => session.Connection.IsClosed).ToArray())
                {
                    session.Leave(game);
                    listener.Remove(session.Connection);
                    sessions.Remove(session);
                }
                Thread.Sleep(10);
            }
            listener.StopAccepting();
            foreach (var session in sessions)
                listener.Remove(session.Connection);
            game.Destroy();
        });
        RemoteConnection? first = null;
        RemoteConnection? second = null;
        var firstWorld = new ClientWorld();
        var secondWorld = new ClientWorld();
        try
        {
            first = RemoteConnection.Connect(endpoint, TestRegistryBootstrap.Manager, [new("a", "1.0.0")]);
            var initial = ClientGameTests.LoadInitialWorld(first, firstWorld);
            second = RemoteConnection.Connect(endpoint, TestRegistryBootstrap.Manager, [new("a", "1.0.0")]);
            var other = ClientGameTests.LoadInitialWorld(second, secondWorld);
            Assert.NotEqual(initial.PlayerNetworkId, other.PlayerNetworkId);
            Assert.Equal(2, secondWorld.Players.Count);
            Assert.Equal(firstWorld.Chunks.Count, secondWorld.Chunks.Count);
            first.TrySend(new InputStatePacket(1, 0, -90, 0));
            WorldUpdatePacket update;
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
            TcpFraming.WriteAsync(invalid.GetStream(), PacketCodec.Encode(new InputStatePacket(1, 0, 0, 0)), stop.Token)
                .GetAwaiter().GetResult();
            var frame = TcpFraming.ReadAsync(invalid.GetStream(), stop.Token).GetAwaiter().GetResult();
            Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(frame!.Value.Span));
            using var mismatched = new TcpClient();
            mismatched.Connect(endpoint);
            var version = "1.0.0+" + new string('a', NetworkLimits.MaxFrameBytes - 14);
            var hello = PacketCodec.Encode(new ClientHelloPacket(PacketCodec.ProtocolVersion, [new("a", version)]));
            Assert.Equal(NetworkLimits.MaxFrameBytes, hello.Length);
            TcpFraming.WriteAsync(mismatched.GetStream(), hello, stop.Token).GetAwaiter().GetResult();
            frame = TcpFraming.ReadAsync(mismatched.GetStream(), stop.Token).GetAwaiter().GetResult();
            Assert.IsType<DisconnectPacket>(PacketCodec.ReadClientbound(frame!.Value.Span));
            update = Assert.IsType<WorldUpdatePacket>(second.WaitForPacket(TimeSpan.FromSeconds(5)));
            Assert.Equal(other.PlayerNetworkId, Assert.Single(update.Players).NetworkId);
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
}
