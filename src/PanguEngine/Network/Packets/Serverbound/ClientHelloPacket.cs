using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record ClientHelloPacket(int ProtocolVersion, IReadOnlyList<ModVersionEntry> Mods) : ServerboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteCount(ProtocolVersion);
        writer.WriteCount(Mods.Count);
        foreach (var mod in Mods)
        {
            writer.WriteString(mod.Id);
            writer.WriteString(mod.Version);
        }
    }

    internal static ClientHelloPacket Read(ref Reader reader)
    {
        var version = reader.ReadCount(int.MaxValue);
        var count = reader.ReadCount(reader.RemainingLength / 2);
        var mods = new ModVersionEntry[count];
        for (var i = 0; i < count; i++)
            mods[i] = new ModVersionEntry(PacketData.ReadString(ref reader), PacketData.ReadString(ref reader));
        return new ClientHelloPacket(version, mods);
    }
}
