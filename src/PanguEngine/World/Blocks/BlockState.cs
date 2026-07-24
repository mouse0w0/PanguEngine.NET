using PanguEngine.World.Chunking;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Represents a concrete, immutable state of a block type.
/// Instances are canonical: the same logical property combination always
/// returns the same <see cref="BlockState"/> reference via <see cref="With{T}"/>.
/// </summary>
public sealed class BlockState
{
    private readonly int _stateIndex;

    internal BlockState(Block block, int stateIndex)
    {
        Block = block;
        _stateIndex = stateIndex;
    }

    /// <summary>The block represented by this state.</summary>
    public Block Block { get; }

    /// <summary>Whether this state represents empty space.</summary>
    public bool IsAir => Block.IsAir;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="property"/> belongs to this state's definition.
    /// </summary>
    public bool Contains(BlockProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        foreach (var p in Block.StateDefinition.Properties)
            if (ReferenceEquals(p, property))
                return true;
        return false;
    }

    /// <summary>
    /// Returns the current value of <paramref name="property"/> for this state.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="property"/> does not belong to this state's definition.
    /// </exception>
    public T Get<T>(BlockProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var definition = Block.StateDefinition;
        var propIndex = definition.GetPropertyIndex(property);
        var valueIndex = _stateIndex / definition.Strides[propIndex] % property.Values.Count;
        return property.Values[valueIndex];
    }

    /// <summary>
    /// Returns the canonical state that has <paramref name="value"/> for <paramref name="property"/>
    /// and retains all other property values. Returns <see langword="this"/> if the value is unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="property"/> does not belong to this state's definition,
    /// or when <paramref name="value"/> is not an allowed value for the property.
    /// </exception>
    public BlockState With<T>(BlockProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        var definition = Block.StateDefinition;
        var propIndex = definition.GetPropertyIndex(property);
        var newValueIndex = property.IndexOf(value);
        if (newValueIndex < 0)
            throw new ArgumentException(
                $"Value '{value}' is not an allowed value for property '{property.Name}'.",
                nameof(value));
        var oldValueIndex = _stateIndex / definition.Strides[propIndex] % property.Values.Count;
        if (newValueIndex == oldValueIndex)
            return this;
        var newStateIndex = _stateIndex + (newValueIndex - oldValueIndex) * definition.Strides[propIndex];
        return definition.States[newStateIndex];
    }

    /// <summary>
    /// Gets whether the specified face can occlude an adjacent block face.
    /// </summary>
    /// <param name="direction">The direction of the face to inspect.</param>
    /// <returns>Whether the face can occlude an adjacent block face.</returns>
    public bool CanOccludeFace(Direction direction) =>
        Block.CanOccludeFace(this, direction);

    /// <summary>
    /// Gets the selection shape for this state at a world position.
    /// </summary>
    /// <param name="blockAccessor">The block state accessor.</param>
    /// <param name="position">The world block position.</param>
    /// <returns>The selection shape.</returns>
    public IBlockShape GetSelectionShape(IReadOnlyBlockAccessor blockAccessor, BlockPos position) =>
        Block.GetSelectionShape(this, blockAccessor, position);

    /// <summary>
    /// Returns a string representation of this block state in the format "BlockType[prop1=value1,prop2=value2]".
    /// </summary>
    public override string ToString()
    {
        var definition = Block.StateDefinition;
        var blockName = Block.ToString();

        if (definition.Properties.Count == 0)
            return $"{blockName}[]";

        var properties = new string[definition.Properties.Count];
        for (var i = 0; i < definition.Properties.Count; i++)
        {
            var property = definition.Properties[i];
            var valueIndex = _stateIndex / definition.Strides[i] % property.ValueCount;
            var valueString = property.GetValueString(valueIndex);
            properties[i] = $"{property.Name}={valueString}";
        }

        return $"{blockName}[{string.Join(",", properties)}]";
    }
}