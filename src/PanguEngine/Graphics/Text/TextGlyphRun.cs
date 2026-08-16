namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes a contiguous run of positioned glyphs using one font.
/// </summary>
public sealed class TextGlyphRun
{
    internal TextGlyphRun(FontFace fontFace, int start, int length, PositionedGlyph[] glyphs)
    {
        FontFace = fontFace;
        Start = start;
        Length = length;
        Glyphs = Array.AsReadOnly(glyphs);
    }

    /// <summary>Gets the loaded font face used by the run.</summary>
    public FontFace FontFace { get; }
    /// <summary>Gets the exact font metadata exposed by the run face.</summary>
    public Font Font => FontFace.Font;
    /// <summary>Gets the source UTF-16 start index.</summary>
    public int Start { get; }
    /// <summary>Gets the source UTF-16 length.</summary>
    public int Length { get; }
    /// <summary>Gets the positioned glyphs.</summary>
    public IReadOnlyList<PositionedGlyph> Glyphs { get; }
}
