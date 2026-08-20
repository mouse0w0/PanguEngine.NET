using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiTextDrawingTests
{
    [Fact]
    public void DrawTextRejectsNullLayout()
    {
        var context = CreateDrawingContext([]);

        Assert.Throws<ArgumentNullException>(() =>
            context.DrawText(Point.Zero, null!, 16, new Color(255, 255, 255)));
    }

    [Fact]
    public void DrawTextSkipsEmptyLayoutTransparentColorAndEmptyClip()
    {
        using var fonts = new TextFontContext();
        var commands = new List<UiDrawCommand>();
        var context = CreateDrawingContext(commands);

        context.DrawText(Point.Zero, new TextLayout(0, 0, TextBounds.Empty, []), 16,
            new Color(255, 255, 255));
        context.DrawText(Point.Zero, fonts.CreateLayout("A"), 16,
            new Color(255, 255, 255, 0));
        CreateDrawingContext(commands, isClipEmpty: true).DrawText(
            Point.Zero,
            fonts.CreateLayout("A"),
            16,
            new Color(255, 255, 255));

        Assert.Empty(commands);
    }

    [Fact]
    public void DrawTextKeepsLayoutAndDoesNotCullOverflowingInkByLayoutBox()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("A");
        var commands = new List<UiDrawCommand>();
        var context = CreateDrawingContext(
            commands,
            originX: 10,
            originY: 20,
            clip: new Rect(0, 0, 1, 1),
            opacity: 0.25);

        context.DrawText(new Point(200, 300), layout, 18, new Color(10, 20, 30, 128));

        var command = Assert.IsType<UiDrawTextCommand>(Assert.Single(commands));
        Assert.Same(layout, command.Layout);
        Assert.Equal(new Point(210, 320), command.Origin);
        Assert.Equal(18, command.FontSize);
        Assert.Equal(new Color(10, 20, 30, 128), command.Color);
        Assert.Equal(new Rect(0, 0, 1, 1), command.Clip);
        Assert.Equal(0.25, command.Opacity);
    }

    [Fact]
    public void BuilderUsesSnapshotScaleBaselineBearingAndAtlasUv()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("A");
        var glyph = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs[0];
        var descriptor = new TestDescriptorSet();
        var binding = new UiGlyphRenderBinding(
            7,
            descriptor,
            64,
            32,
            new GlyphAtlasRegion(2, 3, 5, 7),
            -1,
            6);
        var command = new UiDrawTextCommand(
            new Point(10, 20),
            layout,
            10,
            new Color(128, 64, 32, 128),
            null,
            0.5);
        var builder = new UiDrawBuilder();
        var resolvedKeys = new List<GlyphRasterKey>();

        builder.Build(
            new UiDrawCommandList([command], 1.5),
            400,
            300,
            true,
            glyphResolver: key =>
            {
                resolvedKeys.Add(key);
                return binding;
            });

        var key = Assert.Single(resolvedKeys);
        Assert.Same(Assert.Single(layout.Lines).GlyphRuns[0].FontFace, key.FontFace);
        Assert.Equal(15u, key.PixelSize);
        Assert.Equal(glyph.GlyphId, key.GlyphId);
        var penX = (10 + glyph.X + glyph.XOffset) * 1.5;
        var baselineY = (20 + glyph.Y + glyph.YOffset) * 1.5;
        var first = builder.Vertices[0];
        Assert.Equal((float)(penX - 1), first.X);
        Assert.Equal((float)(baselineY - 6), first.Y);
        Assert.Equal(2 / 64f, first.U);
        Assert.Equal(3 / 32f, first.V);
        Assert.Equal(2.5f / 64, first.ClampMinU);
        Assert.Equal(3.5f / 32, first.ClampMinV);
        Assert.Equal(6.5f / 64, first.ClampMaxU);
        Assert.Equal(9.5f / 32, first.ClampMaxV);
        Assert.Equal((float)(128 / 255.0 * 0.5), first.A);
        var batch = Assert.Single(builder.Batches.ToArray());
        Assert.Equal(UiMaterialKind.Text, batch.Material.Kind);
        Assert.Equal(7UL, batch.Material.ResourceId);
        Assert.Same(descriptor, batch.Material.DescriptorSet);
    }

    [Fact]
    public void BuilderMergesConsecutiveGlyphsOnOnePageAndSkipsPendingGlyphs()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("AB");
        var glyphs = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs;
        Assert.Equal(2, glyphs.Count);
        var descriptor = new TestDescriptorSet();
        var binding = new UiGlyphRenderBinding(
            11,
            descriptor,
            64,
            64,
            new GlyphAtlasRegion(1, 1, 4, 5),
            0,
            4);
        var command = new UiDrawTextCommand(
            Point.Zero,
            layout,
            16,
            new Color(255, 255, 255),
            null,
            1);
        var builder = new UiDrawBuilder();

        builder.Build(
            new UiDrawCommandList([command], 1),
            100,
            100,
            false,
            glyphResolver: _ => binding);

        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);

        builder.Build(
            new UiDrawCommandList([command], 1),
            100,
            100,
            false,
            glyphResolver: key => key.GlyphId == glyphs[0].GlyphId ? null : binding);

        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(6u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void BuilderPreservesMaterialOrderAndSeparatesAtlasPages()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("AB");
        var glyphs = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs;
        var firstDescriptor = new TestDescriptorSet();
        var secondDescriptor = new TestDescriptorSet();
        var imageDescriptor = new TestDescriptorSet();
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var text = new UiDrawTextCommand(
            Point.Zero,
            layout,
            16,
            new Color(255, 255, 255),
            null,
            1);
        var builder = new UiDrawBuilder();

        builder.Build(
            new UiDrawCommandList(
                [
                    new UiFillRectangleCommand(new Rect(0, 0, 1, 1), new Color(255, 0, 0), null, 1),
                    text,
                    new UiDrawImageCommand(new Rect(0, 0, 1, 1), image, image.FullSourceRect,
                        ImageSamplingMode.Linear, null, 1)
                ],
                1),
            100,
            100,
            false,
            _ => new UiImageRenderBinding(30, imageDescriptor),
            key => key.GlyphId == glyphs[0].GlyphId
                ? new UiGlyphRenderBinding(20, firstDescriptor, 64, 64,
                    new GlyphAtlasRegion(1, 1, 4, 5), 0, 4)
                : new UiGlyphRenderBinding(21, secondDescriptor, 64, 64,
                    new GlyphAtlasRegion(1, 1, 4, 5), 0, 4));

        Assert.Equal(
            [UiMaterialKind.Solid, UiMaterialKind.Text, UiMaterialKind.Text, UiMaterialKind.Image],
            builder.Batches.ToArray().Select(batch => batch.Material.Kind));
        Assert.Equal([0UL, 20UL, 21UL, 30UL],
            builder.Batches.ToArray().Select(batch => batch.Material.ResourceId));
    }

    private static UiDrawingContext CreateDrawingContext(
        List<UiDrawCommand> commands,
        double originX = 0,
        double originY = 0,
        Rect? clip = null,
        bool isClipEmpty = false,
        double opacity = 1) =>
        new(commands, new UiDrawingState(originX, originY, clip, isClipEmpty, opacity));

    private sealed class TextFontContext : IDisposable
    {
        private static readonly string FontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "SourceHanSansCN-Regular.otf");

        private readonly FontManager _fontManager = new();
        private readonly TextLayoutEngine _layoutEngine;
        private readonly Font _font;

        internal TextFontContext()
        {
            using var stream = File.OpenRead(FontPath);
            _font = Assert.Single(_fontManager.Register(stream, 0));
            _fontManager.DefaultFont = _font;
            _layoutEngine = new TextLayoutEngine(_fontManager);
        }

        internal TextLayout CreateLayout(string text) =>
            _layoutEngine.Layout(new TextLayoutRequest(
                text,
                _font,
                16,
                double.PositiveInfinity,
                1,
                TextWrapping.NoWrap,
                TextAlignment.Left));

        public void Dispose() => _fontManager.Destroy();
    }

    private sealed class TestDescriptorSet : DescriptorSet
    {
        public override void Destroy() => MarkDestroyed();
    }
}