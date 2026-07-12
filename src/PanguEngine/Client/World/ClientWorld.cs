using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.World;

/// <summary>
/// Stores local client world block state.
/// </summary>
public sealed class ClientWorld
{
    /// <summary>
    /// Creates a client world with the default platform.
    /// </summary>
    public ClientWorld()
    {
        Chunks = new ChunkManager();
        GenerateDefaultPlatform();
    }

    /// <summary>The chunks that store this world's block state.</summary>
    public ChunkManager Chunks { get; }

    /// <summary>
    /// Gets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>The stored block state, or air when no chunk exists.</returns>
    public BlockState GetBlock(BlockPos position)
    {
        return Chunks.GetBlock(position);
    }

    /// <summary>
    /// Sets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <param name="state">The block state to store.</param>
    public void SetBlock(BlockPos position, BlockState state)
    {
        Chunks.SetBlock(position, state);
    }

    /// <summary>
    /// Gets whether the block at the specified position is air.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>Whether the block state at the position is air.</returns>
    public bool IsAir(BlockPos position)
    {
        return GetBlock(position).IsAir;
    }

    private void GenerateDefaultPlatform()
    {
        for (var z = 0; z < Chunk.SizeZ; z++)
        {
            for (var x = 0; x < Chunk.SizeX; x++)
            {
                SetBlock(new BlockPos(x, 0, z), BuiltinBlocks.Grass.DefaultState);
            }
        }

        for (var y = 1; y < 8; y++)
        {
            SetBlock(new BlockPos(8, y, 8), BuiltinBlocks.Stone.DefaultState);
        }
    }
}