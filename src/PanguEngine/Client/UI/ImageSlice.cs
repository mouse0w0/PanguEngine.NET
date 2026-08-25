namespace PanguEngine.Client.UI;

/// <summary>
/// Represents fixed source-pixel insets for a nine-slice image.
/// </summary>
public readonly record struct ImageSlice
{
    /// <summary>
    /// Initializes equal source-pixel insets on every edge.
    /// </summary>
    /// <param name="uniform">The non-negative source-pixel inset.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the inset is negative.</exception>
    public ImageSlice(int uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Initializes symmetric horizontal and vertical source-pixel insets.
    /// </summary>
    /// <param name="horizontal">The non-negative left and right source-pixel inset.</param>
    /// <param name="vertical">The non-negative top and bottom source-pixel inset.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an inset is negative.</exception>
    public ImageSlice(int horizontal, int vertical)
        : this(horizontal, vertical, horizontal, vertical)
    {
    }

    /// <summary>
    /// Initializes source-pixel insets for each edge.
    /// </summary>
    /// <param name="left">The non-negative left source-pixel inset.</param>
    /// <param name="top">The non-negative top source-pixel inset.</param>
    /// <param name="right">The non-negative right source-pixel inset.</param>
    /// <param name="bottom">The non-negative bottom source-pixel inset.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an inset is negative.</exception>
    public ImageSlice(int left, int top, int right, int bottom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(right);
        ArgumentOutOfRangeException.ThrowIfNegative(bottom);
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>
    /// Gets the left source-pixel inset.
    /// </summary>
    public int Left { get; }

    /// <summary>
    /// Gets the top source-pixel inset.
    /// </summary>
    public int Top { get; }

    /// <summary>
    /// Gets the right source-pixel inset.
    /// </summary>
    public int Right { get; }

    /// <summary>
    /// Gets the bottom source-pixel inset.
    /// </summary>
    public int Bottom { get; }

    /// <summary>
    /// Gets zero source-pixel insets on every edge.
    /// </summary>
    public static ImageSlice Zero => default;
}
