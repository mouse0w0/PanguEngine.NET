using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Maps canonical block states to indices used by chunk storage.</summary>
internal interface IBlockPalette
{
    /// <summary>The number of mapped states.</summary>
    int Count { get; }

    /// <summary>Gets the canonical state mapped to an index.</summary>
    BlockState Get(int id);

    /// <summary>Gets or inserts an index, returning false only when a new state exceeds capacity.</summary>
    bool TryGetOrAdd(BlockState state, out int id);
}
