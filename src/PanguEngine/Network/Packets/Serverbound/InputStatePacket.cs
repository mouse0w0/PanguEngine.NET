using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record InputStatePacket(double Forward, double Right, double Yaw, double Pitch) : ServerboundPacket
{
    public override ServerboundPacketType Type => BuiltinPackets.InputState;

    internal override void Write(ref Writer writer)
    {
        writer.WriteDouble(Forward);
        writer.WriteDouble(Right);
        writer.WriteDouble(Yaw);
        writer.WriteDouble(Pitch);
    }

    internal static InputStatePacket Read(ref Reader reader)
    {
        var packet = new InputStatePacket(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        if (!double.IsFinite(packet.Forward) || packet.Forward is < -1 or > 1 ||
            !double.IsFinite(packet.Right) || packet.Right is < -1 or > 1)
            throw new InvalidDataException("Invalid movement axes.");
        ValidateAngles(packet.Yaw, packet.Pitch);
        return packet;
    }

    internal static void ValidateAngles(double yaw, double pitch)
    {
        if (!double.IsFinite(yaw) || !double.IsFinite(pitch) || pitch is < -89 or > 89)
            throw new InvalidDataException("Invalid player angles.");
    }
}
