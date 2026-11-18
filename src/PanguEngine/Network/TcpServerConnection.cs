using System.Diagnostics.CodeAnalysis;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

/// <summary>Adapts a server-side TCP transport to the typed packet endpoint.</summary>
internal sealed class TcpServerConnection(TcpConnection connection) : IServerConnection
{
    internal long ConnectedAt => connection.ConnectedAt;

    internal void EnableHeartbeat() => connection.EnableHeartbeat();

    public bool IsClosed => connection.IsClosed;
    public int IncomingCount => connection.IncomingCount;

    public bool TryReceive([NotNullWhen(true)] out ServerboundPacket? packet)
    {
        if (!connection.TryPeek(out var frame))
        {
            packet = null;
            return false;
        }
        packet = PacketCodec.ReadServerbound(frame.Span);
        connection.Consume();
        return true;
    }

    public bool TrySend(ClientboundPacket packet) => connection.TrySend(PacketCodec.Encode(packet));

    public void CompleteSending(string reason) => connection.CompleteSending(reason);

    public void ThrowIfFaulted() => connection.ThrowIfFaulted();
}
