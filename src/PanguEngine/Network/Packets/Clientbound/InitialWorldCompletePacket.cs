using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record InitialWorldCompletePacket : ClientboundPacket
{
    internal override void Write(ref Writer writer) { }
    internal static InitialWorldCompletePacket Read(ref Reader reader) => new();
}
