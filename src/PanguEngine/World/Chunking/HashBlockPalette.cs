using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Maps a bounded set of canonical states through a reverse lookup dictionary.</summary>
internal sealed class HashBlockPalette : IBlockPalette
{
    private readonly BlockState[] _states;
    private readonly Dictionary<BlockState, int> _indices;

    internal HashBlockPalette(int capacity)
    {
        _states = new BlockState[capacity];
        _indices = new Dictionary<BlockState, int>(capacity, ReferenceEqualityComparer.Instance);
    }

    /// <inheritdoc/>
    public int Count { get; private set; }

    /// <inheritdoc/>
    public BlockState Get(int id) => _states[id];

    /// <inheritdoc/>
    public bool TryGetOrAdd(BlockState state, out int id)
    {
        if (_indices.TryGetValue(state, out id))
            return true;
        if (Count == _states.Length)
        {
            id = 0;
            return false;
        }
        id = Count;
        _states[id] = state;
        _indices.Add(state, id);
        Count++;
        return true;
    }
}
