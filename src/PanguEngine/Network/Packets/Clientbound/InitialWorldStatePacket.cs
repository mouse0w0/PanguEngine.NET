using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record InitialWorldStatePacket : ClientboundPacket
{
    internal InitialWorldStatePacket(long tick, Guid playerId, PlayerPose player, double stepSeconds, IReadOnlyList<ChunkSnapshot> chunks)
    {
        Tick = tick;
        PlayerId = playerId;
        Player = player;
        StepSeconds = stepSeconds;
        Chunks = chunks;
    }

    internal long Tick { get; }
    internal Guid PlayerId { get; }
    internal PlayerPose Player { get; }
    internal double StepSeconds { get; }
    internal IReadOnlyList<ChunkSnapshot> Chunks { get; }

    internal sealed class ChunkSnapshot(ChunkPos position, IReadOnlyList<BlockState> blocks)
    {
        internal ChunkPos Position { get; } = position;

        internal BlockState GetBlock(int index) => blocks[index];

        internal static ChunkSnapshot Capture(Chunk chunk)
        {
            var blocks = new BlockState[Chunk.Volume];
            var index = 0;
            foreach (var entry in chunk.EnumerateBlocks())
                blocks[index++] = entry.State;
            return new ChunkSnapshot(chunk.Position, blocks);
        }
    }
}
