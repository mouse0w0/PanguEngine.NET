using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Maps a bounded set of canonical states using reference comparisons.</summary>
internal sealed class LinearBlockPalette : IBlockPalette
{
    private readonly BlockState[] _states;

    internal LinearBlockPalette(int capacity) : this(new BlockState[capacity], 0)
    {
    }

    internal LinearBlockPalette(BlockState[] entries, int count)
    {
        _states = entries;
        Count = count;
    }

    /// <inheritdoc/>
    public int Count { get; private set; }

    /// <inheritdoc/>
    public BlockState Get(int id) => _states[id];

    /// <inheritdoc/>
    public bool TryGetOrAdd(BlockState state, out int id)
    {
        for (var i = 0; i < Count; i++)
        {
            if (ReferenceEquals(_states[i], state))
            {
                id = i;
                return true;
            }
        }
        if (Count == _states.Length)
        {
            id = 0;
            return false;
        }
        id = Count++;
        _states[id] = state;
        return true;
    }
}
