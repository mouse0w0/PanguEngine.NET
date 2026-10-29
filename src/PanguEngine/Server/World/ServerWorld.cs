using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities;

namespace PanguEngine.Server.World;

/// <summary>Stores authoritative block and entity state on the server thread.</summary>
public sealed class ServerWorld : IReadOnlyBlockAccessor
{
    internal ServerWorld()
    {
        Chunks = new ChunkManager();
        Entities = new EntityManager();
    }

    internal ChunkManager Chunks { get; }
    internal EntityManager Entities { get; }

    /// <summary>Gets the authoritative block state at a world position.</summary>
    public BlockState GetBlock(BlockPos position) => Chunks.GetBlock(position);

    internal void SetBlock(BlockPos position, BlockState state) => Chunks.SetBlock(position, state);

    internal void Destroy() => Entities.Destroy();
}
