using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

/// <summary>Confirms that the server has accepted the client into the Play protocol phase.</summary>
internal sealed record PlayStartPacket : ClientboundPacket
{
    internal override void Write(ref Writer writer) { }

    internal static PlayStartPacket Read(ref Reader reader) => new();
}
