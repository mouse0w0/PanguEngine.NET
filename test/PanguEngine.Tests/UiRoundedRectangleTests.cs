using System.Numerics;
using System.Runtime.InteropServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Client.UI.Styling;
using Rectangle = PanguEngine.Client.UI.Controls.Rectangle;

namespace PanguEngine.Tests;

public sealed class UiRoundedRectangleTests
{
    [Fact]
    public void GpuStorageRecordsMatchShaderLayouts()
    {
        Assert.Equal(48, Marshal.SizeOf<UiGpuDrawData>());
        Assert.Equal(64, Marshal.SizeOf<UiGpuClipData>());
        Assert.Equal(32, Marshal.OffsetOf<UiGpuDrawData>(nameof(UiGpuDrawData.ClipIndex)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<UiGpuClipData>(nameof(UiGpuClipData.ParentIndex)).ToInt32());
        Assert.Equal(52, Marshal.OffsetOf<UiGpuClipData>(nameof(UiGpuClipData.Exclude)).ToInt32());
    }

    [Fact]
    public void EllipticalRectangleFillUsesOneQuadAndUpdatesPhysicalRadii()
    {
        var shape = new Rectangle { RadiusX = 8, RadiusY = 3 };
        var commands = Record(shape, 32);
        var builder = Build(commands);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(0, 0, 32, 32),
            new Point(8, 3), new Point(8, 3), new Point(8, 3), new Point(8, 3)));
        builder.Build(new UiDrawCommandList([
            new UiPushTransformCommand(new Point(2, 4), 1.5), .. commands, UiPopCommand.Instance]),
            128, 128, false);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(2, 4, 48, 48),
            new Point(12, 4.5), new Point(12, 4.5), new Point(12, 4.5), new Point(12, 4.5)));
        shape.RadiusY = 0;
        var square = Record(shape, 32);
        var squareBuilder = Build(square);
        AssertClipContainsShape(squareBuilder, new UiRoundedRectangle(new Rect(0, 0, 32, 32), CornerRadius.Zero));
        Assert.Equal(4, squareBuilder.VertexCount);
    }

    [Fact]
    public void RadiusChangesUpdateClipParameters()
    {
        var builder = new UiDrawBuilder();
        var fill = new UiFillRectangleCommand(new Rect(0, 0, 32, 32), new Color(255, 255, 255));
        builder.Build(new UiDrawCommandList([Clip(new Rect(0, 0, 32, 32), 8), fill, UiPopCommand.Instance]), 64, 64, false);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(0, 0, 32, 32), new CornerRadius(8)));
        builder.Build(new UiDrawCommandList([Clip(new Rect(0, 0, 32, 32), 16), fill, UiPopCommand.Instance]), 64, 64, false);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(0, 0, 32, 32), new CornerRadius(16)));
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(1024)]
    public void RoundedSolidFillUsesOneQuadWithoutCpuCoverage(double size)
    {
        var commands = new List<UiDrawCommand>();
        new UiDrawingContext(commands).FillRoundedRectangle(
            new UiRoundedClipGeometry(new UiRoundedRectangle(new Rect(0, 0, size, size),
                new CornerRadius(size / 2))), new SolidColorBrush(255, 255, 255));
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList(commands), 1200, 1200, false);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(6, builder.IndexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.NotEqual(0u, builder.Vertices[0].DrawDataIndex);
    }

    [Fact]
    public void SolidBackgroundAndBorderShareOneAnalyticDecoration()
    {
        var builder = Build(Record(new TestRegion
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(255, 0, 0, 128),
            BorderBrush = new SolidColorBrush(0, 255, 0, 192)
        }, 32));
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(1u, data.Decoration);
        Assert.Equal(128 / 255f, data.Background.W);
        Assert.Equal(192 / 255f, data.Border.W);
        Assert.NotEqual(0u, data.OuterIndex);
        Assert.NotEqual(0u, data.InnerIndex);
    }

    [Fact]
    public void CornerRadiusRejectsInvalidLengths()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CornerRadius(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CornerRadius(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CornerRadius(1, 2, double.PositiveInfinity, 4));
        Assert.Equal(8, new CornerRadius(2, 4, 6, 8).BottomLeft);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(128, 0)]
    [InlineData(0, 192)]
    public void RoundedRegionDecorationHandlesTransparentBrushes(byte backgroundAlpha, byte borderAlpha)
    {
        var builder = Build(Record(new TestRegion
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(255, 0, 0, backgroundAlpha),
            BorderBrush = new SolidColorBrush(0, 255, 0, borderAlpha)
        }, 32));
        Assert.Equal(0, builder.CellCoverageEvaluations);
        if (backgroundAlpha == 0 && borderAlpha == 0)
        {
            Assert.Equal(0, builder.VertexCount);
            return;
        }

        Assert.Equal(4, builder.VertexCount);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(1u, data.Decoration);
        Assert.Equal(backgroundAlpha == 0 ? Vector4.Zero : new Vector4(1, 0, 0, backgroundAlpha / 255f), data.Background);
        Assert.Equal(borderAlpha == 0 ? Vector4.Zero : new Vector4(0, 1, 0, borderAlpha / 255f), data.Border);
        Assert.NotEqual(0u, data.InnerIndex);
    }

    [Theory]
    [InlineData("2", 2, 2, 2, 2)]
    [InlineData("2px 4px", 2, 4, 2, 4)]
    [InlineData("2 4 6", 2, 4, 6, 4)]
    [InlineData("2 4 6 8", 2, 4, 6, 8)]
    public void CssExpandsCornersClockwise(string text, double tl, double tr, double br, double bl) =>
        Assert.Equal(new CornerRadius(tl, tr, br, bl), UiCssValueConverters.ParseCornerRadius(text));

    [Theory]
    [InlineData("20%")]
    [InlineData("2 / 4")]
    [InlineData("1 2 3 4 5")]
    public void CssRejectsUnsupportedSyntax(string text) =>
        Assert.Throws<FormatException>(() => UiCssValueConverters.ParseCornerRadius(text));

    [Fact]
    public void OversizedCornersShrinkTogether()
    {
        var rounded = new UiRoundedRectangle(new Rect(0, 0, 20, 10), new CornerRadius(20, 10, 10, 20));
        Assert.Equal(new Point(5, 5), rounded.TopLeft);
        Assert.Equal(new Point(2.5, 2.5), rounded.TopRight);
        Assert.False(rounded.Contains(new Point(0, 0)));
        Assert.True(rounded.Contains(new Point(10, 5)));
    }

    [Fact]
    public void UnequalBordersProduceNormalizedInnerEllipses()
    {
        var outer = new UiRoundedRectangle(new Rect(0, 0, 100, 100), new CornerRadius(50));
        var inner = outer.Deflate(new Rect(80, 2, 20, 96));
        Assert.Equal(Point.Zero, inner.TopLeft);
        Assert.True(inner.TopRight.X <= 20);
        Assert.True(inner.TopRight.Y + inner.BottomRight.Y <= 96);
    }

    [Fact]
    public void RectangleRadiusChangesItsHitGeometry()
    {
        var shape = new Rectangle();
        shape.Measure(new Size(40, 40));
        shape.Arrange(new Rect(0, 0, 40, 40));
        Assert.True(shape.Contains(new Point(1, 1)));
        shape.RadiusX = 20;
        shape.RadiusY = 20;
        shape.Measure(new Size(40, 40));
        shape.Arrange(new Rect(0, 0, 40, 40));
        Assert.False(shape.Contains(new Point(1, 1)));
        Assert.True(shape.Contains(new Point(20, 20)));
    }

    [Fact]
    public void DescendantsAreClippedOnlyWhenExplicitlyEnabled()
    {
        var parent = new TestRegion { CornerRadius = new CornerRadius(20) };
        var child = new TestRegion();
        parent.Add(child);
        parent.Measure(new Size(40, 40));
        parent.Arrange(new Rect(0, 0, 40, 40));
        Assert.Same(child, parent.HitTest(new Point(1, 1)));
        parent.ClipToBounds = true;
        Assert.Same(parent, parent.HitTest(new Point(1, 1)));
        Assert.Same(child, parent.HitTest(new Point(20, 20)));
        parent.CornerRadius = CornerRadius.Zero;
        Assert.Same(child, parent.HitTest(new Point(1, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void RepeatingTheSameClipPreservesCoverageAndOneGpuConstraint(double radius)
    {
        UiDrawCommand clip = radius == 0
            ? new UiPushClipCommand(new Rect(0, 0, 8, 8))
            : Clip(new Rect(0, 0, 8, 8), radius);
        var mesh = new UiTriangleMesh([new Point(0, 0), new Point(8, 0),
            new Point(8, 8), new Point(0, 8)], [0u, 1u, 2u, 0u, 2u, 3u]);
        var fill = new UiDrawGeometryCommand(mesh, new Color(255, 255, 255));
        var once = Build(clip, fill, UiPopCommand.Instance);
        var twice = Build(clip, clip, fill, UiPopCommand.Instance, UiPopCommand.Instance);
        Assert.NotEmpty(once.Vertices.ToArray());
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            Assert.Equal(SubmittedAlphaAt(once, x, y), SubmittedAlphaAt(twice, x, y), 6);

        var gpu = Build(clip, clip,
            new UiFillRectangleCommand(new Rect(0, 0, 8, 8), new Color(255, 255, 255)),
            UiPopCommand.Instance, UiPopCommand.Instance);
        Assert.Single(GetClips(gpu));
        AssertClipContainsShape(gpu, new UiRoundedRectangle(new Rect(0, 0, 8, 8), new CornerRadius(radius)));
        Assert.Equal(4, gpu.VertexCount);
        Assert.Equal(0, gpu.CellCoverageEvaluations);
    }

    [Fact]
    public void RoundedBorderSubmitsItsInnerContourAsAHole()
    {
        var outer = new UiRoundedRectangle(new Rect(0, 0, 12, 12), new CornerRadius(4));
        var inner = outer.Deflate(new Rect(2, 2, 8, 8));
        var builder = Build(
            new UiPushRoundedClipCommand(new UiRoundedClipGeometry(outer, inner)),
            new UiFillRectangleCommand(outer.Bounds, new Color(255, 255, 255)),
            UiPopCommand.Instance);
        AssertClipContainsShape(builder, inner, exclude: true);
        AssertClipContainsShape(builder, outer);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Fact]
    public void AbsoluteTransformDoesNotMoveAnEstablishedRoundedClip()
    {
        var builder = Build(
            new UiPushTransformCommand(new Point(10, 10), 1),
            Clip(new Rect(0, 0, 8, 8), 4),
            new UiSetTransformCommand(Point.Zero, 1),
            new UiFillRectangleCommand(new Rect(0, 0, 32, 32), new Color(255, 255, 255)),
            UiPopCommand.Instance, UiPopCommand.Instance, UiPopCommand.Instance);
        var clipIndex = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex].ClipIndex;
        AssertShape(builder.ClipData[(int)clipIndex],
            new UiRoundedRectangle(new Rect(10, 10, 8, 8), new CornerRadius(4)));
        Assert.Equal(10f, builder.Vertices[0].X);
        Assert.Equal(18f, builder.Vertices[2].X);
    }

    private static UiPushRoundedClipCommand Clip(Rect bounds, double radius) =>
        new(new UiRoundedClipGeometry(new UiRoundedRectangle(bounds, new CornerRadius(radius))));

    [Theory]
    [InlineData("radius-x: -1px")]
    [InlineData("radius-y: -2")]
    [InlineData("border-radius: -1")]
    public void NegativeCssRadiiUseStyleErrorWrapping(string declaration)
    {
        _ = new Rectangle();
        _ = new TestRegion();
        var rule = Assert.Single(UiStyleSheet.Parse($"* {{ {declaration}; }}").Rules);
        var type = declaration.StartsWith("border-radius", StringComparison.Ordinal) ? typeof(TestRegion) : typeof(Rectangle);
        Assert.Throws<UiStyleParseException>(() => rule.Bind(type));
    }

    [Fact]
    public void ExtremeBorderBackgroundStaysInsideOuterContour()
    {
        var outer = new UiRoundedRectangle(new Rect(0, 0, 100, 100), new CornerRadius(50));
        var inner = outer.Deflate(new Rect(80, 2, 20, 96));
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([
            new UiPushRoundedClipCommand(new UiRoundedClipGeometry(inner, constraint: outer)),
            new UiFillRectangleCommand(inner.Bounds, new Color(255, 255, 255)),
            UiPopCommand.Instance]), 128, 128, false);
        AssertClipContainsShape(builder, outer);
        AssertClipContainsShape(builder, inner);
        Assert.Equal(4, builder.VertexCount);
    }

    [Fact]
    public void EmptyAndExhaustedInnerBoundsHaveNoBackground()
    {
        var empty = new UiRoundedRectangle(Rect.Zero, new CornerRadius(10));
        Assert.Empty(empty.GetContour(1));
        var region = new TestRegion
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(30),
            Background = new SolidColorBrush(255, 0, 0),
            BorderBrush = new SolidColorBrush(0, 255, 0, 128)
        };
        var builder = Build(Record(region, 20));
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(0u, data.InnerIndex);
        Assert.Equal(Vector4.Zero, data.Background);
        Assert.Equal(new Vector4(0, 1, 0, 128 / 255f), data.Border);
    }

    [Fact]
    public void ExhaustedBackgroundAndZeroThicknessBorderProduceNoQuads()
    {
        var exhausted = Build(Record(new TestRegion
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(30),
            Background = new SolidColorBrush(255, 0, 0)
        }, 20));
        Assert.Equal(0, exhausted.VertexCount);
        var border = Build(Record(new TestRegion
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(255, 0, 0)
        }, 20));
        Assert.Equal(0, border.VertexCount);
    }

    [Fact]
    public void DescendantDrawingRespectsExplicitClipAndNestedHitBounds()
    {
        var outer = new TestRegion { CornerRadius = new CornerRadius(20) };
        var inner = new TestRegion { CornerRadius = new CornerRadius(10) };
        var child = new TestRegion { Background = new SolidColorBrush(255, 255, 255) };
        outer.Add(inner);
        inner.Add(child);
        Assert.Equal(1f, SubmittedAlphaAt(Build(Record(outer, 40)), 0, 0));
        Assert.Same(child, outer.HitTest(new Point(1, 1)));
        outer.ClipToBounds = true;
        inner.ClipToBounds = true;
        var clipped = Build(Record(outer, 40));
        Assert.NotEqual(0u, clipped.Vertices[0].DrawDataIndex);
        Assert.Contains(GetClips(clipped), data => data.RadiusX == new Vector4(20));
        Assert.Contains(GetClips(clipped), data => data.RadiusX == new Vector4(10));
        Assert.Equal(0, clipped.CellCoverageEvaluations);
        Assert.Same(outer, outer.HitTest(new Point(1, 1)));
        Assert.Same(child, outer.HitTest(new Point(20, 20)));
    }

    [Fact]
    public void DifferentRoundedAndRectangularClipsIntersectAndPopRestoresState()
    {
        var fill = new UiFillRectangleCommand(new Rect(0, 0, 32, 32), new Color(255, 255, 255));
        var builder = Build(Clip(new Rect(0, 0, 20, 20), 10),
            Clip(new Rect(10, 0, 20, 20), 10), new UiPushClipCommand(new Rect(0, 8, 32, 4)),
            fill, UiPopCommand.Instance, UiPopCommand.Instance, UiPopCommand.Instance);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(0, 8, 32, 4), CornerRadius.Zero));
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(10, 0, 20, 20), new CornerRadius(10)));
        AssertClipContainsShape(builder,
            new UiRoundedRectangle(new Rect(0, 0, 20, 20), new CornerRadius(10)));
        var restored = Build(Clip(new Rect(0, 0, 20, 20), 10),
            Clip(new Rect(40, 40, 4, 4), 2), fill, UiPopCommand.Instance,
            fill, UiPopCommand.Instance);
        Assert.Equal(1f, SubmittedAlphaAt(restored, 10, 10));
        Assert.Equal(4, restored.VertexCount);
        AssertClipContainsShape(restored, new UiRoundedRectangle(new Rect(0, 0, 20, 20), new CornerRadius(10)));
    }

    [Fact]
    public void ChangingClipRadiusInvalidatesMeshCoverage()
    {
        var mesh = new UiTriangleMesh([new Point(0, 0), new Point(20, 0),
            new Point(20, 20), new Point(0, 20)], [0u, 1u, 2u, 0u, 2u, 3u]);
        var command = new UiDrawGeometryCommand(mesh, new Color(255, 255, 255));
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([Clip(new Rect(0, 0, 20, 20), 2), command,
            UiPopCommand.Instance]), 64, 64, false);
        var smallRadius = SubmittedAlphaAt(builder, 1, 1);
        builder.Build(new UiDrawCommandList([Clip(new Rect(0, 0, 20, 20), 10), command,
            UiPopCommand.Instance]), 64, 64, false);
        Assert.True(smallRadius > SubmittedAlphaAt(builder, 1, 1));
        Assert.Equal(1f, SubmittedAlphaAt(builder, 10, 10));
        Assert.All(builder.Vertices.ToArray(), vertex => Assert.Equal(0u, vertex.DrawDataIndex));
        Assert.True(builder.CellCoverageEvaluations > 0);
    }

    [Fact]
    public void RectangleColorsAndRadiiUpdateDrawing()
    {
        var shape = new Rectangle { RadiusX = 10, RadiusY = 10,
            Stroke = new SolidColorBrush(255, 0, 0), StrokeThickness = 2 };
        var initial = Build(Record(shape, 20));
        var initialData = initial.DrawData[(int)initial.Vertices[0].DrawDataIndex];
        shape.Fill = new SolidColorBrush(0, 255, 0);
        var recolored = Build(Record(shape, 20));
        var recoloredData = recolored.DrawData[(int)recolored.Vertices[0].DrawDataIndex];
        Assert.Equal(new Vector4(0, 1, 0, 1), recoloredData.Background);
        Assert.Equal(initialData.Border, recoloredData.Border);
        shape.RadiusX = 2;
        var changed = Build(Record(shape, 20));
        var changedData = changed.DrawData[(int)changed.Vertices[0].DrawDataIndex];
        Assert.NotEqual(initial.ClipData[(int)initialData.OuterIndex].RadiusX,
            changed.ClipData[(int)changedData.OuterIndex].RadiusX);
        Assert.Equal(4, changed.VertexCount);
    }

    [Theory]
    [InlineData(32, false)]
    [InlineData(1024, false)]
    [InlineData(32, true)]
    [InlineData(1024, true)]
    public void EllipticalRectangleUsesBoundedQuadsAndSkipsLargeStrokeHoles(double size, bool filled)
    {
        var commands = Record(new Rectangle
        {
            RadiusX = 12, RadiusY = 5, Fill = filled ? new SolidColorBrush(32, 53, 83) : null,
            Stroke = new SolidColorBrush(79, 141, 245), StrokeThickness = 4
        }, size);
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([.. commands]), 1200, 1200, false);
        var split = !filled && size == 1024;
        Assert.Equal(split ? 16 : 4, builder.VertexCount);
        Assert.Equal(split ? 24 : 6, builder.IndexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Single(builder.Batches.ToArray());
        if (split)
            Assert.Equal(0f, SubmittedAlphaAt(builder, 512, 512));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EllipticalStrokeScalesWidthAndCenterlineAndRetainsEstablishedClip(bool filled)
    {
        var commands = Record(new Rectangle
        {
            RadiusX = 8, RadiusY = 3, Fill = filled ? new SolidColorBrush(0, 255, 0, 192) : null,
            Stroke = new SolidColorBrush(255, 0, 0, 128), StrokeThickness = 4
        }, 32);
        var builder = Build([Clip(new Rect(0, 0, 32, 32), 8),
            new UiSetTransformCommand(new Point(2, 4), 1.5), .. commands,
            UiPopCommand.Instance, UiPopCommand.Instance]);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(2u, data.Decoration);
        Assert.Equal(BitConverter.SingleToUInt32Bits(3f), data.InnerIndex);
        Assert.Equal(filled ? new Vector4(0, 1, 0, 192 / 255f) : Vector4.Zero, data.Background);
        Assert.Equal(128 / 255f, data.Border.W);
        AssertShape(builder.ClipData[(int)data.OuterIndex], new UiRoundedRectangle(new Rect(5, 7, 42, 42),
            new Point(12, 4.5), new Point(12, 4.5), new Point(12, 4.5), new Point(12, 4.5)));
        AssertShape(builder.ClipData[(int)data.ClipIndex], new UiRoundedRectangle(new Rect(0, 0, 32, 32), new CornerRadius(8)));
    }

    [Theory]
    [InlineData(8, 3, 32)]
    [InlineData(0, 0, 32)]
    public void CollapsedRectangleStrokesRetainGeometry(double radiusX, double radiusY, double thickness)
    {
        var commands = Record(new Rectangle
        {
            RadiusX = radiusX, RadiusY = radiusY, Fill = null,
            Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = thickness,
            StrokeLineJoin = StrokeLineJoin.Bevel
        }, 32);
        Assert.Equal(0, Build(commands).VertexCount);
    }

    [Theory]
    [InlineData(StrokeLineJoin.Miter, 4, 3)]
    [InlineData(StrokeLineJoin.Miter, 1, 4)]
    [InlineData(StrokeLineJoin.Bevel, 4, 4)]
    [InlineData(StrokeLineJoin.Round, 4, 5)]
    public void StraightRectangleUsesAnalyticStrokeAndPreservesJoin(StrokeLineJoin join, double miterLimit, uint mode)
    {
        var commands = Record(new Rectangle
        {
            Fill = new SolidColorBrush(255, 0, 0, 128), Stroke = new SolidColorBrush(0, 0, 255, 192),
            StrokeThickness = 4, StrokeLineJoin = join, StrokeMiterLimit = miterLimit
        }, 32);
        var builder = Build(commands);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(6, builder.IndexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(mode, data.Decoration);
        AssertShape(builder.ClipData[(int)data.OuterIndex], new UiRoundedRectangle(new Rect(2, 2, 28, 28), CornerRadius.Zero));
        Assert.Equal(BitConverter.SingleToUInt32Bits(2f), data.InnerIndex);
        Assert.Equal(128 / 255f, data.Background.W);
        Assert.Equal(192 / 255f, data.Border.W);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(8, 0)]
    public void AZeroRadiusAxisUsesStraightRectangleGpuPath(double radiusX, double radiusY)
    {
        var commands = Record(new Rectangle
        {
            RadiusX = radiusX, RadiusY = radiusY, Fill = null,
            Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = 4
        }, 32);
        var builder = Build(commands);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Equal(3u, builder.DrawData[(int)builder.Vertices[0].DrawDataIndex].Decoration);
    }

    [Theory]
    [InlineData(0, 0, StrokeLineJoin.Miter)]
    [InlineData(0, 0, StrokeLineJoin.Bevel)]
    [InlineData(0, 0, StrokeLineJoin.Round)]
    [InlineData(12, 5, StrokeLineJoin.Miter)]
    public void StrokeOnlyRectangleQuadsSkipSafeHoleWithoutOverlap(double radiusX, double radiusY, StrokeLineJoin join)
    {
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([.. Record(new Rectangle
        {
            RadiusX = radiusX, RadiusY = radiusY, Fill = null,
            Stroke = new SolidColorBrush(255, 255, 255, 128), StrokeThickness = 4, StrokeLineJoin = join
        }, 128)]), 160, 160, false);
        Assert.Equal(16, builder.VertexCount);
        Assert.Equal(24, builder.IndexCount);
        Assert.Single(builder.Batches.ToArray());
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Equal(0f, SubmittedAlphaAt(builder, 64, 64));
        for (var y = 0; y < 130; y++)
        for (var x = 0; x < 130; x++)
            Assert.InRange(SubmittedAlphaAt(builder, x, y), 0, 1);
        foreach (var vertex in builder.Vertices)
            Assert.Equal(builder.Vertices[0].DrawDataIndex, vertex.DrawDataIndex);
    }

    [Fact]
    public void AViewportEntirelyInsideTheStrokeHoleSubmitsNoQuads()
    {
        var builder = Build([new UiPushTransformCommand(new Point(-400, -400), 1),
            .. Record(new Rectangle
            {
                RadiusX = 12, RadiusY = 5, Fill = null,
                Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = 4
            }, 1024), UiPopCommand.Instance]);
        Assert.Equal(0, builder.VertexCount);
        Assert.Equal(0, builder.IndexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Fact]
    public void ALineCenterlineRetainsItsGeometryStroke()
    {
        var shape = new Rectangle
        {
            RadiusX = 8, RadiusY = 3, Fill = null,
            Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = 32
        };
        shape.Measure(new Size(32, 64));
        shape.Arrange(new Rect(0, 0, 32, 64));
        var commands = new List<UiDrawCommand>();
        shape.AppendDrawCommands(commands, 1);
        var builder = Build([.. commands]);
        Assert.True(builder.VertexCount > 0);
        Assert.True(builder.CellCoverageEvaluations > 0);
        Assert.All(builder.Vertices.ToArray(), vertex => Assert.Equal(0u, vertex.DrawDataIndex));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroWidthRoundedStrokeSubmitsOnlyItsFill(bool filled)
    {
        var builder = Build(Record(new Rectangle
        {
            RadiusX = 8, RadiusY = 3, Fill = filled ? new SolidColorBrush(255, 0, 0) : null,
            Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = 0
        }, 32));
        Assert.Equal(filled ? 4 : 0, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Theory]
    [InlineData(StrokeLineJoin.Miter)]
    [InlineData(StrokeLineJoin.Round)]
    [InlineData(StrokeLineJoin.Bevel)]
    public void ThickRoundedStrokeRetainsNormalizedCenterlineAndWidth(StrokeLineJoin join)
    {
        var commands = Record(new Rectangle
        {
            RadiusX = 12, RadiusY = 5, Fill = null,
            Stroke = new SolidColorBrush(255, 255, 255), StrokeThickness = 24, StrokeLineJoin = join
        }, 32);
        var builder = Build(commands);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        var radius = new Point(12, 5);
        AssertShape(builder.ClipData[(int)data.OuterIndex],
            new UiRoundedRectangle(new Rect(12, 12, 8, 8), radius, radius, radius, radius));
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Equal(BitConverter.SingleToUInt32Bits(12f), builder.DrawData[(int)builder.Vertices[0].DrawDataIndex].InnerIndex);
    }

    [Fact]
    public void RoundedShapeJointDrawRetainsIndependentColorsAndOpacity()
    {
        var commands = Record(new Rectangle
        {
            RadiusX = 8, RadiusY = 3, Opacity = 0.5,
            Fill = new SolidColorBrush(255, 0, 0, 128),
            Stroke = new SolidColorBrush(0, 0, 255, 192), StrokeThickness = 4
        }, 32);
        var builder = Build(commands);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(6, builder.IndexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(2u, data.Decoration);
        Assert.Equal(new Vector4(1, 0, 0, 64 / 255f), data.Background);
        Assert.Equal(new Vector4(0, 0, 1, 96 / 255f), data.Border);
        Assert.Equal(BitConverter.SingleToUInt32Bits(2f), data.InnerIndex);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(128, 0, 4)]
    [InlineData(0, 192, 4)]
    public void RoundedShapeJointDrawHandlesTransparentBrushes(byte fillAlpha, byte strokeAlpha, int vertexCount)
    {
        var builder = Build(Record(new Rectangle
        {
            RadiusX = 8, RadiusY = 3, Fill = new SolidColorBrush(255, 0, 0, fillAlpha),
            Stroke = new SolidColorBrush(0, 0, 255, strokeAlpha), StrokeThickness = 4
        }, 32));
        Assert.Equal(vertexCount, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Theory]
    [InlineData(ImageSamplingMode.Nearest, 0)]
    [InlineData(ImageSamplingMode.Nearest, 4)]
    [InlineData(ImageSamplingMode.Linear, 0)]
    [InlineData(ImageSamplingMode.Linear, 4)]
    public void ClippedImagesRetainOriginalUvAndClamp(ImageSamplingMode sampling, double radius)
    {
        var image = UiImage.FromRgba(new byte[64], 4, 4);
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([
            radius == 0 ? new UiPushClipCommand(new Rect(0, 0, 8, 8)) : Clip(new Rect(0, 0, 8, 8), radius),
            new UiDrawImageCommand(new Rect(-2, 0, 8, 8), image, new Rect(1, 1, 2, 2), sampling),
            UiPopCommand.Instance]), 64, 64, false,
            _ => new UiImageRenderBinding(17, 16, 16, new UiImageAtlasRegion(4, 8, 4, 4)));
        Assert.NotEmpty(builder.Vertices.ToArray());
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        foreach (var vertex in builder.Vertices)
        {
            Assert.Equal((5 + (vertex.X + 2) / 4) / 16, vertex.U, 6);
            Assert.Equal((9 + vertex.Y / 4) / 16, vertex.V, 6);
            Assert.Equal(5.5f / 16, vertex.ClampMinU);
            Assert.Equal(9.5f / 16, vertex.ClampMinV);
            Assert.Equal(6.5f / 16, vertex.ClampMaxU);
            Assert.Equal(10.5f / 16, vertex.ClampMaxV);
            Assert.Equal(sampling == ImageSamplingMode.Nearest ? UiMaterialKind.ImageNearest : UiMaterialKind.ImageLinear,
                vertex.MaterialKind);
            Assert.Equal(17u, vertex.TextureIndex);
        }
    }

    [Theory]
    [InlineData(ImageSamplingMode.Nearest, false)]
    [InlineData(ImageSamplingMode.Linear, false)]
    [InlineData(ImageSamplingMode.Nearest, true)]
    [InlineData(ImageSamplingMode.Linear, true)]
    public void NineSliceFractionalPartitionsCoverTheInteriorOnce(ImageSamplingMode sampling, bool border)
    {
        var image = UiImage.FromRgba(new byte[256], 8, 8);
        var brush = new NineSliceImageBrush(image, new ImageSlice(2), samplingMode: sampling);
        var outer = new UiRoundedRectangle(new Rect(0.25, 0.25, 20, 20), new CornerRadius(6));
        var geometry = new UiRoundedClipGeometry(outer, border ? outer.Deflate(new Rect(3.5, 3.5, 13.5, 13.5)) : null);
        var commands = new List<UiDrawCommand>();
        var context = new UiDrawingContext(commands);
        using (context.PushTransform(Point.Zero, 1.25))
        {
            if (border)
                context.FillRoundedBorder(geometry, brush);
            else
                context.FillRoundedRectangle(geometry, brush);
        }
        var textured = new UiDrawBuilder();
        textured.Build(new UiDrawCommandList(commands), 64, 64, false,
            _ => new UiImageRenderBinding(1, 8, 8, new UiImageAtlasRegion(0, 0, 8, 8)));
        Assert.InRange(textured.VertexCount, 4, 36);
        Assert.Equal(0, textured.CellCoverageEvaluations);
        for (var y = 0; y < 28; y++)
        for (var x = 0; x < 28; x++)
        {
            var submittedAlpha = SubmittedAlphaAt(textured, x, y);
            Assert.InRange(submittedAlpha, 0, 1);
            var local = new Point((x + 0.5) / 1.25, (y + 0.5) / 1.25);
            if (outer.Contains(local) && !(geometry.Inner?.Contains(local) ?? false))
                Assert.Equal(1f, submittedAlpha);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void NineSlicePartitionsPreserveFractionalAncestorClipEdges(bool horizontal, bool clipAtStart)
    {
        var image = UiImage.FromRgba(new byte[256], 8, 8);
        var brush = new NineSliceImageBrush(image, new ImageSlice(2));
        var origin = clipAtStart ? 0.75 : 0.25;
        var outer = new UiRoundedRectangle(new Rect(origin, origin, 20, 20), new CornerRadius(1));
        var ancestor = horizontal
            ? clipAtStart ? new Rect(2.8, 0, 29.2, 32) : new Rect(0, 0, 2.2, 32)
            : clipAtStart ? new Rect(0, 2.8, 32, 29.2) : new Rect(0, 0, 32, 2.2);
        var commands = new List<UiDrawCommand>();
        var context = new UiDrawingContext(commands);
        using (context.PushClip(ancestor))
            context.FillRoundedRectangle(new UiRoundedClipGeometry(outer), brush);
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList(commands), 64, 64, false,
            _ => new UiImageRenderBinding(1, 8, 8, new UiImageAtlasRegion(0, 0, 8, 8)));

        var x = horizontal ? 2 : 10;
        var y = horizontal ? 10 : 2;
        var vertexIndex = Assert.Single(Enumerable.Range(0, builder.VertexCount / 4)
            .Select(index => index * 4)
            .Where(index => x + 0.5 >= builder.Vertices[index].X && x + 0.5 < builder.Vertices[index + 2].X &&
                y + 0.5 >= builder.Vertices[index].Y && y + 0.5 < builder.Vertices[index + 2].Y));
        Assert.Equal(1f, SubmittedAlphaAt(builder, x, y));
        AssertClipContainsShape(builder, new UiRoundedRectangle(ancestor, CornerRadius.Zero), vertexIndex: vertexIndex);
        for (var i = vertexIndex; i < vertexIndex + 4; i++)
        {
            var vertex = builder.Vertices[i];
            var coordinate = horizontal ? vertex.X : vertex.Y;
            var source = clipAtStart ? coordinate - origin : 2 + (coordinate - 2.25) / 4;
            Assert.Equal(source / 8, horizontal ? vertex.U : vertex.V, 6);
            Assert.Equal((clipAtStart ? 0.5f : 2.5f) / 8, horizontal ? vertex.ClampMinU : vertex.ClampMinV);
            Assert.Equal((clipAtStart ? 1.5f : 5.5f) / 8, horizontal ? vertex.ClampMaxU : vertex.ClampMaxV);
        }
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Single(builder.Batches.ToArray());
    }

    private static UiDrawCommand[] Record(UiNode node, double size)
    {
        node.Measure(new Size(size, size));
        node.Arrange(new Rect(0, 0, size, size));
        var commands = new List<UiDrawCommand>();
        node.AppendDrawCommands(commands, 1);
        return commands.ToArray();
    }

    [Theory]
    [InlineData(ImageStretch.None)]
    [InlineData(ImageStretch.Fill)]
    [InlineData(ImageStretch.Uniform)]
    [InlineData(ImageStretch.UniformToFill)]
    public void RoundedBrushRetainsStretchDestinationAndSource(ImageStretch stretch)
    {
        var image = UiImage.FromRgba(new byte[128], 8, 4);
        var brush = new ImageBrush(image, stretch);
        var bounds = new Rect(0, 0, 20, 20);
        var square = new List<UiDrawCommand>();
        new UiDrawingContext(square).FillRectangle(bounds, brush);
        var rounded = new List<UiDrawCommand>();
        new UiDrawingContext(rounded).FillRoundedRectangle(
            new UiRoundedClipGeometry(new UiRoundedRectangle(bounds, new CornerRadius(6))), brush);
        var expected = Assert.Single(square.OfType<UiDrawImageCommand>());
        var actual = Assert.Single(rounded.OfType<UiDrawImageCommand>());
        Assert.Equal(expected.Bounds, actual.Bounds);
        Assert.Equal(expected.SourceRect, actual.SourceRect);
        Assert.Equal(expected.SamplingMode, actual.SamplingMode);
    }

    [Fact]
    public void ReusedBuilderDoesNotRetainPreviousImageClip()
    {
        var image = UiImage.FromRgba(new byte[64], 4, 4);
        var commands = new UiDrawCommandList([Clip(new Rect(0, 0, 20, 20), 6),
            new UiDrawImageCommand(new Rect(0, 0, 8, 20), image, image.FullSourceRect, ImageSamplingMode.Linear),
            new UiDrawImageCommand(new Rect(8, 0, 12, 20), image, image.FullSourceRect, ImageSamplingMode.Linear),
            UiPopCommand.Instance]);
        var builder = new UiDrawBuilder();
        UiImageResolver resolver = _ => new UiImageRenderBinding(1, 4, 4, new UiImageAtlasRegion(0, 0, 4, 4));
        builder.Build(commands, 64, 64, false, resolver);
        Assert.Equal(8, builder.VertexCount);
        builder.Build(new UiDrawCommandList([
            new UiDrawImageCommand(new Rect(24, 0, 4, 4), image, image.FullSourceRect, ImageSamplingMode.Linear)
        ]), 64, 64, false, resolver);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(24f, builder.Vertices[0].X);
        Assert.Equal(28f, builder.Vertices[2].X);
        Assert.Equal(0u, builder.DrawData[(int)builder.Vertices[0].DrawDataIndex].ClipIndex);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Fact]
    public void RectangularAndRoundedClipsShareOneParameterBatch()
    {
        var builder = Build(new UiPushClipCommand(new Rect(4, 4, 56, 56)),
            new UiFillRectangleCommand(new Rect(8, 8, 4, 4), new Color(255, 0, 0)),
            Clip(new Rect(16, 16, 16, 16), 8),
            new UiFillRectangleCommand(new Rect(0, 0, 64, 64), new Color(0, 255, 0)), UiPopCommand.Instance,
            Clip(new Rect(36, 16, 16, 16), 8),
            new UiFillRectangleCommand(new Rect(0, 0, 64, 64), new Color(0, 0, 255)), UiPopCommand.Instance,
            UiPopCommand.Instance);
        var batch = Assert.Single(builder.Batches.ToArray());
        Assert.Equal(new UiScissor(0, 0, 64, 64), batch.Scissor);
        Assert.Equal((uint)builder.IndexCount, batch.IndexCount);
        Assert.Equal(12, builder.VertexCount);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(16, 16, 16, 16), new CornerRadius(8)), vertexIndex: 4);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(36, 16, 16, 16), new CornerRadius(8)), vertexIndex: 8);
        Assert.Equal(0, builder.CellCoverageEvaluations);
        Assert.Equal(0f, SubmittedAlphaAt(builder, 4, 4));
        Assert.Equal(1f, SubmittedAlphaAt(builder, 24, 24));
        Assert.Equal(1f, SubmittedAlphaAt(builder, 44, 24));
    }

    [Fact]
    public void RectangleInsideRoundedClipIsBakedIntoGeometry()
    {
        var builder = Build(Clip(new Rect(0, 0, 32, 32), 8),
            new UiPushClipCommand(new Rect(12, 12, 4, 4)),
            new UiFillRectangleCommand(new Rect(0, 0, 32, 32), new Color(255, 255, 255)),
            UiPopCommand.Instance, UiPopCommand.Instance);
        Assert.Equal(new UiScissor(0, 0, 64, 64), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(1f, SubmittedAlphaAt(builder, 14, 14));
        Assert.Equal(0f, SubmittedAlphaAt(builder, 10, 14));
        Assert.Equal(12f, builder.Vertices[0].X);
        Assert.Equal(16f, builder.Vertices[2].X);
    }

    [Fact]
    public void BakedRectangleClipKeepsImageUvAndAtlasClamp()
    {
        var image = UiImage.FromRgba(new byte[64], 4, 4);
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([
            Clip(new Rect(0, 0, 32, 32), 8),
            new UiPushClipCommand(new Rect(12, 12, 4, 4)),
            new UiDrawImageCommand(new Rect(0, 0, 32, 32), image, image.FullSourceRect, ImageSamplingMode.Linear),
            UiPopCommand.Instance, UiPopCommand.Instance]), 64, 64, false,
            _ => new UiImageRenderBinding(1, 4, 4, new UiImageAtlasRegion(0, 0, 4, 4)));
        Assert.Equal(4, builder.VertexCount);
        Assert.Equal(12f, builder.Vertices[0].X);
        Assert.Equal(0.375f, builder.Vertices[0].U);
        Assert.Equal(0.375f, builder.Vertices[0].V);
        Assert.Equal(0.5f, builder.Vertices[2].U);
        Assert.Equal(0.5f, builder.Vertices[2].V);
        Assert.Equal(0.125f, builder.Vertices[0].ClampMinU);
        Assert.Equal(0.875f, builder.Vertices[0].ClampMaxU);
        Assert.Equal(new UiScissor(0, 0, 64, 64), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void StraightMiddleHeightDoesNotMultiplyRoundedDecorationVertices()
    {
        var shortCount = BuildDecoration(96).VertexCount;
        var tallCount = BuildDecoration(1024).VertexCount;
        Assert.Equal(8, shortCount);
        Assert.Equal(shortCount, tallCount);

        static UiDrawBuilder BuildDecoration(double height)
        {
            var outer = new UiRoundedRectangle(new Rect(0, 0, 160, height), new CornerRadius(24));
            var inner = outer.Deflate(new Rect(6, 6, 148, height - 12));
            var builder = new UiDrawBuilder();
            builder.Build(new UiDrawCommandList([
                new UiPushRoundedClipCommand(new UiRoundedClipGeometry(inner, constraint: outer)),
                new UiFillRectangleCommand(inner.Bounds, new Color(32, 53, 83)), UiPopCommand.Instance,
                new UiPushRoundedClipCommand(new UiRoundedClipGeometry(outer, inner)),
                new UiFillRectangleCommand(outer.Bounds, new Color(79, 141, 245)), UiPopCommand.Instance
            ]), 256, 1200, false);
            return builder;
        }
    }

    [Fact]
    public void PhysicalIntersectionTracksParentAndTransformChanges()
    {
        var geometry = new UiRoundedClipGeometry(
            new UiRoundedRectangle(new Rect(0, 0, 20, 20), new CornerRadius(6)));
        var parent = new UiConvexClip(UiConvexClip.Rectangle(new Rect(0, 0, 16, 20)));
        var first = geometry.GetPhysicalClip(1, 0, 0, parent);
        var equivalentParent = new UiConvexClip(parent.Outer.ToArray());
        Assert.True(geometry.GetPhysicalClip(1, 0, 0, equivalentParent).Contains(new Rect(10, 8, 2, 2)));
        var narrower = new UiConvexClip(UiConvexClip.Rectangle(new Rect(0, 0, 8, 20)));
        var changed = geometry.GetPhysicalClip(1, 0, 0, narrower);
        Assert.True(first.Contains(new Rect(10, 8, 2, 2)));
        Assert.False(changed.Contains(new Rect(10, 8, 2, 2)));
        Assert.True(changed.Contains(new Rect(4, 8, 2, 2)));
        Assert.False(geometry.GetPhysicalClip(1, 10, 0, narrower).Contains(new Rect(4, 8, 2, 2)));
    }

    [Fact]
    public void BorderOnlyDecorationUsesNonOverlappingQuadsAroundItsSafeHole()
    {
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList(Record(new TestRegion
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(255, 255, 255, 128)
        }, 128).ToList()), 160, 160, false);
        Assert.Equal(16, builder.VertexCount);
        var draw = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(128 / 255f, draw.Border.W);
        Assert.Equal(0f, draw.Background.W);
        for (var y = 0; y < 130; y++)
        for (var x = 0; x < 130; x++)
            Assert.InRange(SubmittedAlphaAt(builder, x, y), 0, 1);
        Assert.Equal(0f, SubmittedAlphaAt(builder, 64, 64));
        Assert.All(builder.Vertices.ToArray(), vertex =>
            Assert.Equal(builder.Vertices[0].DrawDataIndex, vertex.DrawDataIndex));
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    private static UiDrawBuilder Build(params UiDrawCommand[] commands)
    {
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([.. commands]), 64, 64, false);
        return builder;
    }

    [Fact]
    public void RectangularClipMatchesTheZeroRadiusGpuClip()
    {
        var bounds = new Rect(1.25, 2.5, 20.75, 21.25);
        var fill = new UiFillRectangleCommand(new Rect(0, 0, 32, 32), new Color(255, 255, 255));
        var rectangle = Build(new UiPushClipCommand(bounds), fill, UiPopCommand.Instance);
        var rounded = Build(Clip(bounds, 0), fill, UiPopCommand.Instance);
        Assert.Equal(rounded.Vertices.ToArray().Select(vertex => (vertex.X, vertex.Y, vertex.A)),
            rectangle.Vertices.ToArray().Select(vertex => (vertex.X, vertex.Y, vertex.A)));
        AssertClipContainsShape(rectangle, new UiRoundedRectangle(bounds, CornerRadius.Zero));
        AssertClipContainsShape(rounded, new UiRoundedRectangle(bounds, CornerRadius.Zero));
        Assert.Equal(0, rectangle.CellCoverageEvaluations);
    }

    [Fact]
    public void RectangularAndRoundedClipsShareBatchWithoutReorderingMaterials()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();
        builder.Build(new UiDrawCommandList([
            new UiPushClipCommand(new Rect(0, 0, 8, 8)),
            new UiFillRectangleCommand(new Rect(0, 0, 8, 8), new Color(255, 0, 0)), UiPopCommand.Instance,
            Clip(new Rect(12, 0, 8, 8), 4),
            new UiDrawImageCommand(new Rect(12, 0, 8, 8), image, image.FullSourceRect, ImageSamplingMode.Linear),
            UiPopCommand.Instance,
            new UiFillRectangleCommand(new Rect(24, 0, 4, 4), new Color(0, 255, 0))
        ]), 64, 64, false, _ => new UiImageRenderBinding(1, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1)));
        Assert.Equal(12, builder.VertexCount);
        Assert.Equal(18u, Assert.Single(builder.Batches.ToArray()).IndexCount);
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(0, 0, 8, 8), CornerRadius.Zero));
        AssertClipContainsShape(builder, new UiRoundedRectangle(new Rect(12, 0, 8, 8), new CornerRadius(4)), vertexIndex: 4);
        Assert.Equal(0u, builder.Vertices[8].DrawDataIndex);
        Assert.Equal(UiMaterialKind.Solid, builder.Vertices[0].MaterialKind);
        Assert.Equal(UiMaterialKind.ImageLinear, builder.Vertices[4].MaterialKind);
        Assert.Equal(1u, builder.Vertices[4].TextureIndex);
        Assert.Equal(UiMaterialKind.Solid, builder.Vertices[8].MaterialKind);
        Assert.Equal(1f, builder.Vertices[8].G);
        Assert.Equal(0, builder.CellCoverageEvaluations);
    }

    [Fact]
    public void RectangularClipKeepsCpuGeometryCoverageAndSharesTheAnalyticBatch()
    {
        var mesh = new UiTriangleMesh([new Point(0, 0), new Point(4, 0),
            new Point(4, 4), new Point(0, 4)], [0u, 1u, 2u, 0u, 2u, 3u]);
        var builder = Build(new UiPushClipCommand(new Rect(0.25, 0.25, 1.5, 1.5)),
            new UiFillRectangleCommand(new Rect(0, 0, 4, 4), new Color(255, 0, 0)),
            new UiDrawGeometryCommand(mesh, new Color(0, 255, 0)), UiPopCommand.Instance,
            new UiFillRectangleCommand(new Rect(8, 8, 2, 2), new Color(0, 0, 255)));
        Assert.NotEqual(0u, builder.Vertices[0].DrawDataIndex);
        Assert.Equal(1f, builder.Vertices[0].A);
        var geometry = builder.Vertices.ToArray().Where(vertex => vertex.G == 1).ToArray();
        Assert.NotEmpty(geometry);
        Assert.All(geometry, vertex =>
        {
            Assert.Equal(0u, vertex.DrawDataIndex);
            Assert.Equal(0.5625f, vertex.A, 6);
        });
        Assert.Equal(0u, builder.Vertices[^1].DrawDataIndex);
        Assert.True(builder.CellCoverageEvaluations > 0);
        var batch = Assert.Single(builder.Batches.ToArray());
        Assert.Equal(new UiScissor(0, 0, 64, 64), batch.Scissor);
        Assert.Equal((uint)builder.IndexCount, batch.IndexCount);
    }

    private static float SubmittedAlphaAt(UiDrawBuilder builder, int x, int y)
    {
        var result = 0f;
        for (var i = 0; i < builder.VertexCount; i += 4)
        {
            var first = builder.Vertices[i];
            var last = builder.Vertices[i + 2];
            if (x + 0.5 >= first.X && x + 0.5 < last.X && y + 0.5 >= first.Y && y + 0.5 < last.Y)
                result += first.A;
        }
        return result;
    }

    private static void AssertShape(UiGpuClipData data, UiRoundedRectangle shape)
    {
        Assert.Equal(new Vector4((float)shape.Bounds.X, (float)shape.Bounds.Y,
            (float)(shape.Bounds.X + shape.Bounds.Width), (float)(shape.Bounds.Y + shape.Bounds.Height)), data.Bounds);
        Assert.Equal(new Vector4((float)shape.TopLeft.X, (float)shape.TopRight.X,
            (float)shape.BottomRight.X, (float)shape.BottomLeft.X), data.RadiusX);
        Assert.Equal(new Vector4((float)shape.TopLeft.Y, (float)shape.TopRight.Y,
            (float)shape.BottomRight.Y, (float)shape.BottomLeft.Y), data.RadiusY);
    }

    private static void AssertClipContainsShape(UiDrawBuilder builder, UiRoundedRectangle shape,
        bool exclude = false, int vertexIndex = 0)
    {
        var bounds = new Vector4((float)shape.Bounds.X, (float)shape.Bounds.Y,
            (float)(shape.Bounds.X + shape.Bounds.Width), (float)(shape.Bounds.Y + shape.Bounds.Height));
        var radiusX = new Vector4((float)shape.TopLeft.X, (float)shape.TopRight.X,
            (float)shape.BottomRight.X, (float)shape.BottomLeft.X);
        var radiusY = new Vector4((float)shape.TopLeft.Y, (float)shape.TopRight.Y,
            (float)shape.BottomRight.Y, (float)shape.BottomLeft.Y);
        Assert.Contains(GetClips(builder, vertexIndex), clip => clip.Bounds == bounds && clip.RadiusX == radiusX &&
            clip.RadiusY == radiusY && clip.Exclude == (exclude ? 1u : 0u));
    }

    private static IEnumerable<UiGpuClipData> GetClips(UiDrawBuilder builder, int vertexIndex = 0)
    {
        var index = builder.DrawData[(int)builder.Vertices[vertexIndex].DrawDataIndex].ClipIndex;
        while (index != 0)
        {
            var clip = builder.ClipData[(int)index];
            yield return clip;
            index = clip.ParentIndex;
        }
    }

    private sealed class TestRegion : Region
    {
        internal void Add(UiNode node) => Children.Add(node);
    }
}
