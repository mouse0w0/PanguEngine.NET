using System.Diagnostics.CodeAnalysis;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

internal interface IClientConnection
{
    (bool IsAccepting, long Epoch) RequestState { get; }
    bool TrySend(ServerboundPacket packet);
    bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet);
    ClientboundPacket WaitForPacket(TimeSpan? timeout = null);
    void Close();
}
