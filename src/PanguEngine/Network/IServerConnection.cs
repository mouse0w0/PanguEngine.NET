using System.Diagnostics.CodeAnalysis;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

/// <summary>The server endpoint of a connection carrying typed game packets.</summary>
internal interface IServerConnection
{
    bool IsClosed { get; }
    int IncomingCount { get; }
    bool TryReceive([NotNullWhen(true)] out ServerboundPacket? packet);
    bool TrySend(ClientboundPacket packet);

    /// <summary>Queues the disconnect notice and ends sending without discarding earlier packets.</summary>
    void Disconnect(string reason);

    void ThrowIfFaulted();
}
