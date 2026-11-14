using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record BlockInteractionPacket(BlockOperation Operation, double Yaw, double Pitch) : ServerboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteByte((byte)Operation);
        writer.WriteDouble(Yaw);
        writer.WriteDouble(Pitch);
    }

    internal static BlockInteractionPacket Read(ref Reader reader) =>
        new((BlockOperation)reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble());
}
