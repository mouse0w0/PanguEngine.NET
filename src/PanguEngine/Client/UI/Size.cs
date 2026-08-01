namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a two-dimensional size in logical pixels.
/// </summary>
public readonly record struct Size
{
    /// <summary>
    /// Initializes a size from its dimensions.
    /// </summary>
    /// <param name="width">The non-negative width.</param>
    /// <param name="height">The non-negative height.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a dimension is NaN, negative, or negative infinity.
    /// </exception>
    public Size(double width, double height)
    {
        VerifyDimension(width, nameof(width));
        VerifyDimension(height, nameof(height));
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the height.
    /// </summary>
    public double Height { get; }

    /// <summary>
    /// Gets the zero size.
    /// </summary>
    public static Size Zero => default;

    /// <summary>
    /// Gets a size with both dimensions unconstrained.
    /// </summary>
    public static Size Infinite { get; } =
        new(double.PositiveInfinity, double.PositiveInfinity);

    private static void VerifyDimension(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "A size dimension must be non-negative or positive infinity.");
    }
}
