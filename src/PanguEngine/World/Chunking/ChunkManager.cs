using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>
/// Manages the chunks that make up a local world.
/// </summary>
public sealed class ChunkManager
{
    private readonly Dictionary<ChunkPos, Chunk> _chunks = [];

    /// <summary>
    /// Gets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>The stored block state, or air if the chunk has not been created.</returns>
    public BlockState GetBlock(BlockPos position)
    {
        var chunkPos = position.ToChunkPos();
        return _chunks.TryGetValue(chunkPos, out var chunk)
            ? chunk.GetBlock(position)
            : BuiltinBlocks.Air.DefaultState;
    }

    /// <summary>
    /// Sets a block state by world block position, creating the target chunk when needed.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <param name="state">The block state to store.</param>
    public void SetBlock(BlockPos position, BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var chunkPos = position.ToChunkPos();
        if (!_chunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new Chunk(chunkPos);
            _chunks.Add(chunkPos, chunk);
        }

        chunk.SetBlock(position, state);
    }

    /// <summary>
    /// Enumerates the chunks that have been created.
    /// </summary>
    /// <returns>The created chunks.</returns>
    public IEnumerable<Chunk> EnumerateChunks()
    {
        return _chunks.Values;
    }
}