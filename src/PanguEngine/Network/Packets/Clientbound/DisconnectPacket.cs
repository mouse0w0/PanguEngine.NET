using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record DisconnectPacket(string Reason) : ClientboundPacket
{
    internal override void Write(ref Writer writer) => writer.WriteString(Reason);
    internal static DisconnectPacket Read(ref Reader reader) => new(PacketData.ReadString(ref reader));
}
