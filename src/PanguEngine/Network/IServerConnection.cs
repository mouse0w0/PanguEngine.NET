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

    /// <summary>Ends sending without discarding packets already queued for the client.</summary>
    void CompleteSending(string reason);

    void ThrowIfFaulted();
}
