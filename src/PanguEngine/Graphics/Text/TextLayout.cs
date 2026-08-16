namespace PanguEngine.Graphics.Text;

/// <summary>
/// Represents an immutable CPU text layout.
/// </summary>
public sealed class TextLayout
{
    internal TextLayout(double width, double height, TextBounds inkBounds, TextLine[] lines)
    {
        Width = width;
        Height = height;
        InkBounds = inkBounds;
        Lines = Array.AsReadOnly(lines.ToArray());
    }

    /// <summary>Gets the logical layout width.</summary>
    public double Width { get; }
    /// <summary>Gets the logical layout height.</summary>
    public double Height { get; }
    /// <summary>Gets the glyph ink bounds.</summary>
    public TextBounds InkBounds { get; }
    /// <summary>Gets the laid out lines.</summary>
    public IReadOnlyList<TextLine> Lines { get; }
}
