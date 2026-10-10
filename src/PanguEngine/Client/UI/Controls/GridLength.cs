namespace PanguEngine.Client.UI.Controls;

/// <summary>Represents a fixed grid length, automatic sizing, or a proportional weight.</summary>
public readonly record struct GridLength
{
    /// <summary>Initializes a grid length or weight.</summary>
    /// <param name="value">The finite non-negative length or weight.</param>
    /// <param name="unitType">The sizing mode.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not finite and non-negative.</exception>
    public GridLength(double value, GridUnitType unitType = GridUnitType.Pixel)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "A grid length or weight must be finite and non-negative.");
        Value = unitType == GridUnitType.Auto ? 1 : value;
        UnitType = unitType;
    }

    /// <summary>Gets the fixed length or weight; automatic lengths use one.</summary>
    public double Value { get; }

    /// <summary>Gets the sizing mode.</summary>
    public GridUnitType UnitType { get; }

    /// <summary>Gets a length sized to its content.</summary>
    public static GridLength Auto => new(1, GridUnitType.Auto);

    /// <summary>Gets a proportional length with weight one.</summary>
    public static GridLength Star => new(1, GridUnitType.Star);

    /// <summary>Converts a logical pixel length into a fixed grid length.</summary>
    /// <param name="value">The fixed length.</param>
    /// <returns>The fixed grid length.</returns>
    public static implicit operator GridLength(double value) => new(value);
}
