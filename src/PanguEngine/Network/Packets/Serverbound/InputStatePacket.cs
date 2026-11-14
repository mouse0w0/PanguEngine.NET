using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record InputStatePacket(double Forward, double Right, double Yaw, double Pitch) : ServerboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteDouble(Forward);
        writer.WriteDouble(Right);
        writer.WriteDouble(Yaw);
        writer.WriteDouble(Pitch);
    }

    internal static InputStatePacket Read(ref Reader reader) =>
        new(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
}
