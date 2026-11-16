using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Uses the bound session state identifiers without a local mapping.</summary>
internal sealed class GlobalBlockPalette : IBlockPalette
{
    /// <inheritdoc/>
    public int Count => BlockStateIds.Count;

    /// <inheritdoc/>
    public BlockState Get(int id) => BlockStateIds.Get(id);

    /// <inheritdoc/>
    public bool TryGetOrAdd(BlockState state, out int id)
    {
        id = BlockStateIds.GetId(state);
        return true;
    }
}
