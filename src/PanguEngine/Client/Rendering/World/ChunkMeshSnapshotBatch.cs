using PanguEngine.Client.World;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

/// <summary>Owns pooled chunk copies shared by one dispatch and all of its CPU readers.</summary>
internal sealed class ChunkMeshSnapshotBatch : IDisposable
{
    private readonly Dictionary<ChunkPos, BlockStateSnapshot> _chunks = [];
    private int _references = 1;

    internal ChunkMeshSnapshot Capture(ClientWorld world, ChunkPos position)
    {
        var chunks = new BlockStateSnapshot[27];
        var index = 0;
        for (var y = -1; y <= 1; y++)
        for (var z = -1; z <= 1; z++)
        for (var x = -1; x <= 1; x++)
        {
            var neighbor = position.Offset(x, y, z);
            if (!_chunks.TryGetValue(neighbor, out var snapshot))
            {
                snapshot = world.Chunks.TryGetChunk(neighbor, out var chunk)
                    ? chunk.CaptureBlocks(pooled: true)
                    : BlockStateSnapshot.Air;
                _chunks.Add(neighbor, snapshot);
            }
            chunks[index++] = snapshot;
        }
        return new ChunkMeshSnapshot(position, this, chunks);
    }

    internal void Retain() => Interlocked.Increment(ref _references);

    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
            return;
        foreach (var snapshot in _chunks.Values)
            snapshot.ReturnPooledArrays();
    }

    /// <summary>Ends the dispatcher's ownership after it has stopped adding tasks.</summary>
    public void Dispose() => Release();
}
