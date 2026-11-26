using PanguEngine.Network.Serialization;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record ChunkDataPacket(ChunkPos Position, BlockStateSnapshot Blocks) : ClientboundPacket
{
    public override ClientboundPacketType Type => BuiltinPackets.ChunkData;

    internal BlockState GetBlock(int index) => Blocks.Get(index);

    internal static ChunkDataPacket Capture(Chunk chunk)
    {
        return new ChunkDataPacket(chunk.Position, chunk.CaptureBlocks());
    }

    internal override void Write(ref Writer writer)
    {
        writer.WriteInt32(Position.X);
        writer.WriteInt32(Position.Y);
        writer.WriteInt32(Position.Z);
        writer.WriteByte((byte)Blocks.Bits);
        if (Blocks.Bits == 0)
        {
            PacketData.WriteBlock(ref writer, Blocks.SingleState!);
            return;
        }
        if (Blocks.Bits <= 8)
        {
            writer.WriteCount(Blocks.EntryCount);
            for (var i = 0; i < Blocks.EntryCount; i++)
                PacketData.WriteBlock(ref writer, Blocks.Entries[i]);
        }
        for (var i = 0; i < Blocks.WordCount; i++)
            writer.WriteUInt64(Blocks.Words[i]);
    }

    internal static ChunkDataPacket Read(ref Reader reader)
    {
        var position = new ChunkPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        var bits = reader.ReadByte();
        if (bits == 0)
            return new ChunkDataPacket(position, new BlockStateSnapshot(0, PacketData.ReadBlock(ref reader), [], 0, []));
        if (bits < 4 || (bits > 8 && bits != BlockStateIds.Bits))
            throw new InvalidDataException("Invalid chunk state bit width.");

        BlockState[] palette = [];
        if (bits <= 8)
        {
            var count = reader.ReadCount(1 << bits);
            if (count == 0)
                throw new InvalidDataException("A local chunk palette must contain a state.");
            palette = new BlockState[count];
            var states = new HashSet<BlockState>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < count; i++)
            {
                palette[i] = PacketData.ReadBlock(ref reader);
                if (!states.Add(palette[i]))
                    throw new InvalidDataException("Duplicate chunk palette state.");
            }
        }

        var wordCount = PackedBlockStates.WordCount(bits);
        if (reader.RemainingLength < wordCount * sizeof(ulong))
            throw new EndOfStreamException("The chunk block data is incomplete.");
        var words = new ulong[wordCount];
        for (var i = 0; i < words.Length; i++)
            words[i] = reader.ReadUInt64();
        for (var i = 0; i < Chunk.Volume; i++)
        {
            var value = PackedBlockStates.Get(words, bits, i);
            if (bits <= 8)
            {
                if (value >= palette.Length)
                    throw new InvalidDataException("Unknown chunk palette index.");
            }
            else
            {
                _ = BlockStateIds.Get(value);
            }
        }
        return new ChunkDataPacket(position, new BlockStateSnapshot(bits, null, palette, palette.Length, words));
    }
}
