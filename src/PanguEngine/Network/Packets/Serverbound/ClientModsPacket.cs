using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record ClientModsPacket(IReadOnlyList<ModVersionEntry> Mods) : ServerboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteCount(Mods.Count);
        foreach (var mod in Mods)
        {
            writer.WriteString(mod.Id);
            writer.WriteString(mod.Version);
        }
    }

    internal static ClientModsPacket Read(ref Reader reader)
    {
        var count = reader.ReadCount(reader.RemainingLength / 2);
        var mods = new ModVersionEntry[count];
        for (var i = 0; i < mods.Length; i++)
            mods[i] = new ModVersionEntry(PacketData.ReadString(ref reader), PacketData.ReadString(ref reader));
        return new ClientModsPacket(mods);
    }
}
