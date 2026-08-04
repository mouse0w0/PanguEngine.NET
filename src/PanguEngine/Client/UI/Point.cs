namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a point in logical pixels.
/// </summary>
public readonly record struct Point
{
    /// <summary>
    /// Initializes a point from finite coordinates.
    /// </summary>
    /// <param name="x">The finite horizontal coordinate.</param>
    /// <param name="y">The finite vertical coordinate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either coordinate is not finite.
    /// </exception>
    public Point(double x, double y)
    {
        VerifyCoordinate(x, nameof(x));
        VerifyCoordinate(y, nameof(y));
        X = x;
        Y = y;
    }

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Gets the zero point.
    /// </summary>
    public static Point Zero => default;

    private static void VerifyCoordinate(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "A point coordinate must be finite.");
    }
}
