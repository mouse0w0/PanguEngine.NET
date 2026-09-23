using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using Path = PanguEngine.Client.UI.Controls.Path;

namespace PanguEngine.Tests;

public sealed class UiShapeTests
{
    [Fact]
    public void ShapeDefaultsUseExpectedValuesAndInvalidation()
    {
        var shape = new Rectangle();

        Assert.Equal(typeof(Shape), Shape.FillProperty.OwnerType);
        Assert.Equal(typeof(Shape), Shape.FillProperty.TargetType);
        Assert.Equal(UiPropertyInvalidation.Render, Shape.FillProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Shape.StrokeProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Shape.StrokeThicknessProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Shape.StrokeLineCapProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Shape.StrokeMiterLimitProperty.Invalidation);

        var fill = Assert.IsType<SolidColorBrush>(shape.Fill);
        Assert.Equal(new Color(0, 0, 0), fill.Color);
        Assert.Null(shape.Stroke);
        Assert.Equal(1d, shape.StrokeThickness);
        Assert.Equal(StrokeLineCap.Butt, shape.StrokeLineCap);
        Assert.Equal(StrokeLineJoin.Miter, shape.StrokeLineJoin);
        Assert.Equal(4d, shape.StrokeMiterLimit);

        Assert.Equal(typeof(Path), Path.DataProperty.OwnerType);
        Assert.Null(Path.DataProperty.DefaultValue);
        Assert.Equal(ShapeFillRule.NonZero, Path.FillRuleProperty.DefaultValue);
        Assert.Equal(PathStretch.Uniform, Path.StretchProperty.DefaultValue);
    }

    [Fact]
    public void RectangleAndEllipseUseZeroNaturalSizeAndExplicitSize()
    {
        var rectangle = new Rectangle();
        rectangle.Measure(Size.Infinite);

        Assert.Equal(Size.Zero, rectangle.DesiredSize);

        rectangle.Width = 40;
        rectangle.Height = 20;
        Arrange(rectangle, new Rect(0, 0, 40, 20));

        Assert.True(rectangle.Contains(new Point(20, 10)));
        Assert.False(rectangle.Contains(new Point(-1, 10)));
        Assert.True(rectangle.Contains(new Point(40, 10)));
        Assert.False(rectangle.Contains(new Point(40.1, 10)));

        var ellipse = new Ellipse();
        ellipse.Measure(Size.Infinite);

        Assert.Equal(Size.Zero, ellipse.DesiredSize);

        ellipse.Width = 40;
        ellipse.Height = 20;
        Arrange(ellipse, new Rect(0, 0, 40, 20));

        Assert.True(ellipse.Contains(new Point(20, 10)));
        Assert.False(ellipse.Contains(new Point(-1, 10)));
    }

    [Fact]
    public void PathNaturalSizeIncludesStrokeAndNormalizesNegativeCoordinates()
    {
        var square = new Path { Data = PathGeometry.Parse("M-10,-10 L10,-10 L10,10 L-10,10 Z") };
        square.Measure(Size.Infinite);

        Assert.Equal(new Size(20, 20), square.DesiredSize);

        Arrange(square, new Rect(0, 0, 20, 20));

        Assert.True(square.Contains(new Point(1, 1)));
        Assert.True(square.Contains(new Point(19, 19)));
        Assert.False(square.Contains(new Point(-1, -1)));

        var butt = new Path
        {
            Data = PathGeometry.Parse("M0,0 L10,0"),
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 4,
            StrokeLineCap = StrokeLineCap.Butt
        };
        butt.Measure(Size.Infinite);

        Assert.Equal(new Size(10, 4), butt.DesiredSize);

        var squareCap = new Path
        {
            Data = PathGeometry.Parse("M0,0 L10,0"),
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 4,
            StrokeLineCap = StrokeLineCap.Square
        };
        squareCap.Measure(Size.Infinite);

        Assert.Equal(new Size(14, 4), squareCap.DesiredSize);
    }

    [Fact]
    public void PathStretchModesMapMeshToLayout()
    {
        var squareData = PathGeometry.Parse("M0,0 H10 V10 H0 Z");

        var none = new Path
        {
            Data = squareData,
            Stretch = PathStretch.None,
            Width = 100,
            Height = 50
        };
        Arrange(none, new Rect(0, 0, 100, 50));

        Assert.True(none.Contains(new Point(5, 5)));
        Assert.False(none.Contains(new Point(50, 25)));

        var fill = new Path
        {
            Data = squareData,
            Stretch = PathStretch.Fill,
            Width = 100,
            Height = 50
        };
        Arrange(fill, new Rect(0, 0, 100, 50));

        Assert.True(fill.Contains(new Point(50, 25)));

        var uniform = new Path
        {
            Data = squareData,
            Stretch = PathStretch.Uniform,
            Width = 100,
            Height = 50
        };
        Arrange(uniform, new Rect(0, 0, 100, 50));

        Assert.True(uniform.Contains(new Point(50, 25)));
        Assert.False(uniform.Contains(new Point(5, 5)));
    }

    [Fact]
    public void FillAndStrokeNullDisableHitRegions()
    {
        var filled = new Rectangle { Width = 20, Height = 20 };
        Arrange(filled, new Rect(0, 0, 20, 20));

        Assert.True(filled.Contains(new Point(10, 10)));

        filled.Fill = null;

        Assert.False(filled.Contains(new Point(10, 10)));

        var stroked = new Rectangle
        {
            Width = 20,
            Height = 20,
            Fill = null,
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 6
        };
        Arrange(stroked, new Rect(0, 0, 20, 20));

        Assert.True(stroked.Contains(new Point(3, 10)));
        Assert.False(stroked.Contains(new Point(10, 10)));
    }

    [Fact]
    public void TranslucentFillStillHits()
    {
        var rectangle = new Rectangle
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(new Color(0, 0, 0, 0))
        };
        Arrange(rectangle, new Rect(0, 0, 20, 20));

        Assert.True(rectangle.Contains(new Point(10, 10)));
    }

    [Fact]
    public void ChangingGeometryInvalidatesLayoutButColorDoesNot()
    {
        var rectangle = new Rectangle { Width = 50, Height = 25 };
        Arrange(rectangle, new Rect(0, 0, 50, 25));

        Assert.True(rectangle.IsMeasureValid);
        Assert.True(rectangle.IsArrangeValid);

        rectangle.Fill = new SolidColorBrush(new Color(255, 0, 0));

        Assert.True(rectangle.IsMeasureValid);
        Assert.True(rectangle.IsArrangeValid);

        rectangle.Stroke = new SolidColorBrush(new Color(0, 0, 255));

        Assert.False(rectangle.IsMeasureValid);
        Assert.False(rectangle.IsArrangeValid);

        var path = new Path { Width = 20, Height = 20 };
        Arrange(path, new Rect(0, 0, 20, 20));

        Assert.True(path.IsMeasureValid);

        path.Data = PathGeometry.Parse("M0,0 L20,20");

        Assert.False(path.IsMeasureValid);
    }

    [Fact]
    public void PathFillRuleControlsHoleHit()
    {
        var donut = PathGeometry.Parse("M0,0 H20 V20 H0 Z M5,5 H15 V15 H5 Z");

        var evenOdd = new Path { Data = donut, FillRule = ShapeFillRule.EvenOdd };
        Arrange(evenOdd, new Rect(0, 0, 20, 20));

        Assert.False(evenOdd.Contains(new Point(10, 10)));
        Assert.True(evenOdd.Contains(new Point(2, 2)));

        var nonZero = new Path { Data = donut, FillRule = ShapeFillRule.NonZero };
        Arrange(nonZero, new Rect(0, 0, 20, 20));

        Assert.True(nonZero.Contains(new Point(10, 10)));
    }

    [Fact]
    public void NullAndEmptyGeometryMeasureToZeroAndDoNotHit()
    {
        var noData = new Path();
        noData.Measure(Size.Infinite);

        Assert.Equal(Size.Zero, noData.DesiredSize);

        var empty = new Path { Data = PathGeometry.Parse("") };
        empty.Measure(Size.Infinite);

        Assert.Equal(Size.Zero, empty.DesiredSize);

        Arrange(noData, new Rect(0, 0, 10, 10));

        Assert.False(noData.Contains(new Point(5, 5)));
    }

    [Fact]
    public void ReplacingDataInvalidatesMeasureAndHit()
    {
        var path = new Path
        {
            Data = PathGeometry.Parse("M0 0 H20 V20 H0 Z"),
            Stretch = PathStretch.None,
            Width = 20,
            Height = 20
        };
        Arrange(path, new Rect(0, 0, 20, 20));

        Assert.True(path.Contains(new Point(18, 10)));

        path.Data = PathGeometry.Parse("M0 0 L10 10 L0 20 Z");

        Assert.False(path.IsMeasureValid);

        Arrange(path, new Rect(0, 0, 20, 20));

        Assert.True(path.Contains(new Point(2, 10)));
        Assert.False(path.Contains(new Point(18, 10)));
    }

    [Fact]
    public void SharedGeometryKeepsIndependentStyleAndLayout()
    {
        var geometry = PathGeometry.Parse("M0 0 H20 V20 H0 Z");

        var filled = new Path
        {
            Data = geometry,
            Stretch = PathStretch.None,
            Width = 20,
            Height = 20
        };
        var stroked = new Path
        {
            Data = geometry,
            Stretch = PathStretch.None,
            Width = 20,
            Height = 20,
            Fill = null,
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 4
        };
        var stretched = new Path
        {
            Data = geometry,
            Stretch = PathStretch.Fill,
            Width = 40,
            Height = 20
        };

        Arrange(filled, new Rect(0, 0, 20, 20));
        Arrange(stroked, new Rect(0, 0, 20, 20));
        Arrange(stretched, new Rect(0, 0, 40, 20));

        Assert.True(filled.Contains(new Point(10, 10)));
        Assert.False(stroked.Contains(new Point(10, 10)));
        Assert.True(stroked.Contains(new Point(2, 10)));
        Assert.True(stretched.Contains(new Point(38, 10)));
        Assert.False(filled.Contains(new Point(38, 10)));
    }

    [Fact]
    public void InvalidPathDataThrowsWhenParsed()
    {
        var error = Assert.Throws<FormatException>(() => PathGeometry.Parse("M0 0 L"));

        Assert.Contains("offset", error.Message);
    }

    [Fact]
    public void EllipseStrokeOnlyHitsTheRing()
    {
        var ellipse = new Ellipse
        {
            Width = 100,
            Height = 100,
            Fill = null,
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 10
        };
        Arrange(ellipse, new Rect(0, 0, 100, 100));

        Assert.True(ellipse.Contains(new Point(6, 50)));
        Assert.True(ellipse.Contains(new Point(50, 94)));
        Assert.False(ellipse.Contains(new Point(50, 50)));
        Assert.False(ellipse.Contains(new Point(20, 50)));
    }

    [Fact]
    public void OversizedStrokeClampsAndStaysInsideLayout()
    {
        var rectangle = new Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = null,
            Stroke = new SolidColorBrush(new Color(0, 0, 0)),
            StrokeThickness = 40
        };
        Arrange(rectangle, new Rect(0, 0, 10, 10));

        Assert.Equal(new Rect(0, 0, 10, 10), rectangle.LayoutBounds);
        Assert.False(rectangle.Contains(new Point(-1, 5)));
        Assert.False(rectangle.Contains(new Point(11, 5)));
        Assert.False(rectangle.Contains(new Point(5, -1)));
        Assert.False(rectangle.Contains(new Point(5, 11)));
    }

    [Fact]
    public void CssRegistrationExposesShapeAndPathPropertiesWithoutData()
    {
        var fill = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "fill")!.Convert("none"));
        Assert.Same(Shape.FillProperty, fill.Property);
        Assert.Null(fill.BoxedValue);

        var stroke = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "stroke")!.Convert("#010203"));
        Assert.Same(Shape.StrokeProperty, stroke.Property);
        var strokeBrush = Assert.IsType<SolidColorBrush>(stroke.BoxedValue);
        Assert.Equal(new Color(1, 2, 3), strokeBrush.Color);

        var width = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "stroke-width")!.Convert("3px"));
        Assert.Same(Shape.StrokeThicknessProperty, width.Property);
        Assert.Equal(3d, (double)width.BoxedValue!);

        var cap = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "stroke-linecap")!.Convert("round"));
        Assert.Same(Shape.StrokeLineCapProperty, cap.Property);
        Assert.Equal(StrokeLineCap.Round, (StrokeLineCap)cap.BoxedValue!);

        var join = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "stroke-linejoin")!.Convert("bevel"));
        Assert.Same(Shape.StrokeLineJoinProperty, join.Property);
        Assert.Equal(StrokeLineJoin.Bevel, (StrokeLineJoin)join.BoxedValue!);

        var miter = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Rectangle), "stroke-miterlimit")!.Convert("4"));
        Assert.Same(Shape.StrokeMiterLimitProperty, miter.Property);
        Assert.Equal(4d, (double)miter.BoxedValue!);

        var fillRule = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Path), "fill-rule")!.Convert("evenodd"));
        Assert.Same(Path.FillRuleProperty, fillRule.Property);
        Assert.Equal(ShapeFillRule.EvenOdd, (ShapeFillRule)fillRule.BoxedValue!);

        var stretch = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Path), "stretch")!.Convert("fill"));
        Assert.Same(Path.StretchProperty, stretch.Property);
        Assert.Equal(PathStretch.Fill, (PathStretch)stretch.BoxedValue!);

        var data = Assert.Single(
            UiCssRegistry.FindProperty(typeof(Path), "data")!.Convert("M0 0 L1 1"));
        Assert.Same(Path.DataProperty, data.Property);
        Assert.Equal(new Rect(0, 0, 1, 1), Assert.IsType<PathGeometry>(data.BoxedValue).Bounds);
    }

    [Fact]
    public void UnsupportedBrushThrowsWhileDrawing()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var rectangle = new Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = new ImageBrush(image)
        };
        var screen = CreateScreen(rectangle);
        try
        {
            Assert.Throws<NotSupportedException>(() => screen.CreateDrawCommandList());
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ValidGeometryRecordsDrawingCommands()
    {
        var rectangle = new Rectangle { Width = 10, Height = 10 };
        var screen = CreateScreen(rectangle);
        try
        {
            var commands = screen.CreateDrawCommandList();

            Assert.NotEmpty(commands);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ScreenScaleChangesKeepLocalHitGeometryConsistent()
    {
        var rectangle = new Rectangle { Width = 20, Height = 20 };
        var root = new Panel();
        root.Children.Add(rectangle);
        var screen = new UiScreen(root) { UseLayoutRounding = false, Scale = 1 };
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(200, 100), 0);

            Assert.True(rectangle.Contains(new Point(10, 10)));
            Assert.False(rectangle.Contains(new Point(-1, 10)));

            screen.Scale = 2;
            screen.PrepareFrame(new Size(200, 100), 0);

            Assert.True(rectangle.Contains(new Point(10, 10)));
            Assert.False(rectangle.Contains(new Point(-1, 10)));
        }
        finally
        {
            screen.Close();
        }
    }

    private static void Arrange(UiNode node, Rect bounds)
    {
        node.Measure(new Size(bounds.Width, bounds.Height));
        node.Arrange(bounds);
    }

    private static UiScreen CreateScreen(UiNode child)
    {
        var root = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root) { UseLayoutRounding = false };
        screen.Open();
        screen.PrepareFrame(new Size(200, 100), 0);
        return screen;
    }
}
