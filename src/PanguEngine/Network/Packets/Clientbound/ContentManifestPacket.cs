using PanguEngine.Network.Serialization;
using PanguEngine.Registries;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record ContentManifestPacket(RegistryIdMappings Mappings) : ClientboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteCount(Mappings.Tables.Count);
        foreach (var table in Mappings.Tables)
        {
            writer.WriteString(table.RegistryKey.ToString());
            writer.WriteCount(table.Keys.Count);
            foreach (var key in table.Keys)
                writer.WriteString(key.ToString());
        }
    }

    internal static ContentManifestPacket Read(ref Reader reader)
    {
        var tables = new RegistryIdMapping[reader.ReadCount(reader.RemainingLength / 2)];
        for (var i = 0; i < tables.Length; i++)
        {
            var key = PacketData.ReadKey(ref reader);
            var keys = new ResourceKey[reader.ReadCount(reader.RemainingLength)];
            for (var j = 0; j < keys.Length; j++)
                keys[j] = PacketData.ReadKey(ref reader);
            tables[i] = new RegistryIdMapping(key, keys);
        }
        return new ContentManifestPacket(new RegistryIdMappings(tables));
    }
}
