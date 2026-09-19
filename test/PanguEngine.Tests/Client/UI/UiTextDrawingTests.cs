using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;
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
    public void DrawTextSkipsEmptyLayoutAndTransparentColor()
    {
        using var fonts = new TextFontContext();
        var commands = new List<UiDrawCommand>();
        var context = CreateDrawingContext(commands);

        context.DrawText(Point.Zero, new TextLayout(0, 0, TextBounds.Empty, []), 16,
            new Color(255, 255, 255));
        context.DrawText(Point.Zero, fonts.CreateLayout("A"), 16,
            new Color(255, 255, 255, 0));

        Assert.Empty(commands);
    }

    [Fact]
    public void DrawTextRecordsLocalOriginAndLayoutWithoutApplyingPendingState()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("A");
        var commands = new List<UiDrawCommand>();
        var context = CreateDrawingContext(commands);

        using (context.PushClip(new Rect(0, 0, 1, 1)))
        using (context.PushOpacity(0.25))
        {
            context.DrawText(new Point(200, 300), layout, 18, new Color(10, 20, 30, 128));
        }

        Assert.Collection(
            commands,
            command => Assert.Equal(
                new Rect(0, 0, 1, 1),
                Assert.IsType<UiPushClipCommand>(command).Clip),
            command => Assert.Equal(
                0.25,
                Assert.IsType<UiPushOpacityCommand>(command).Opacity),
            command =>
            {
                var text = Assert.IsType<UiDrawTextCommand>(command);
                Assert.Same(layout, text.Layout);
                Assert.Equal(new Point(200, 300), text.Origin);
                Assert.Equal(18, text.FontSize);
                Assert.Equal(new Color(10, 20, 30, 128), text.Color);
            },
            command => Assert.IsType<UiPopCommand>(command),
            command => Assert.IsType<UiPopCommand>(command));
    }

    [Fact]
    public void BuilderUsesEffectiveScaleAndTranslationForText()
    {
        using var fonts = new TextFontContext();
        var sourceRun = Assert.Single(Assert.Single(fonts.CreateLayout("A").Lines).GlyphRuns);
        var glyph = new PositionedGlyph(sourceRun.Glyphs[0].GlyphId, 0, 3, 5, 10, 0, 1, -2, false);
        var layout = new TextLayout(10, 20, TextBounds.Empty,
            [new TextLine(0, 1, 0, 0, 10, 20, 20, 5, [new TextGlyphRun(sourceRun.FontFace, 0, 1, [glyph])])]);
        var binding = new UiGlyphRenderBinding(
            7,
            64,
            32,
            new GlyphAtlasRegion(2, 3, 5, 7),
            -1,
            6);
        var command = new UiDrawTextCommand(
            new Point(7, 11),
            layout,
            10,
            new Color(128, 64, 32, 128));
        var builder = new UiDrawBuilder();
        var resolvedKeys = new List<GlyphRasterKey>();

        builder.Build(
            CommandList(
                Transform(new Point(5, 7), 1.5),
                Transform(new Point(2, 4), 2),
                Opacity(0.5),
                command,
                Pop,
                Pop,
                Pop),
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
        Assert.Equal(30u, key.PixelSize);
        Assert.Equal(glyph.GlyphId, key.GlyphId);
        var first = builder.Vertices[0];
        Assert.Equal(40f, first.X);
        Assert.Equal(49f, first.Y);
        Assert.Equal(45f, builder.Vertices[2].X);
        Assert.Equal(56f, builder.Vertices[2].Y);
        Assert.Equal(2 / 64f, first.U);
        Assert.Equal(3 / 32f, first.V);
        Assert.Equal(2.5f / 64, first.ClampMinU);
        Assert.Equal(3.5f / 32, first.ClampMinV);
        Assert.Equal(6.5f / 64, first.ClampMaxU);
        Assert.Equal(9.5f / 32, first.ClampMaxV);
        Assert.Equal((float)(128 / 255.0 * 0.5), first.A);
        Assert.Equal(PackMaterialData(UiMaterialKind.TextMask, 7), first.MaterialData);
        Assert.Single(builder.Batches.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteTransformsControlTextPositionAndRasterSizeUntilRestored(bool separateComponents)
    {
        using var fonts = new TextFontContext();
        var sourceRun = Assert.Single(Assert.Single(fonts.CreateLayout("A").Lines).GlyphRuns);
        var glyph = new PositionedGlyph(sourceRun.Glyphs[0].GlyphId, 0, 3, 5, 10, 0, 1, -2, false);
        var layout = new TextLayout(10, 20, TextBounds.Empty,
            [new TextLine(0, 1, 0, 0, 10, 20, 20, 5, [new TextGlyphRun(sourceRun.FontFace, 0, 1, [glyph])])]);
        var commands = new List<UiDrawCommand>();
        var context = CreateDrawingContext(commands);
        using (context.PushTransform(new Point(10, 20), 2))
        {
            using (separateComponents
                       ? context.SetTranslate(new Point(30, 40))
                       : context.SetTransform(new Point(30, 40)))
            using (separateComponents ? context.SetScale(1) : default(UiDrawingScope))
                context.DrawText(new Point(2, 3), layout, 10, new Color(255, 255, 255));
            context.DrawText(new Point(2, 3), layout, 10, new Color(255, 255, 255));
        }
        context.Complete();
        var builder = new UiDrawBuilder();
        var resolvedKeys = new List<GlyphRasterKey>();

        builder.Build(new UiDrawCommandList(commands), 100, 100, false, glyphResolver: key =>
        {
            resolvedKeys.Add(key);
            return new UiGlyphRenderBinding(1, 64, 32, new GlyphAtlasRegion(2, 3, 5, 7), -1, 6);
        });

        Assert.Equal(new uint[] { 10, 20 }, resolvedKeys.Select(key => key.PixelSize).ToArray());
        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(35f, builder.Vertices[0].X);
        Assert.Equal(40f, builder.Vertices[0].Y);
        Assert.Equal(40f, builder.Vertices[2].X);
        Assert.Equal(47f, builder.Vertices[2].Y);
        Assert.Equal(21f, builder.Vertices[4].X);
        Assert.Equal(26f, builder.Vertices[4].Y);
        Assert.Equal(26f, builder.Vertices[6].X);
        Assert.Equal(33f, builder.Vertices[6].Y);
    }

    [Fact]
    public void BuilderMergesConsecutiveGlyphsOnOnePageAndSkipsPendingGlyphs()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("AB");
        var glyphs = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs;
        Assert.Equal(2, glyphs.Count);
        var binding = new UiGlyphRenderBinding(
            11,
            64,
            64,
            new GlyphAtlasRegion(1, 1, 4, 5),
            0,
            4);
        var command = new UiDrawTextCommand(
            Point.Zero,
            layout,
            16,
            new Color(255, 255, 255));
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(command),
            100,
            100,
            false,
            glyphResolver: _ => binding);

        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);

        builder.Build(
            CommandList(command),
            100,
            100,
            false,
            glyphResolver: key => key.GlyphId == glyphs[0].GlyphId ? null : binding);

        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(6u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void BuilderDoesNotResolveGlyphsWhenOpacityZero()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("A");
        var resolvedKeys = new List<GlyphRasterKey>();
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Opacity(0),
                new UiDrawTextCommand(Point.Zero, layout, 16, new Color(255, 255, 255)),
                Pop),
            100,
            100,
            false,
            glyphResolver: key =>
            {
                resolvedKeys.Add(key);
                return new UiGlyphRenderBinding(
                    1,
                    64,
                    64,
                    new GlyphAtlasRegion(0, 0, 1, 1),
                    0,
                    0);
            });

        Assert.Empty(resolvedKeys);
        Assert.Empty(builder.Vertices.ToArray());
    }

    [Fact]
    public void BuilderDropsTextUnderEmptyClipWithoutResolvingGlyphs()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("A");
        var resolvedKeys = new List<GlyphRasterKey>();
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Clip(Rect.Zero),
                new UiDrawTextCommand(Point.Zero, layout, 16, new Color(255, 255, 255)),
                Pop),
            100,
            100,
            false,
            glyphResolver: key =>
            {
                resolvedKeys.Add(key);
                return new UiGlyphRenderBinding(
                    1,
                    64,
                    64,
                    new GlyphAtlasRegion(0, 0, 1, 1),
                    0,
                    0);
            });

        Assert.Empty(resolvedKeys);
        Assert.Empty(builder.Vertices.ToArray());
    }

    [Fact]
    public void BuilderPreservesRoundedScissorCullingForGlyphInk()
    {
        using var fonts = new TextFontContext();
        var sourceRun = Assert.Single(Assert.Single(fonts.CreateLayout("A").Lines).GlyphRuns);
        var glyph = new PositionedGlyph(sourceRun.Glyphs[0].GlyphId, 0, 0, 0, 1, 0, 0, 0, false);
        var layout = new TextLayout(1, 1, TextBounds.Empty,
            [new TextLine(0, 1, 0, 0, 1, 1, 1, 0, [new TextGlyphRun(sourceRun.FontFace, 0, 1, [glyph])])]);
        var builder = new UiDrawBuilder();
        var resolutions = 0;

        builder.Build(
            CommandList(
                Clip(new Rect(2.2, 0, 0.1, 10)),
                new UiDrawTextCommand(new Point(1.1, 0), layout, 16, new Color(255, 255, 255)),
                Pop),
            100, 100, false,
            glyphResolver: _ =>
            {
                resolutions++;
                return new UiGlyphRenderBinding(1, 8, 8, new GlyphAtlasRegion(0, 0, 1, 1), 0, 0);
            });

        Assert.Equal(1, resolutions);
        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(1.1f, builder.Vertices[0].X);
        Assert.Equal(2.1f, builder.Vertices[2].X);
        Assert.Equal(new UiScissor(2, 0, 1, 10), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void BuilderPreservesMaterialOrderAcrossTextureSlotsInOneBatch()
    {
        using var fonts = new TextFontContext();
        var layout = fonts.CreateLayout("AB");
        var glyphs = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs;
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var text = new UiDrawTextCommand(
            Point.Zero,
            layout,
            16,
            new Color(255, 255, 255));
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                new UiFillRectangleCommand(new Rect(0, 0, 1, 1), new Color(255, 0, 0)),
                text,
                new UiDrawImageCommand(new Rect(0, 0, 1, 1), image, image.FullSourceRect,
                    ImageSamplingMode.Linear)),
            100,
            100,
            false,
            _ => new UiImageRenderBinding(30, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1)),
            key => key.GlyphId == glyphs[0].GlyphId
                ? new UiGlyphRenderBinding(20, 64, 64,
                    new GlyphAtlasRegion(1, 1, 4, 5), 0, 4)
                : new UiGlyphRenderBinding(21, 64, 64,
                    new GlyphAtlasRegion(1, 1, 4, 5), 0, 4));

        Assert.Equal(24u, Assert.Single(builder.Batches.ToArray()).IndexCount);
        Assert.Equal(
            [
                PackMaterialData(UiMaterialKind.Solid, 0),
                PackMaterialData(UiMaterialKind.TextMask, 20),
                PackMaterialData(UiMaterialKind.TextMask, 21),
                PackMaterialData(UiMaterialKind.ImageLinear, 30)
            ],
            builder.Vertices.ToArray().Chunk(4).Select(vertices => vertices[0].MaterialData));
    }

    private static uint PackMaterialData(UiMaterialKind materialKind, uint textureIndex) =>
        (textureIndex << 8) | (uint)materialKind;

    private static UiDrawCommandList CommandList(params UiDrawCommand[] commands) =>
        new UiDrawCommandList([.. commands]);

    private static UiPushTransformCommand Transform(Point translation, double scale = 1) =>
        new(translation, scale);

    private static UiPushClipCommand Clip(Rect clip) =>
        new(clip);

    private static UiPushOpacityCommand Opacity(double opacity) =>
        new(opacity);

    private static UiDrawCommand Pop => UiPopCommand.Instance;

    private static UiDrawingContext CreateDrawingContext(List<UiDrawCommand> commands) =>
        new(commands);

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
}
