using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record ContentReadyPacket : ServerboundPacket
{
    internal override void Write(ref Writer writer) { }
    internal static ContentReadyPacket Read(ref Reader reader) => new();
}
