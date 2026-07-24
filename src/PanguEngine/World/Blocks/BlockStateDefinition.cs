namespace PanguEngine.World.Blocks;

/// <summary>
/// Owns the complete set of canonical block states for a single block type
/// and provides efficient property read and state transition via mixed-radix indexing.
/// Property lookup is linear in the number of declared properties, which is typically small (1-4).
/// </summary>
public sealed class BlockStateDefinition
{
    private const int MaxStateCount = 65536;

    private readonly BlockProperty[] _properties;

    internal BlockStateDefinition(Block block, BlockProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        for (var i = 0; i < properties.Length; i++)
            if (properties[i] is null)
                throw new ArgumentException($"Property at index {i} is null.", nameof(properties));

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in properties)
            if (!names.Add(p.Name))
                throw new ArgumentException($"Duplicate property name '{p.Name}'.", nameof(properties));

        // Overflow-safe state count check: before each multiplication verify count <= Max / next_factor.
        var stateCount = 1L;
        foreach (var p in properties)
        {
            var factor = (long)p.ValueCount;
            if (stateCount > MaxStateCount / factor)
                throw new InvalidOperationException(
                    $"Block state count would exceed the maximum of {MaxStateCount}.");
            stateCount *= factor;
        }

        Block = block;
        _properties = (BlockProperty[])properties.Clone();
        Properties = Array.AsReadOnly(_properties);

        // Mixed-radix strides: stride[i] = product of ValueCount for all properties after i.
        Strides = new int[properties.Length];
        var stride = 1;
        for (var i = properties.Length - 1; i >= 0; i--)
        {
            Strides[i] = stride;
            stride *= properties[i].ValueCount;
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

    /// <summary>
    /// Returns the zero-based index of <paramref name="property"/> in this definition.
    /// </summary>
    /// <param name="property">The property to locate.</param>
    /// <returns>The zero-based index of the property.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="property"/> does not belong to this definition.
    /// </exception>
    internal int GetPropertyIndex(BlockProperty property)
    {
        for (var i = 0; i < _properties.Length; i++)
            if (ReferenceEquals(_properties[i], property))
                return i;
        throw new ArgumentException(
            $"Property '{property.Name}' does not belong to this block's state definition.",
            nameof(property));
    }
}