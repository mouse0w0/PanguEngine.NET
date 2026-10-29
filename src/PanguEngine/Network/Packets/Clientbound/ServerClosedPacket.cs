namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record ServerClosedPacket(long Tick) : ClientboundPacket;
