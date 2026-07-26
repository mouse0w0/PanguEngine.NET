namespace PanguEngine.World.Blocks;

internal sealed class EnumBlockProperty<TEnum> : BlockProperty<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<string, int> _valueIndexes;

    internal EnumBlockProperty(string name, TEnum[] values) : base(name, values)
    {
        if (typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false))
            throw new ArgumentException(
                $"Flags enum type '{typeof(TEnum).Name}' is not supported for block property '{name}'.",
                nameof(values));
        if (values.Length == 0)
            throw new ArgumentException("Enum property must have at least one value.", nameof(values));
        if (values.Length > 65536)
            throw new ArgumentException("Enum property cannot have more than 65536 values.", nameof(values));

        var seen = new HashSet<TEnum>();
        _valueIndexes = new Dictionary<string, int>(values.Length, StringComparer.Ordinal);
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (!seen.Add(value))
                throw new ArgumentException($"Duplicate value '{value}' in enum property '{name}'.", nameof(values));

            var valueString = GetValueString(index);
            if (!_valueIndexes.TryAdd(valueString, index))
                throw new ArgumentException(
                    $"Duplicate value key '{valueString}' in enum property '{name}'.",
                    nameof(values));
        }
    }

    internal override int ValueCount => Values.Count;

    internal override string GetValueString(int valueIndex) => Values[valueIndex].ToString().ToLowerInvariant();

    internal override int GetValueIndex(string value) =>
        _valueIndexes.TryGetValue(value, out var valueIndex) ? valueIndex : -1;

    internal override int IndexOf(TEnum value)
    {
        for (int i = 0; i < Values.Count; i++)
        {
            if (EqualityComparer<TEnum>.Default.Equals(Values[i], value))
                return i;
        }

        throw new ArgumentException($"Value '{value}' is not a valid value for property '{Name}'.", nameof(value));
    }
}