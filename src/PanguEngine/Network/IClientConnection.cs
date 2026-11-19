using System.Diagnostics.CodeAnalysis;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

internal interface IClientConnection
{
    /// <summary>Whether the connection has ended, even if packets remain to be read.</summary>
    bool IsClosed { get; }
    bool TrySend(ServerboundPacket packet);

    /// <summary>Reads the next queued packet, including disconnect notices, or returns false while open with no packets.</summary>
    /// <exception cref="ConnectionClosedException">The connection has ended and no packets remain.</exception>
    bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet);

    /// <summary>Waits for the next queued packet, including a disconnect notice.</summary>
    /// <exception cref="ConnectionClosedException">The connection has ended with no packets remaining, or the wait timed out.</exception>
    ClientboundPacket WaitForPacket(TimeSpan? timeout = null);
    void Close();
}
