namespace PanguEngine.World.Blocks;

/// <summary>
/// Represents the metadata of a block state property independent of its value type.
/// </summary>
public abstract class BlockProperty
{
    private protected BlockProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || !name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            throw new ArgumentException(
                $"Property name '{name}' must be non-empty and contain only lowercase letters, digits, and underscores.",
                nameof(name));
        Name = name;
    }

    /// <summary>The name of this property.</summary>
    public string Name { get; }

    internal abstract int ValueCount { get; }
    internal abstract string GetValueString(int valueIndex);
    internal abstract int GetValueIndex(string value);

    /// <summary>
    /// Creates a boolean property with allowed values <c>false</c> then <c>true</c>.
    /// </summary>
    public static BlockProperty<bool> CreateBoolean(string name) =>
        new BooleanBlockProperty(name);

    /// <summary>
    /// Creates an enum property with the explicitly supplied allowed values in declaration order.
    /// Enum types marked with <see cref="FlagsAttribute"/> are not supported.
    /// </summary>
    public static BlockProperty<TEnum> CreateEnum<TEnum>(string name, params TEnum[] values)
        where TEnum : struct, Enum =>
        new EnumBlockProperty<TEnum>(name, values);

    /// <summary>
    /// Creates an integer property with consecutive values from <paramref name="minValue"/>
    /// to <paramref name="maxValue"/> inclusive, in ascending order.
    /// </summary>
    public static BlockProperty<int> CreateInteger(string name, int minValue, int maxValue) =>
        new IntegerBlockProperty(name, minValue, maxValue);
}

/// <summary>
/// Represents a type-safe block state property with a finite set of allowed values.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public abstract class BlockProperty<T> : BlockProperty
{
    private protected BlockProperty(string name, T[] values) : base(name)
    {
        Values = Array.AsReadOnly(values);
    }

    /// <summary>The allowed values in declaration order.</summary>
    public IReadOnlyList<T> Values { get; }

    internal abstract int IndexOf(T value);
}