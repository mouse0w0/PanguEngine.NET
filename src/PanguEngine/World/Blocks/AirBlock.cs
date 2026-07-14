using PanguEngine.World.Chunking;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Defines the built-in air block type.
/// </summary>
public sealed class AirBlock : Block
{
    /// <summary>
    /// Creates an air block.
    /// </summary>
    public AirBlock()
    {
    }

    /// <inheritdoc/>
    public override bool IsAir => true;

    /// <inheritdoc/>
    public override bool CanOccludeFace(Direction direction)
    {
        return false;
    }

    /// <inheritdoc/>
    public override IBlockShape GetSelectionShape(
        BlockState state,
        IReadOnlyBlockAccessor blockAccessor,
        BlockPos position)
    {
        return BlockShape.Empty;
    }
}