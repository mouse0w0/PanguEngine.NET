using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing.Geometry;
using Xunit;

namespace PanguEngine.Tests;

public sealed class SvgPathGeometryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void EmptyDataHasNoContours(string? data) => Assert.Empty(SvgPathGeometry.Parse(data).Flatten(0.1));

    [Fact]
    public void CompactCoordinatesAndImplicitLinesAreParsed()
    {
        var contour = Assert.Single(SvgPathGeometry.Parse("M10-5 .5.5 1e1,2e0z").Flatten(0.1));
        Assert.True(contour.Closed);
        Assert.Equal(new Point(10, -5), contour.Points[0]);
        Assert.Equal(new Point(.5, .5), contour.Points[1]);
        Assert.Equal(new Point(10, 2), contour.Points[2]);
    }

    [Fact]
    public void QuadraticBoundsIncludeExactExtremum()
    {
        var geometry = SvgPathGeometry.Parse("M0 0 Q10 30 20 0");
        Assert.Equal(15, geometry.Bounds.Height, 8);
        Assert.Equal(20, geometry.Bounds.Width, 8);
    }

    [Fact]
    public void ArcRadiusCorrectionReachesEndpoint()
    {
        var geometry = SvgPathGeometry.Parse("M0 0 A1 1 0 0110 0");
        var contour = Assert.Single(geometry.Flatten(.01));
        Assert.Equal(new Point(10, 0), contour.Points[^1]);
        Assert.Equal(5, geometry.Bounds.Height, 7);
    }

    [Fact]
    public void SmoothQuadraticDoesNotReflectCubicControl()
    {
        var actual = SvgPathGeometry.Parse("M0 0 C0 10 10 10 10 0 T20 0").Flatten(.1);
        Assert.All(actual[0].Points.Where(p => p.X > 10), p => Assert.Equal(0, p.Y, 8));
    }

    [Fact]
    public void TinyArcEndpointSeparationRemainsFinite()
    {
        var geometry = SvgPathGeometry.Parse("M0 0 A5 5 0 0 1 1e-200 0");
        var contour = Assert.Single(geometry.Flatten(.1));
        Assert.Equal(new Point(1e-200, 0), contour.Points[^1]);
        Assert.All(contour.Points, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
    }

    [Fact]
    public void UnrepresentableRelativeCoordinateReportsParseOffset()
    {
        var error = Assert.Throws<FormatException>(() => SvgPathGeometry.Parse("M1e308 0 l1e308 0"));
        Assert.Contains("offset", error.Message);
    }

    [Fact]
    public void ExcessiveSubdivisionReportsExplicitLimit()
    {
        var geometry = SvgPathGeometry.Parse("M0 0 Q10 30 20 0");
        Assert.Throws<InvalidOperationException>(() => geometry.Flatten(1e-30));
    }

    [Fact]
    public void RelativeCommandsAfterCloseStartAtSubpathOrigin()
    {
        var contours = SvgPathGeometry.Parse("M10 20 h10 v10 h-10 z l5 0").Flatten(.1);
        Assert.Equal(2, contours.Length);
        Assert.True(contours[0].Closed);
        Assert.Equal(new Point(10, 20), contours[1].Points[0]);
        Assert.Equal(new Point(15, 20), contours[1].Points[1]);
    }

    [Fact]
    public void SmoothCubicMatchesExplicitReflectedControl()
    {
        var smooth = SvgPathGeometry.Parse("M0 0 C0 10 10 10 10 0 S20 -10 20 0").Flatten(.1);
        var explicitCurve = SvgPathGeometry.Parse("M0 0 C0 10 10 10 10 0 C10 -10 20 -10 20 0").Flatten(.1);
        Assert.Equal(explicitCurve[0].Points, smooth[0].Points);
    }

    [Fact]
    public void RotatedArcContainsItsSampledPointsInExactBounds()
    {
        var geometry = SvgPathGeometry.Parse("M3 7 A20 10 35 1 1 30 40");
        var bounds = geometry.Bounds;
        Assert.All(geometry.Flatten(.01)[0].Points, point =>
        {
            Assert.InRange(point.X, bounds.X - 1e-9, bounds.X + bounds.Width + 1e-9);
            Assert.InRange(point.Y, bounds.Y - 1e-9, bounds.Y + bounds.Height + 1e-9);
        });
    }

    [Theory]
    [InlineData("L0 0")]
    [InlineData("M")]
    [InlineData("M0 0 L")]
    [InlineData("M0,,0")]
    [InlineData("M0 0,")]
    [InlineData("M0 0 Z1 1")]
    [InlineData("M0 0 L1e999 1")]
    [InlineData("M0 0 A1 1 0 2 0 3 4")]
    public void InvalidDataReportsOffset(string data) =>
        Assert.Contains("offset", Assert.Throws<FormatException>(() => SvgPathGeometry.Parse(data)).Message);
}
