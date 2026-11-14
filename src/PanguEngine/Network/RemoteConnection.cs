using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Registries;

namespace PanguEngine.Network;

internal sealed class RemoteConnection(TcpConnection connection) : IClientConnection
{
    internal static RemoteConnection Connect(IPEndPoint endpoint, RegistryManager registries,
        IReadOnlyList<ModVersionEntry> mods)
    {
        var client = new TcpClient(endpoint.AddressFamily);
        ConnectAsync(client, endpoint).GetAwaiter().GetResult();
        var result = new RemoteConnection(new TcpConnection(client));
        var started = Stopwatch.GetTimestamp();
        result.TrySend(new ClientHelloPacket(PacketCodec.ProtocolVersion, mods));
        var packet = result.WaitForPacket(NetworkLimits.HandshakeTimeout - Stopwatch.GetElapsedTime(started));
        if (packet is not ContentManifestPacket manifest)
            throw new ConnectionClosedException("Expected the server content manifest.");
        try
        {
            registries.BindIdTables(manifest.Mappings);
            BlockStateIds.Build();
        }
        catch (Exception error) when (error is InvalidDataException or KeyNotFoundException or InvalidOperationException)
        {
            result.Close();
            throw new ConnectionClosedException(error.Message);
        }
        if (Stopwatch.GetElapsedTime(started) >= NetworkLimits.HandshakeTimeout)
            throw new ConnectionClosedException("Content handshake timed out.");
        if (!result.TrySend(new ContentReadyPacket()))
            throw new ConnectionClosedException("The server connection ended during content confirmation.");
        result.EnableHeartbeat();
        return result;
    }

    private void EnableHeartbeat() => connection.EnableHeartbeat();

    public (bool IsAccepting, long Epoch) RequestState => (!connection.IsClosed, 0);

    public bool TrySend(ServerboundPacket packet) => connection.TrySend(PacketCodec.Encode(packet));

    public bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet)
    {
        packet = null;
        if (!connection.TryPeek(out var frame))
            return false;
        try { packet = PacketCodec.ReadClientbound(frame.Span); }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException or DecoderFallbackException)
        {
            connection.Close(error.Message);
            throw new ConnectionClosedException(error.Message);
        }
        connection.Consume();
        if (packet is DisconnectPacket disconnected)
            throw new ConnectionClosedException(disconnected.Reason);
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
