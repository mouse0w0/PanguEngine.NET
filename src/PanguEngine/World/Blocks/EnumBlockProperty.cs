namespace PanguEngine.World.Blocks;

internal sealed class EnumBlockProperty<TEnum> : BlockProperty<TEnum>
    where TEnum : struct, Enum
{
    internal EnumBlockProperty(string name, TEnum[] values) : base(name, values)
    {
        if (values.Length == 0)
            throw new ArgumentException("Enum property must have at least one value.", nameof(values));
        if (values.Length > 65536)
            throw new ArgumentException("Enum property cannot have more than 65536 values.", nameof(values));
        var seen = new HashSet<TEnum>();
        foreach (var v in values)
            if (!seen.Add(v))
                throw new ArgumentException($"Duplicate value '{v}' in enum property '{name}'.", nameof(values));
    }

    internal override int ValueCount => Values.Count;

    internal override string GetValueString(int valueIndex) => Values[valueIndex].ToString().ToLowerInvariant();

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