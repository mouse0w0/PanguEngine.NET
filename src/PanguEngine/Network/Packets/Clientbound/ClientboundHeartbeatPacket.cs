namespace PanguEngine.Network.Packets.Clientbound;

/// <summary>Keeps an idle TCP connection active from the server side during Play.</summary>
internal sealed record ClientboundHeartbeatPacket : ClientboundPacket
{
    public override ClientboundPacketType Type => BuiltinPackets.ClientboundHeartbeat;
}
