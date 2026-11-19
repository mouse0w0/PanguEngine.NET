using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record LoginSuccessPacket : ClientboundPacket
{
    internal override void Write(ref Writer writer) { }

    internal static LoginSuccessPacket Read(ref Reader reader) => new();
}
