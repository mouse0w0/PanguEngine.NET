using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Network;

/// <summary>Connects a client to a remote server and exchanges typed packets over TCP.</summary>
internal sealed class TcpClientConnection(TcpConnection connection) : IClientConnection
{
    private ConnectionPhase _phase = ConnectionPhase.Handshake;

    public bool IsClosed => connection.IsClosed;

    public bool TrySend(ServerboundPacket packet) => connection.TrySend(PacketCodec.Encode(_phase, packet));

    public bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet)
    {
        packet = null;
        if (!connection.TryPeek(out var frame))
            return false;
        try { packet = PacketCodec.ReadClientbound(_phase, frame.Span); }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException or DecoderFallbackException)
        {
            connection.Close(error.Message);
            throw new ConnectionClosedException(error.Message);
        }
        connection.Consume();
        return true;
    }

    public ClientboundPacket WaitForPacket(TimeSpan? timeout = null)
    {
        var start = Stopwatch.GetTimestamp();
        var limit = timeout ?? NetworkLimits.FrameTimeout;
        while (true)
        {
            if (TryReceive(out var packet))
                return packet;
            var remaining = limit - Stopwatch.GetElapsedTime(start);
            if (remaining <= TimeSpan.Zero)
                throw new ConnectionClosedException("Timed out waiting for server data.");
            connection.WaitForFrame(remaining);
        }
    }

    public void Close() => connection.Destroy();

    internal static TcpClientConnection Connect(IPEndPoint endpoint, string username, RegistryManager registries,
        IReadOnlyList<ModVersionEntry> mods)
    {
        var client = new TcpClient(endpoint.AddressFamily);
        ConnectAsync(client, endpoint).GetAwaiter().GetResult();
        var transport = new TcpConnection(client);
        var result = new TcpClientConnection(transport);
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (!result.TrySend(new ClientHelloPacket(PacketCodec.ProtocolVersion)))
                throw new ConnectionClosedException("The server connection ended during the handshake.");
            result.WaitExpected<LoginStartPacket>(started);
            result._phase = ConnectionPhase.Login;
            if (!result.TrySend(new LoginRequestPacket(username)))
                throw new ConnectionClosedException("The server connection ended during login.");
            result.WaitExpected<LoginSuccessPacket>(started);
            result._phase = ConnectionPhase.Configuration;
            if (!result.TrySend(new ClientModsPacket(mods)))
                throw new ConnectionClosedException("The server connection ended during configuration.");
            var manifest = result.WaitExpected<ContentManifestPacket>(started);
            registries.BindIdTables(manifest.Mappings);
            BlockStateIds.Build();
            if (Stopwatch.GetElapsedTime(started) >= NetworkLimits.HandshakeTimeout)
                throw new ConnectionClosedException("Connection setup timed out.");
            if (!result.TrySend(new ContentReadyPacket()))
                throw new ConnectionClosedException("The server connection ended during content confirmation.");
            result.WaitExpected<PlayStartPacket>(started);
            result._phase = ConnectionPhase.Play;
            transport.EnableHeartbeat(PacketCodec.Encode(ConnectionPhase.Play, new ServerboundHeartbeatPacket()));
            return result;
        }
        catch (Exception error) when (error is ConnectionClosedException or InvalidDataException or EncoderFallbackException)
        {
            result.Close();
            throw new ConnectionClosedException(error.Message);
        }
    }

    private T WaitExpected<T>(long started) where T : ClientboundPacket
    {
        var remaining = NetworkLimits.HandshakeTimeout - Stopwatch.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
            throw new ConnectionClosedException("Connection setup timed out.");
        var packet = WaitForPacket(remaining);
        if (packet is DisconnectPacket disconnected)
            throw new ConnectionClosedException(disconnected.Reason);
        return packet as T ?? throw new ConnectionClosedException("Unexpected message during connection setup.");
    }

    private static async Task ConnectAsync(TcpClient client, IPEndPoint endpoint)
    {
        using var deadline = new CancellationTokenSource(NetworkLimits.ConnectTimeout);
        try { await client.ConnectAsync(endpoint, deadline.Token).ConfigureAwait(false); }
        catch (Exception error) when (error is SocketException or OperationCanceledException)
        {
            throw new ConnectionClosedException($"Could not connect to {endpoint}: {error.Message}");
        }
    }
}
