using PanguEngine.Network.Serialization;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record ChunkDataPacket(ChunkPos Position, IReadOnlyList<BlockState> Blocks) : ClientboundPacket
{
    internal BlockState GetBlock(int index) => Blocks[index];

    internal static ChunkDataPacket Capture(Chunk chunk)
    {
        var blocks = new BlockState[Chunk.Volume];
        var index = 0;
        foreach (var entry in chunk.EnumerateBlocks())
            blocks[index++] = entry.State;
        return new ChunkDataPacket(chunk.Position, blocks);
    }

    internal override void Write(ref Writer writer)
    {
        writer.WriteInt32(Position.X);
        writer.WriteInt32(Position.Y);
        writer.WriteInt32(Position.Z);
        for (var i = 0; i < Chunk.Volume; i++)
            PacketData.WriteBlock(ref writer, Blocks[i]);
    }

    internal static ChunkDataPacket Read(ref Reader reader)
    {
        var position = new ChunkPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        if (reader.RemainingLength < Chunk.Volume)
            throw new EndOfStreamException("The chunk block data is incomplete.");
        var blocks = new BlockState[Chunk.Volume];
        for (var i = 0; i < blocks.Length; i++)
            blocks[i] = PacketData.ReadBlock(ref reader);
        return new ChunkDataPacket(position, blocks);
    }
}
