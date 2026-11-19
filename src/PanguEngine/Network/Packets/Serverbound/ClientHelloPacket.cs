using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record ClientHelloPacket(int ProtocolVersion) : ServerboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteCount(ProtocolVersion);
    }

    internal static ClientHelloPacket Read(ref Reader reader)
    {
        return new ClientHelloPacket(reader.ReadCount(int.MaxValue));
    }
}
