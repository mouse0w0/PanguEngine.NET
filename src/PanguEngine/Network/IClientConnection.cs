using System.Diagnostics.CodeAnalysis;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

internal interface IClientConnection
{
    /// <summary>Whether the connection has ended, even if packets remain to be read.</summary>
    bool IsClosed { get; }
    bool TrySend(ServerboundPacket packet);
    bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet);
    ClientboundPacket WaitForPacket(TimeSpan? timeout = null);
    void Close();
}
