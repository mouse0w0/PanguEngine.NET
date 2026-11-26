using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record DisconnectPacket(string Reason) : ClientboundPacket
{
    public override ClientboundPacketType Type => BuiltinPackets.Disconnect;

    internal static DisconnectPacket FromReason(string reason) => new(reason.Length > 512
        ? "The connection was closed; the diagnostic message exceeded its length limit."
        : reason);

    internal override void Write(ref Writer writer) => writer.WriteString(Reason);
    internal static DisconnectPacket Read(ref Reader reader) => new(PacketData.ReadString(ref reader));
}
