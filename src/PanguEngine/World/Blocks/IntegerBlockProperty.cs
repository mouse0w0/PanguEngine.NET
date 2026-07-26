using System.Globalization;

namespace PanguEngine.World.Blocks;

internal sealed class IntegerBlockProperty : BlockProperty<int>
{
    internal IntegerBlockProperty(string name, int minValue, int maxValue) : base(name,
        BuildValues(name, minValue, maxValue))
    {
    }

    internal override int ValueCount => Values.Count;

    internal override string GetValueString(int valueIndex) =>
        Values[valueIndex].ToString(CultureInfo.InvariantCulture);

    internal override int GetValueIndex(string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            !string.Equals(
                parsed.ToString(CultureInfo.InvariantCulture),
                value,
                StringComparison.Ordinal))
        {
            return -1;
        }

        var min = Values[0];
        var max = Values[^1];
        if (parsed < min || parsed > max)
            return -1;

        return parsed - min;
    }

    internal override int IndexOf(int value)
    {
        int min = Values[0];
        int max = Values[^1];
        if (value < min || value > max)
            throw new ArgumentException(
                $"Value '{value}' is not a valid value for property '{Name}' (range [{min}, {max}]).", nameof(value));
        return value - min;
    }

    private static int[] BuildValues(string name, int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue),
                $"minValue ({minValue}) must be <= maxValue ({maxValue}).");
        long count = (long)maxValue - minValue + 1;
        if (count > 65536)
            throw new ArgumentOutOfRangeException(nameof(maxValue),
                $"Integer property '{name}' has {count} values, which exceeds the limit of 65536.");
        int[] values = new int[count];
        for (int i = 0; i < values.Length; i++)
            values[i] = minValue + i;
        return values;
    }
}