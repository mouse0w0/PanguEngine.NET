using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record LoginStartPacket : ClientboundPacket
{
    internal override void Write(ref Writer writer) { }

    internal static LoginStartPacket Read(ref Reader reader) => new();
}
