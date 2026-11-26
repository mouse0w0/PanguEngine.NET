namespace PanguEngine.Network.Packets.Serverbound;

/// <summary>Keeps an idle TCP connection active from the client side during Play.</summary>
internal sealed record ServerboundHeartbeatPacket : ServerboundPacket
{
    public override ServerboundPacketType Type => BuiltinPackets.ServerboundHeartbeat;
}
