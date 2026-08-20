using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Graphics.Text;

public sealed class GlyphRasterizationTests
{
    [Theory]
    [InlineData(16, 1.25, 20u)]
    [InlineData(10, 1.25, 13u)]
    [InlineData(0.1, 1, 1u)]
    public void PixelSizeUsesNearestPhysicalPixel(
        double fontSize,
        double scale,
        uint expected) =>
        Assert.Equal(expected, GlyphRasterization.GetPixelSize(fontSize, scale));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(double.MaxValue, double.MaxValue)]
    public void PixelSizeRejectsValuesWithoutARepresentablePositiveResult(
        double fontSize,
        double scale) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GlyphRasterization.GetPixelSize(fontSize, scale));

    [Fact]
    public void RasterizeCopiesGrayBitmapAndBearings()
    {
        using var context = new GlyphFontContext();
        var glyphId = context.GetGlyphId("A");

        var bitmap = context.FontManager.Rasterize(
            context.DefaultFace,
            20,
            glyphId,
            GlyphRasterizationMode.Grayscale);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
        Assert.Equal(bitmap.Width * bitmap.Height, bitmap.Pixels.Length);
    }

    [Fact]
    public void RasterizeCachesNoNativeStateInAnEmptyBitmap()
    {
        using var context = new GlyphFontContext();
        var glyphId = context.GetGlyphId(" ");

        var bitmap = context.FontManager.Rasterize(
            context.DefaultFace,
            20,
            glyphId,
            GlyphRasterizationMode.Grayscale);

        Assert.True(bitmap.Width == 0 || bitmap.Height == 0);
        Assert.Empty(bitmap.Pixels.ToArray());
    }

    [Fact]
    public void RasterizeRejectsFaceFromAnotherManager()
    {
        using var first = new GlyphFontContext();
        using var second = new GlyphFontContext();
        var glyphId = second.GetGlyphId("A");

        Assert.Throws<ArgumentException>(() => first.FontManager.Rasterize(
            second.DefaultFace,
            20,
            glyphId,
            GlyphRasterizationMode.Grayscale));
    }

    [Fact]
    public void RasterizeRejectsAccessAfterManagerDisposal()
    {
        var context = new GlyphFontContext();
        var glyphId = context.GetGlyphId("A");
        context.Dispose();

        Assert.Throws<ObjectDisposedException>(() => context.FontManager.Rasterize(
            context.DefaultFace,
            20,
            glyphId,
            GlyphRasterizationMode.Grayscale));
    }

    [Fact]
    public void RasterizeRejectsAccessFromAnotherThread()
    {
        using var context = new GlyphFontContext();
        var glyphId = context.GetGlyphId("A");
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => context.FontManager.Rasterize(
                context.DefaultFace,
                20,
                glyphId,
                GlyphRasterizationMode.Grayscale)));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
    }

    private sealed class GlyphFontContext : IDisposable
    {
        private static readonly string FontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "SourceHanSansCN-Regular.otf");

        internal GlyphFontContext()
        {
            FontManager = new FontManager();
            using var stream = File.OpenRead(FontPath);
            var font = Assert.Single(FontManager.Register(stream, 0));
            FontManager.DefaultFont = font;
            DefaultFace = FontManager.Match(font);
            LayoutEngine = new TextLayoutEngine(FontManager);
        }

        internal FontManager FontManager { get; }
        internal FontFace DefaultFace { get; }
        private TextLayoutEngine LayoutEngine { get; }

        internal uint GetGlyphId(string text)
        {
            var layout = LayoutEngine.Layout(new TextLayoutRequest(
                text,
                DefaultFace.Font,
                16,
                double.PositiveInfinity,
                1,
                TextWrapping.NoWrap,
                TextAlignment.Left));
            return Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs[0].GlyphId;
        }

        public void Dispose() => FontManager.Destroy();
    }
}