namespace PanguEngine.Client.UI.Controls;

/// <summary>Defines the immutable sizing limits of one grid row or column.</summary>
public readonly record struct GridDefinition
{
    private readonly double? _maximum;

    /// <summary>Initializes a row or column definition.</summary>
    /// <param name="length">The fixed length, automatic mode, or proportional weight.</param>
    /// <param name="minLength">The finite non-negative minimum length.</param>
    /// <param name="maxLength">The non-negative maximum length, or positive infinity.</param>
    /// <remarks>The minimum takes precedence when it exceeds the maximum.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a sizing limit is invalid.</exception>
    public GridDefinition(GridLength length, double minLength = 0, double maxLength = double.PositiveInfinity)
    {
        if (!double.IsFinite(minLength) || minLength < 0)
            throw new ArgumentOutOfRangeException(nameof(minLength), "A grid minimum must be finite and non-negative.");
        if (double.IsNaN(maxLength) || maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "A grid maximum must be non-negative or positive infinity.");
        Length = length;
        MinLength = minLength;
        _maximum = double.IsPositiveInfinity(maxLength) ? null : maxLength;
    }

    /// <summary>Gets the length or proportional weight.</summary>
    public GridLength Length { get; }

    /// <summary>Gets the minimum length in logical pixels.</summary>
    public double MinLength { get; }

    /// <summary>Gets the maximum length in logical pixels, or positive infinity.</summary>
    public double MaxLength => _maximum ?? double.PositiveInfinity;

    /// <summary>Converts a grid length into a definition without additional limits.</summary>
    /// <param name="length">The length or weight.</param>
    /// <returns>The definition.</returns>
    public static implicit operator GridDefinition(GridLength length) => new(length);

    /// <summary>Converts a logical pixel length into a fixed definition.</summary>
    /// <param name="length">The fixed length.</param>
    /// <returns>The definition.</returns>
    public static implicit operator GridDefinition(double length) => new(new GridLength(length));
}
