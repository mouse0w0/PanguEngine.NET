using PanguEngine.Client.World;
using PanguEngine.Network.Packets;

namespace PanguEngine.Client.Game;

/// <summary>Provides the client state available when handling a Play packet.</summary>
internal readonly record struct ClientPacketContext(ClientWorld World, ClientSession? Session = null,
    ClientGame? Game = null, double Now = 0)
{
    internal void Handle(ClientboundPacket packet) => packet.Type.Handle(this, packet);
}
