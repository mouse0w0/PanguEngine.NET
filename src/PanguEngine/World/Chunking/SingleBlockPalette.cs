using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Maps one canonical state to index zero without an entry array.</summary>
internal sealed class SingleBlockPalette(BlockState state) : IBlockPalette
{
    /// <inheritdoc/>
    public int Count => 1;

    /// <inheritdoc/>
    public BlockState Get(int id) => state;

    /// <inheritdoc/>
    public bool TryGetOrAdd(BlockState value, out int id)
    {
        id = 0;
        return ReferenceEquals(state, value);
    }
}
