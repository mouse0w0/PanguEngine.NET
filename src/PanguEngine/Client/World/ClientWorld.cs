using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities;

namespace PanguEngine.Client.World;

/// <summary>
/// Stores local client world block state.
/// </summary>
public sealed class ClientWorld : IReadOnlyBlockAccessor
{
    private bool _isDestroyed;

    /// <summary>
    /// Creates a client world using the frozen process-level entity definitions.
    /// </summary>
    public ClientWorld()
    {
        Chunks = new ChunkManager();
        Entities = new EntityManager();
    }

    /// <summary>The chunks that store this world's block state.</summary>
    public ChunkManager Chunks { get; }

    /// <summary>The entities that belong to this world.</summary>
    public EntityManager Entities { get; }

    /// <summary>Raised after a block state is stored.</summary>
    internal event Action<BlockPos>? BlockChanged;

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
        BlockChanged?.Invoke(position);
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

    /// <summary>
    /// Destroys the entity state owned by this world.
    /// </summary>
    public void Destroy()
    {
        if (_isDestroyed)
            return;

        _isDestroyed = true;
        BlockChanged = null;
        Entities.Destroy();
    }
}
