namespace PanguEngine.World.Blocks;

/// <summary>
/// Owns the complete set of canonical block states for a single block type
/// and provides efficient property read and state transition via mixed-radix indexing.
/// Property indexes are available by property reference and name.
/// </summary>
public sealed class BlockStateDefinition
{
    private const int MaxStateCount = 65536;

    private readonly Dictionary<BlockProperty, int> _propertyIndexes;
    private readonly Dictionary<string, int> _propertyNameIndexes;

    internal BlockStateDefinition(Block block, BlockProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var propertySnapshot = (BlockProperty[])properties.Clone();
        for (var i = 0; i < propertySnapshot.Length; i++)
            if (propertySnapshot[i] is null)
                throw new ArgumentException($"Property at index {i} is null.", nameof(properties));

        _propertyIndexes = new Dictionary<BlockProperty, int>(
            propertySnapshot.Length,
            ReferenceEqualityComparer.Instance);
        _propertyNameIndexes = new Dictionary<string, int>(
            propertySnapshot.Length,
            StringComparer.Ordinal);
        for (var index = 0; index < propertySnapshot.Length; index++)
        {
            var property = propertySnapshot[index];
            if (!_propertyNameIndexes.TryAdd(property.Name, index))
                throw new ArgumentException(
                    $"Duplicate property name '{property.Name}'.",
                    nameof(properties));
            _propertyIndexes.Add(property, index);
        }

        // Overflow-safe state count check: before each multiplication verify count <= Max / next_factor.
        var stateCount = 1L;
        foreach (var p in propertySnapshot)
        {
            var factor = (long)p.ValueCount;
            if (stateCount > MaxStateCount / factor)
                throw new InvalidOperationException(
                    $"Block state count would exceed the maximum of {MaxStateCount}.");
            stateCount *= factor;
        }

        Block = block;
        Properties = Array.AsReadOnly(propertySnapshot);

        // Mixed-radix strides: stride[i] = product of ValueCount for all properties after i.
        Strides = new int[propertySnapshot.Length];
        var stride = 1;
        for (var i = propertySnapshot.Length - 1; i >= 0; i--)
        {
            Strides[i] = stride;
            stride *= propertySnapshot[i].ValueCount;
        }

        var states = new BlockState[(int)stateCount];
        for (var i = 0; i < states.Length; i++)
            states[i] = new BlockState(block, i);
        States = Array.AsReadOnly(states);
    }

    /// <summary>The block that owns this definition.</summary>
    public Block Block { get; }

    /// <summary>The properties declared for this block, in declaration order.</summary>
    public IReadOnlyList<BlockProperty> Properties { get; }

    /// <summary>All canonical states for this block, in cartesian-product order.</summary>
    public IReadOnlyList<BlockState> States { get; }

    internal int[] Strides { get; }

    internal int GetPropertyIndex(string name) =>
        _propertyNameIndexes.TryGetValue(name, out var index) ? index : -1;

    internal int GetPropertyIndex(BlockProperty property) =>
        _propertyIndexes.TryGetValue(property, out var index) ? index : -1;
}