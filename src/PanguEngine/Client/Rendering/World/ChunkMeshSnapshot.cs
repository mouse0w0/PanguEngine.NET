using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMeshSnapshot(
    ChunkPos position,
    ChunkMeshSnapshotBatch batch,
    BlockStateSnapshot[] chunks)
{
    internal ChunkPos Position { get; } = position;
    internal ChunkMeshSnapshotBatch Batch { get; } = batch;

    internal BlockState GetBlock(BlockPos localPosition)
    {
        var x = (localPosition.X >> Chunk.BitsX) + 1;
        var y = (localPosition.Y >> Chunk.BitsY) + 1;
        var z = (localPosition.Z >> Chunk.BitsZ) + 1;
        var index = (localPosition.X & Chunk.MaskX)
            | ((localPosition.Z & Chunk.MaskZ) << Chunk.BitsX)
            | ((localPosition.Y & Chunk.MaskY) << (Chunk.BitsX + Chunk.BitsZ));
        return chunks[x + 3 * (z + 3 * y)].Get(index);
    }
}
