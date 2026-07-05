using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>
/// Stores block states for a fixed-size chunk.
/// </summary>
public sealed class Chunk
{
    /// <summary>The bit shift used to convert X block coordinates to chunk coordinates.</summary>
    public const int BitsX = 4;

    /// <summary>The bit shift used to convert Y block coordinates to chunk coordinates.</summary>
    public const int BitsY = 4;

    /// <summary>The bit shift used to convert Z block coordinates to chunk coordinates.</summary>
    public const int BitsZ = 4;

    /// <summary>The number of blocks along the chunk X axis.</summary>
    public const int SizeX = 1 << BitsX;

    /// <summary>The number of blocks along the chunk Y axis.</summary>
    public const int SizeY = 1 << BitsY;

    /// <summary>The number of blocks along the chunk Z axis.</summary>
    public const int SizeZ = 1 << BitsZ;

    /// <summary>The mask used to convert X block coordinates to local chunk coordinates.</summary>
    public const int MaskX = SizeX - 1;

    /// <summary>The mask used to convert Y block coordinates to local chunk coordinates.</summary>
    public const int MaskY = SizeY - 1;

    /// <summary>The mask used to convert Z block coordinates to local chunk coordinates.</summary>
    public const int MaskZ = SizeZ - 1;

    /// <summary>The number of block states stored in one chunk.</summary>
    public const int Volume = SizeX * SizeY * SizeZ;

    private readonly BlockState[] _blocks;

    /// <summary>
    /// Creates a chunk at the specified chunk position.
    /// </summary>
    /// <param name="position">The chunk position.</param>
    public Chunk(ChunkPos position)
    {
        Position = position;
        _blocks = new BlockState[Volume];
        Array.Fill(_blocks, BuiltinBlocks.Air.DefaultState);
    }

    /// <summary>The position of this chunk in chunk space.</summary>
    public ChunkPos Position { get; }

    /// <summary>Whether this chunk needs a mesh rebuild.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>The block state stored at the local slot mapped from the position.</returns>
    public BlockState GetBlock(BlockPos position)
    {
        return _blocks[GetIndex(position)];
    }

    /// <summary>
    /// Sets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <param name="state">The block state to store.</param>
    public void SetBlock(BlockPos position, BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _blocks[GetIndex(position)] = state;
        IsDirty = true;
    }

    /// <summary>
    /// Clears the dirty flag.
    /// </summary>
    public void ClearDirty()
    {
        IsDirty = false;
    }

    /// <summary>
    /// Enumerates all block states with chunk-local positions in storage order.
    /// </summary>
    /// <returns>The chunk-local positions and stored block states.</returns>
    public IEnumerable<(BlockPos LocalPosition, BlockState State)> EnumerateBlocks()
    {
        var index = 0;
        for (var y = 0; y < SizeY; y++)
        {
            for (var z = 0; z < SizeZ; z++)
            {
                for (var x = 0; x < SizeX; x++)
                {
                    yield return (new BlockPos(x, y, z), _blocks[index++]);
                }
            }
        }
    }

    private static int GetIndex(BlockPos position)
    {
        var local = position.ToChunkLocalPos();

        return local.X | (local.Z << BitsX) | (local.Y << (BitsX + BitsZ));
    }
}