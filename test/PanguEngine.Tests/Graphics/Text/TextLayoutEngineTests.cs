using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Graphics.Text;

public sealed class TextLayoutEngineTests
{
    [Fact]
    public void LayoutRejectsRequestsBeforeDefaultFontInitialization()
    {
        using var manager = new FontManager();
        var engine = new TextLayoutEngine(manager);
        var request = new TextLayoutRequest(
            "Pangu",
            new Font("Missing Family"),
            16,
            double.PositiveInfinity,
            1,
            TextWrapping.NoWrap,
            TextAlignment.Left);

        Assert.Throws<InvalidOperationException>(() => engine.Layout(request));
    }

    [Fact]
    public void LayoutReturnsZeroSizeForEmptyText()
    {
        using var context = new LayoutContext();

        var layout = context.Layout(string.Empty);

        Assert.Empty(layout.Lines);
        Assert.Equal(0, layout.Width);
        Assert.Equal(0, layout.Height);
        Assert.Equal(TextBounds.Empty, layout.InkBounds);
    }

    [Fact]
    public void LayoutPreservesUtf16ClustersForLigaturesAndCombiningMarks()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("office e\u0301");
        var glyphs = layout.Lines.SelectMany(line => line.GlyphRuns).SelectMany(run => run.Glyphs).ToArray();

        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.InRange(glyph.Cluster, 0, 8));
        Assert.True(glyphs.Zip(glyphs.Skip(1), (left, right) => left.Cluster <= right.Cluster).All(value => value));
    }

    [Fact]
    public void LayoutUsesDefaultFontForEnglishAndSimplifiedChinese()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("Pangu 盘古引擎");
        var runs = Assert.Single(layout.Lines).GlyphRuns;

        Assert.All(runs, run => Assert.Same(context.DefaultFace, run.FontFace));
        Assert.All(runs, run => Assert.Equal(run.FontFace.Font, run.Font));
        Assert.All(runs.SelectMany(run => run.Glyphs), glyph => Assert.False(glyph.IsMissing));
    }

    [Fact]
    public void LayoutFallsBackToDefaultFaceForMissingRequestedFamily()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("Pangu", font: new Font("Missing Family"));
        var runs = Assert.Single(layout.Lines).GlyphRuns;

        Assert.All(runs, run => Assert.Same(context.DefaultFace, run.FontFace));
    }

    [Fact]
    public void LayoutTreatsCrLfAndTrailingNewlineAsEmptyLines()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("A\r\n\n");

        Assert.Equal(3, layout.Lines.Count);
        Assert.Equal(0, layout.Lines[1].Length);
        Assert.Equal(0, layout.Lines[2].Length);
    }

    [Fact]
    public void LayoutWrapsAtWhitespaceAndKeepsWhitespaceInTheLine()
    {
        using var context = new LayoutContext();
        var single = context.Layout("A");

        var layout = context.Layout("A A", maximumWidth: single.Width * 1.6, wrapping: TextWrapping.Wrap);

        Assert.Equal(2, layout.Lines.Count);
        Assert.Equal(2, layout.Lines[0].Length);
        Assert.Equal(1, layout.Lines[1].Length);
    }

    [Theory]
    [InlineData("\u00A0")]
    [InlineData("\u2007")]
    [InlineData("\u202F")]
    [InlineData("\u2060")]
    [InlineData("\uFEFF")]
    public void LayoutDoesNotPreferExcludedWhitespaceAsBreak(string excludedWhitespace)
    {
        using var context = new LayoutContext();
        var prefix = $"A{excludedWhitespace}A";
        var prefixLayout = context.Layout(prefix);

        var layout = context.Layout(
            $"{prefix}A",
            maximumWidth: prefixLayout.Width + 0.01,
            wrapping: TextWrapping.Wrap);

        Assert.Equal(2, layout.Lines.Count);
        Assert.Equal(prefix.Length, layout.Lines[0].Length);
    }

    [Fact]
    public void LayoutMeasuresEachCandidateAfterLigatureReshaping()
    {
        using var context = new LayoutContext();
        var twoCharacters = context.Layout("ff");

        var layout = context.Layout(
            "ffi",
            maximumWidth: twoCharacters.Width + 0.01,
            wrapping: TextWrapping.Wrap);

        Assert.Equal(2, layout.Lines.Count);
        Assert.Equal(2, layout.Lines[0].Length);
        Assert.Equal(1, layout.Lines[1].Length);
    }

    [Fact]
    public void LayoutFallsBackAtTextElementBoundariesForZeroWidth()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("word", maximumWidth: 0, wrapping: TextWrapping.Wrap);

        Assert.Equal(4, layout.Lines.Count);
        Assert.All(layout.Lines, line => Assert.Equal(1, line.Length));
    }

    [Fact]
    public void LayoutKeepsVariationSequenceInOneCluster()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("A\uFE0F");
        var run = Assert.Single(layout.Lines[0].GlyphRuns);

        Assert.Equal(2, run.Length);
        Assert.NotEmpty(run.Glyphs);
        Assert.All(run.Glyphs, glyph => Assert.Equal(0, glyph.Cluster));
        Assert.All(run.Glyphs, glyph => Assert.False(glyph.IsMissing));
    }

    [Fact]
    public void LayoutRightAlignsShorterLinesToWidestLine()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("WW\nI", alignment: TextAlignment.Right);

        Assert.Equal(2, layout.Lines.Count);
        Assert.Equal(0, layout.Lines[0].X);
        Assert.True(layout.Lines[1].X > 0);
    }

    [Fact]
    public void LayoutAppliesNaturalLineHeightMultiplier()
    {
        using var context = new LayoutContext();

        var normal = context.Layout("A\nB", lineHeight: 1);
        var expanded = context.Layout("A\nB", lineHeight: 1.5);

        Assert.Equal(normal.Height * 1.5, expanded.Height, 8);
    }

    [Fact]
    public void LayoutAllowsLineFramesToOverlap()
    {
        using var context = new LayoutContext();

        var normal = context.Layout("A\nB", lineHeight: 1);
        var compact = context.Layout("A\nB", lineHeight: 0.75);

        Assert.Equal(normal.Height * 0.75, compact.Height, 8);
    }

    [Fact]
    public void LayoutMarksRtlTextAsUnsupportedWithoutReorderingClusters()
    {
        using var context = new LayoutContext();

        var layout = context.Layout("אב");
        var glyphs = layout.Lines.SelectMany(line => line.GlyphRuns).SelectMany(run => run.Glyphs).ToArray();

        Assert.Equal(2, glyphs.Length);
        Assert.All(glyphs, glyph => Assert.True(glyph.IsMissing));
        Assert.Equal([0, 1], glyphs.Select(glyph => glyph.Cluster));
    }

    [Fact]
    public void LayoutRejectsInvalidMathematicalInputs()
    {
        using var context = new LayoutContext();

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Layout("A", fontSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Layout("A", lineHeight: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Layout("A", maximumWidth: -1));
    }

    private sealed class LayoutContext : IDisposable
    {
        internal LayoutContext()
        {
            using var stream = File.OpenRead(FontPath);
            FontManager = new FontManager();
            FontManager.DefaultFont = Assert.Single(FontManager.Register(stream, 0));
            DefaultFace = FontManager.Match(FontManager.DefaultFont);
            TextLayoutEngine = new TextLayoutEngine(FontManager);
        }

        internal FontManager FontManager { get; }
        internal FontFace DefaultFace { get; }
        internal TextLayoutEngine TextLayoutEngine { get; }

        internal TextLayout Layout(
            string text,
            double fontSize = 16,
            double maximumWidth = double.PositiveInfinity,
            double lineHeight = 1,
            TextWrapping wrapping = TextWrapping.NoWrap,
            TextAlignment alignment = TextAlignment.Left,
            Font? font = null)
        {
            return TextLayoutEngine.Layout(new TextLayoutRequest(
                text,
                font ?? FontManager.DefaultFont,
                fontSize,
                maximumWidth,
                lineHeight,
                wrapping,
                alignment));
        }

        public void Dispose()
        {
            FontManager.Dispose();
        }
    }

    private static string FontPath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "SourceHanSansCN-Regular.otf");
}
