namespace PanguEngine.Client.UI;

/// <summary>
/// Represents non-negative corner radii in logical pixels, ordered clockwise from the top left.
/// </summary>
public readonly record struct CornerRadius
{
    /// <summary>Initializes every corner with the same radius.</summary>
    /// <param name="uniform">The finite non-negative radius.</param>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite and non-negative.</exception>
    public CornerRadius(double uniform) : this(uniform, uniform, uniform, uniform) { }

    /// <summary>Initializes independent radii for the four corners.</summary>
    /// <param name="topLeft">The top-left radius.</param>
    /// <param name="topRight">The top-right radius.</param>
    /// <param name="bottomRight">The bottom-right radius.</param>
    /// <param name="bottomLeft">The bottom-left radius.</param>
    /// <exception cref="ArgumentOutOfRangeException">A radius is not finite and non-negative.</exception>
    public CornerRadius(double topLeft, double topRight, double bottomRight, double bottomLeft)
    {
        Verify(topLeft, nameof(topLeft));
        Verify(topRight, nameof(topRight));
        Verify(bottomRight, nameof(bottomRight));
        Verify(bottomLeft, nameof(bottomLeft));
        TopLeft = topLeft;
        TopRight = topRight;
        BottomRight = bottomRight;
        BottomLeft = bottomLeft;
    }

    /// <summary>Gets the top-left radius.</summary>
    public double TopLeft { get; }
    /// <summary>Gets the top-right radius.</summary>
    public double TopRight { get; }
    /// <summary>Gets the bottom-right radius.</summary>
    public double BottomRight { get; }
    /// <summary>Gets the bottom-left radius.</summary>
    public double BottomLeft { get; }
    /// <summary>Gets square corners.</summary>
    public static CornerRadius Zero => default;

    private static void Verify(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, "A corner radius must be finite and non-negative.");
    }
}
