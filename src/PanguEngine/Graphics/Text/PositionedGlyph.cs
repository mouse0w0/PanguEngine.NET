namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes a positioned glyph in logical pixels.
/// </summary>
public readonly record struct PositionedGlyph
{
    internal PositionedGlyph(
        uint glyphId,
        int cluster,
        double x,
        double y,
        double xAdvance,
        double yAdvance,
        double xOffset,
        double yOffset,
        bool isMissing)
    {
        GlyphId = glyphId;
        Cluster = cluster;
        X = x;
        Y = y;
        XAdvance = xAdvance;
        YAdvance = yAdvance;
        XOffset = xOffset;
        YOffset = yOffset;
        IsMissing = isMissing;
    }

    /// <summary>Gets the font glyph identifier.</summary>
    public uint GlyphId { get; }
    /// <summary>Gets the source UTF-16 cluster index.</summary>
    public int Cluster { get; }
    /// <summary>Gets the baseline pen X coordinate.</summary>
    public double X { get; }
    /// <summary>Gets the baseline pen Y coordinate.</summary>
    public double Y { get; }
    /// <summary>Gets the horizontal advance.</summary>
    public double XAdvance { get; }
    /// <summary>Gets the vertical advance.</summary>
    public double YAdvance { get; }
    /// <summary>Gets the horizontal glyph offset.</summary>
    public double XOffset { get; }
    /// <summary>Gets the downward-positive vertical glyph offset.</summary>
    public double YOffset { get; }
    /// <summary>Gets whether the glyph represents unsupported or missing text.</summary>
    public bool IsMissing { get; }
}
