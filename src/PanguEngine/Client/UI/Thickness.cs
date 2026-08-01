namespace PanguEngine.Client.UI;

/// <summary>
/// Represents non-negative spacing around a rectangular element in logical pixels.
/// </summary>
public readonly record struct Thickness
{
    /// <summary>
    /// Initializes equal spacing on every edge.
    /// </summary>
    /// <param name="uniform">The finite non-negative edge value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite and non-negative.
    /// </exception>
    public Thickness(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Initializes symmetric horizontal and vertical spacing.
    /// </summary>
    /// <param name="horizontal">The finite non-negative left and right value.</param>
    /// <param name="vertical">The finite non-negative top and bottom value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a value is not finite and non-negative.
    /// </exception>
    public Thickness(double horizontal, double vertical)
        : this(horizontal, vertical, horizontal, vertical)
    {
    }

    /// <summary>
    /// Initializes spacing for each edge.
    /// </summary>
    /// <param name="left">The finite non-negative left value.</param>
    /// <param name="top">The finite non-negative top value.</param>
    /// <param name="right">The finite non-negative right value.</param>
    /// <param name="bottom">The finite non-negative bottom value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a value is not finite and non-negative.
    /// </exception>
    public Thickness(double left, double top, double right, double bottom)
    {
        VerifyEdge(left, nameof(left));
        VerifyEdge(top, nameof(top));
        VerifyEdge(right, nameof(right));
        VerifyEdge(bottom, nameof(bottom));
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>
    /// Gets the left spacing.
    /// </summary>
    public double Left { get; }

    /// <summary>
    /// Gets the top spacing.
    /// </summary>
    public double Top { get; }

    /// <summary>
    /// Gets the right spacing.
    /// </summary>
    public double Right { get; }

    /// <summary>
    /// Gets the bottom spacing.
    /// </summary>
    public double Bottom { get; }

    /// <summary>
    /// Gets zero spacing on every edge.
    /// </summary>
    public static Thickness Zero => default;

    private static void VerifyEdge(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "A thickness value must be finite and non-negative.");
    }
}
