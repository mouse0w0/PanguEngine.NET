namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes one laid out text line.
/// </summary>
public sealed class TextLine
{
    internal TextLine(
        int start,
        int length,
        double x,
        double y,
        double width,
        double naturalHeight,
        double height,
        double baseline,
        TextGlyphRun[] glyphRuns)
    {
        Start = start;
        Length = length;
        X = x;
        Y = y;
        Width = width;
        NaturalHeight = naturalHeight;
        Height = height;
        Baseline = baseline;
        GlyphRuns = Array.AsReadOnly(glyphRuns.ToArray());
    }

    /// <summary>Gets the source UTF-16 start index.</summary>
    public int Start { get; }
    /// <summary>Gets the source UTF-16 length.</summary>
    public int Length { get; }
    /// <summary>Gets the line X coordinate.</summary>
    public double X { get; }
    /// <summary>Gets the line top coordinate.</summary>
    public double Y { get; }
    /// <summary>Gets the line advance width.</summary>
    public double Width { get; }
    /// <summary>Gets the natural line height.</summary>
    public double NaturalHeight { get; }
    /// <summary>Gets the final line height.</summary>
    public double Height { get; }
    /// <summary>Gets the baseline Y coordinate.</summary>
    public double Baseline { get; }
    /// <summary>Gets the glyph runs.</summary>
    public IReadOnlyList<TextGlyphRun> GlyphRuns { get; }
}
