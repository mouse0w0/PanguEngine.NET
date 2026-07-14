using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.World;

/// <summary>
/// Provides read-only access to block states by world position.
/// </summary>
public interface IReadOnlyBlockAccessor
{
    /// <summary>
    /// Gets the block state at the specified world position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>The block state at the position.</returns>
    BlockState GetBlock(BlockPos position);
}