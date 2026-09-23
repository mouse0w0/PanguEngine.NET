using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using Xunit;

namespace PanguEngine.Tests;

public sealed class PathBuilderTests
{
    [Fact]
    public void EmptyBuilderBuildsEmptyGeometry()
    {
        var geometry = new PathBuilder().Build();

        Assert.Equal(Rect.Zero, geometry.Bounds);
        Assert.Empty(geometry.Flatten(0.1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseEmptyDataReturnsEmptyGeometry(string? data)
    {
        var geometry = PathGeometry.Parse(data);

        Assert.Equal(Rect.Zero, geometry.Bounds);
        Assert.Empty(geometry.Flatten(0.1));
    }

    [Fact]
    public void FirstCommandMustBeMoveTo()
    {
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().LineTo(new Point(1, 1)));
        Assert.Throws<InvalidOperationException>(
            () => new PathBuilder().QuadraticTo(new Point(1, 1), new Point(2, 2)));
        Assert.Throws<InvalidOperationException>(
            () => new PathBuilder().CubicTo(new Point(1, 1), new Point(2, 2), new Point(3, 3)));
        Assert.Throws<InvalidOperationException>(
            () => new PathBuilder().ArcTo(new Point(1, 1), 1, 1, 0, false, true));
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().Close());
    }

    [Fact]
    public void LineAfterMoveIsAllowed()
    {
        var geometry = new PathBuilder().MoveTo(new Point(0, 0)).LineTo(new Point(3, 4)).Build();

        var contour = Assert.Single(geometry.Flatten(0.1));
        Assert.False(contour.Closed);
        Assert.Equal(new Point(3, 4), contour.Points[^1]);
    }

    [Fact]
    public void CloseAfterMoveIsAllowed()
    {
        var geometry = new PathBuilder().MoveTo(new Point(1, 2)).LineTo(new Point(4, 2)).Close().Build();

        var contour = Assert.Single(geometry.Flatten(0.1));
        Assert.True(contour.Closed);
    }

    [Fact]
    public void BuildIsAnIndependentSnapshot()
    {
        var builder = new PathBuilder().MoveTo(new Point(0, 0)).LineTo(new Point(6, 6));
        var first = builder.Build();
        builder.LineTo(new Point(0, 12));
        var second = builder.Build();

        var firstContour = Assert.Single(first.Flatten(0.1));
        Assert.Equal(2, firstContour.Points.Length);
        Assert.Equal(new Point(6, 6), firstContour.Points[^1]);

        var secondContour = Assert.Single(second.Flatten(0.1));
        Assert.Equal(3, secondContour.Points.Length);
        Assert.Equal(new Point(0, 12), secondContour.Points[^1]);
    }

    [Fact]
    public void BuilderCanContinueAfterBuild()
    {
        var builder = new PathBuilder().MoveTo(new Point(0, 0)).LineTo(new Point(10, 0));
        var first = builder.Build();
        builder.LineTo(new Point(10, 10));
        var second = builder.Build();

        Assert.Equal(2, first.Flatten(0.1)[0].Points.Length);
        Assert.Equal(new Point(10, 0), first.Flatten(0.1)[0].Points[^1]);
        Assert.Equal(3, second.Flatten(0.1)[0].Points.Length);
        Assert.Equal(new Point(10, 10), second.Flatten(0.1)[0].Points[^1]);
    }

    [Fact]
    public void CloseReturnsToSubpathStartAndAllowsMoreSegments()
    {
        var geometry = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .LineTo(new Point(10, 0))
            .LineTo(new Point(10, 10))
            .Close()
            .LineTo(new Point(20, 20))
            .Build();

        var contours = geometry.Flatten(0.1);
        Assert.Equal(2, contours.Length);
        Assert.True(contours[0].Closed);
        Assert.False(contours[1].Closed);
        Assert.Equal(new Point(0, 0), contours[1].Points[0]);
        Assert.Equal(new Point(20, 20), contours[1].Points[^1]);
    }

    [Fact]
    public void MultipleMoveToProduceMultipleSubpaths()
    {
        var geometry = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .LineTo(new Point(5, 0))
            .MoveTo(new Point(10, 10))
            .LineTo(new Point(15, 10))
            .Build();

        var contours = geometry.Flatten(0.1);
        Assert.Equal(2, contours.Length);
        Assert.Equal(new Point(0, 0), contours[0].Points[0]);
        Assert.Equal(new Point(10, 10), contours[1].Points[0]);
    }

    [Fact]
    public void BoundsIncludeNegativeCoordinates()
    {
        var geometry = new PathBuilder().MoveTo(new Point(-5, -3)).LineTo(new Point(5, 7)).Build();

        Assert.Equal(new Rect(-5, -3, 10, 10), geometry.Bounds);
    }

    [Fact]
    public void CubicMatchesParsedSvg()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .CubicTo(new Point(0, 10), new Point(10, 10), new Point(10, 0))
            .Build();
        var parsed = PathGeometry.Parse("M0 0 C0 10 10 10 10 0");

        Assert.Equal(parsed.Bounds, built.Bounds);
        Assert.Equal(parsed.Flatten(0.1)[0].Points, built.Flatten(0.1)[0].Points);
    }

    [Fact]
    public void QuadraticMatchesParsedSvg()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .QuadraticTo(new Point(10, 30), new Point(20, 0))
            .Build();
        var parsed = PathGeometry.Parse("M0 0 Q10 30 20 0");

        Assert.Equal(parsed.Bounds, built.Bounds);
        Assert.Equal(parsed.Flatten(0.1)[0].Points, built.Flatten(0.1)[0].Points);
    }

    [Fact]
    public void ArcMatchesParsedSvg()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .ArcTo(new Point(10, 0), 1, 1, 0, false, true)
            .Build();
        var parsed = PathGeometry.Parse("M0 0 A1 1 0 0 1 10 0");

        Assert.Equal(parsed.Bounds, built.Bounds);
        Assert.Equal(parsed.Flatten(0.01)[0].Points, built.Flatten(0.01)[0].Points);
    }

    [Fact]
    public void RotatedArcMatchesParsedSvg()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(3, 7))
            .ArcTo(new Point(30, 40), 20, 10, 35, true, true)
            .Build();
        var parsed = PathGeometry.Parse("M3 7 A20 10 35 1 1 30 40");

        Assert.Equal(parsed.Bounds, built.Bounds);
        Assert.Equal(parsed.Flatten(0.01)[0].Points, built.Flatten(0.01)[0].Points);
    }

    [Fact]
    public void ArcWithZeroRadiusBecomesLine()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .ArcTo(new Point(10, 0), 0, 0, 0, false, true)
            .Build();
        var parsed = PathGeometry.Parse("M0 0 A0 0 0 0 1 10 0");

        Assert.Equal(parsed.Flatten(0.1)[0].Points, built.Flatten(0.1)[0].Points);
    }

    [Fact]
    public void ArcWithCoincidentEndpointsAddsNoSegment()
    {
        var built = new PathBuilder()
            .MoveTo(new Point(5, 5))
            .ArcTo(new Point(5, 5), 3, 3, 0, false, true)
            .Build();

        var contour = Assert.Single(built.Flatten(0.1));
        Assert.Single(contour.Points);
        Assert.Equal(new Point(5, 5), contour.Points[0]);
    }

    [Fact]
    public void ParseInvalidDataThrowsFormatExceptionWithOffset()
    {
        var error = Assert.Throws<FormatException>(() => PathGeometry.Parse("L0 0"));
        Assert.Contains("offset", error.Message);
    }

    [Fact]
    public void ParseBoundsOverflowThrowsFormatExceptionWithOffset()
    {
        var error = Assert.Throws<FormatException>(() => PathGeometry.Parse("M-1e308 0 L1e308 0"));
        Assert.Contains("offset", error.Message);
    }
}
