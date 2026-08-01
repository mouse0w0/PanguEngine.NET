namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a fill used by UI decorations.
/// </summary>
public abstract class Brush
{
    /// <summary>
    /// Initializes a brush.
    /// </summary>
    private protected Brush()
    {
    }
}

/// <summary>
/// Represents a brush that fills an area with one color.
/// </summary>
public sealed class SolidColorBrush : Brush, IEquatable<SolidColorBrush>
{
    /// <summary>
    /// Initializes a solid color brush.
    /// </summary>
    /// <param name="color">The fill color.</param>
    public SolidColorBrush(Color color)
    {
        Color = color;
    }

    /// <summary>
    /// Gets the fill color.
    /// </summary>
    public Color Color { get; }

    /// <inheritdoc />
    public bool Equals(SolidColorBrush? other) =>
        other is not null && Color == other.Color;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is SolidColorBrush other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Color.GetHashCode();
}
