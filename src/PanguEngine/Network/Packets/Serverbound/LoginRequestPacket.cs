using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record LoginRequestPacket(string Username) : ServerboundPacket
{
    internal override void Write(ref Writer writer) => writer.WriteString(Username);

    internal static LoginRequestPacket Read(ref Reader reader) => new(reader.ReadString(32));
}
