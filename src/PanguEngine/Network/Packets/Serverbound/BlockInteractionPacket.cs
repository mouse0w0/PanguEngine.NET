using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record BlockInteractionPacket(BlockOperation Operation, double Yaw, double Pitch) : ServerboundPacket
{
    public override ServerboundPacketType Type => BuiltinPackets.BlockInteraction;

    internal override void Write(ref Writer writer)
    {
        writer.WriteByte((byte)Operation);
        writer.WriteDouble(Yaw);
        writer.WriteDouble(Pitch);
    }

    internal static BlockInteractionPacket Read(ref Reader reader)
    {
        var packet = new BlockInteractionPacket((BlockOperation)reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble());
        if (packet.Operation is not (BlockOperation.Break or BlockOperation.Place))
            throw new InvalidDataException("Invalid block operation.");
        InputStatePacket.ValidateAngles(packet.Yaw, packet.Pitch);
        return packet;
    }
}
