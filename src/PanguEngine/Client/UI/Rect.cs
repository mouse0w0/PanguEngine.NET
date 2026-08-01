namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a rectangular layout boundary in logical pixels.
/// </summary>
public readonly record struct Rect
{
    /// <summary>
    /// Initializes a rectangle from its origin and dimensions.
    /// </summary>
    /// <param name="x">The finite horizontal origin.</param>
    /// <param name="y">The finite vertical origin.</param>
    /// <param name="width">The finite non-negative width.</param>
    /// <param name="height">The finite non-negative height.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the origin is not finite or a dimension is not finite and non-negative.
    /// </exception>
    public Rect(double x, double y, double width, double height)
    {
        VerifyOrigin(x, nameof(x));
        VerifyOrigin(y, nameof(y));
        VerifyDimension(width, nameof(width));
        VerifyDimension(height, nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Initializes a rectangle from its origin and size.
    /// </summary>
    /// <param name="x">The finite horizontal origin.</param>
    /// <param name="y">The finite vertical origin.</param>
    /// <param name="size">The finite size.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the origin or size is not finite, or a size dimension is negative.
    /// </exception>
    public Rect(double x, double y, Size size)
        : this(x, y, size.Width, size.Height)
    {
    }

    /// <summary>
    /// Gets the horizontal origin.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Gets the vertical origin.
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the height.
    /// </summary>
    public double Height { get; }

    /// <summary>
    /// Gets the zero rectangle.
    /// </summary>
    public static Rect Zero => default;

    private static void VerifyOrigin(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "A rectangle origin must be finite.");
    }

    private static void VerifyDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "A rectangle dimension must be finite and non-negative.");
    }
}
