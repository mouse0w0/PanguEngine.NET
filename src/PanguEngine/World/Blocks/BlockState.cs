using PanguEngine.World.Chunking;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Represents a concrete state of a block.
/// </summary>
public sealed class BlockState
{
    /// <summary>
    /// Creates a block state for the specified block.
    /// </summary>
    /// <param name="block">The block represented by this state.</param>
    public BlockState(Block block)
    {
        ArgumentNullException.ThrowIfNull(block);
        Block = block;
    }

    /// <summary>The block represented by this state.</summary>
    public Block Block { get; }

    /// <summary>Whether this state represents empty space.</summary>
    public bool IsAir => Block.IsAir;

    /// <summary>
    /// Gets whether the specified face can occlude an adjacent block face.
    /// </summary>
    /// <param name="direction">The direction of the face to inspect.</param>
    /// <returns>Whether the face can occlude an adjacent block face.</returns>
    public bool CanOccludeFace(Direction direction)
    {
        return Block.CanOccludeFace(direction);
    }

    /// <summary>
    /// Gets the selection shape for this state at a world position.
    /// </summary>
    /// <param name="blockAccessor">The block state accessor.</param>
    /// <param name="position">The world block position.</param>
    /// <returns>The selection shape.</returns>
    public IBlockShape GetSelectionShape(IReadOnlyBlockAccessor blockAccessor, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(blockAccessor);
        return Block.GetSelectionShape(this, blockAccessor, position);
    }
}