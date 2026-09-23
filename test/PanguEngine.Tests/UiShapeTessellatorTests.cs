using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Tests;

public sealed class UiShapeTessellatorTests
{
    private const double AreaTolerance = 0.05;

    [Fact]
    public void FillClosedContourMatchesRectangleArea()
    {
        var mesh = UiShapeTessellator.Fill(
            new[] { Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 5), new Point(0, 5)) },
            ShapeFillRule.NonZero);

        Assert.Equal(50.0, TotalArea(mesh), AreaTolerance);
        Assert.Equal(new Rect(0, 0, 10, 5), mesh.Bounds);
    }

    [Fact]
    public void FillOpenContourIsImplicitlyClosed()
    {
        var mesh = UiShapeTessellator.Fill(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10)) },
            ShapeFillRule.NonZero);

        Assert.Equal(100.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillNonZeroRemovesOppositelyWoundHole()
    {
        var outer = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));
        var hole = Contour(true, new Point(2, 2), new Point(2, 8), new Point(8, 8), new Point(8, 2));

        var mesh = UiShapeTessellator.Fill(new[] { outer, hole }, ShapeFillRule.NonZero);

        Assert.Equal(64.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillNonZeroKeepsSameWoundNestedContour()
    {
        var outer = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));
        var inner = Contour(true, new Point(2, 2), new Point(8, 2), new Point(8, 8), new Point(2, 8));

        var mesh = UiShapeTessellator.Fill(new[] { outer, inner }, ShapeFillRule.NonZero);

        Assert.Equal(100.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillEvenOddSubtractsNestedContourRegardlessOfOrientation()
    {
        var outer = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));
        var inner = Contour(true, new Point(2, 2), new Point(8, 2), new Point(8, 8), new Point(2, 8));

        var mesh = UiShapeTessellator.Fill(new[] { outer, inner }, ShapeFillRule.EvenOdd);

        Assert.Equal(64.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillEvenOddIgnoresOrientationOfOverlappingContours()
    {
        var first = Contour(true, new Point(0, 0), new Point(3, 0), new Point(3, 3), new Point(0, 3));
        var second = Contour(true, new Point(2, 2), new Point(5, 2), new Point(5, 5), new Point(2, 5));

        var mesh = UiShapeTessellator.Fill(new[] { first, second }, ShapeFillRule.EvenOdd);

        Assert.Equal(16.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillNonZeroCancelsOppositelyWoundOverlap()
    {
        var first = Contour(true, new Point(0, 0), new Point(3, 0), new Point(3, 3), new Point(0, 3));
        var second = Contour(true, new Point(2, 2), new Point(2, 5), new Point(5, 5), new Point(5, 2));

        var mesh = UiShapeTessellator.Fill(new[] { first, second }, ShapeFillRule.NonZero);

        Assert.Equal(16.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillSelfIntersectingContourUsesEvenOddParity()
    {
        var bowTie = Contour(true, new Point(0, 0), new Point(10, 10), new Point(10, 0), new Point(0, 10));

        var mesh = UiShapeTessellator.Fill(new[] { bowTie }, ShapeFillRule.EvenOdd);

        Assert.Equal(50.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void FillEmptyInputReturnsEmptyMesh()
    {
        var mesh = UiShapeTessellator.Fill(Array.Empty<FlattenedContour>(), ShapeFillRule.NonZero);

        Assert.Same(UiTriangleMesh.Empty, mesh);
        Assert.Equal(Rect.Zero, mesh.Bounds);
    }

    [Fact]
    public void StrokeButtCapsProduceOnlyTheSegmentBody()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Bevel, 4.0, 0.01);

        Assert.Equal(20.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeSquareCapsExtendTheSegmentBody()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0)) },
            2.0, StrokeLineCap.Square, StrokeLineJoin.Bevel, 4.0, 0.01);

        Assert.Equal(24.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeRoundCapsAddTwoHalfDisks()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0)) },
            2.0, StrokeLineCap.Round, StrokeLineJoin.Bevel, 4.0, 0.01);

        Assert.Equal(20.0 + Math.PI, TotalArea(mesh), 0.1);
    }

    [Fact]
    public void StrokeBevelJoinAddsOnlyTheCornerWedge()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(10, 10)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Bevel, 4.0, 0.01);

        Assert.Equal(39.5, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeMiterJoinFillsOuterCornerWithinLimit()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(10, 10)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Equal(40.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeMiterFallsBackToBevelBeyondLimit()
    {
        var path = Contour(false, new Point(0, 0), new Point(10, 0), new Point(9.25, 0.5));
        var bevel = UiShapeTessellator.Stroke(new[] { path }, 2.0,
            StrokeLineCap.Butt, StrokeLineJoin.Miter, 1.0, 0.01);
        var miter = UiShapeTessellator.Stroke(new[] { path }, 2.0,
            StrokeLineCap.Butt, StrokeLineJoin.Miter, 100.0, 0.01);

        double bevelArea = TotalArea(bevel);
        double miterArea = TotalArea(miter);

        Assert.True(bevelArea > 20.0);
        Assert.True(miterArea > 20.0);
        Assert.True(miterArea > bevelArea, $"miter {miterArea} should exceed bevel {bevelArea}");
        Assert.True(double.IsFinite(bevelArea));
    }

    [Fact]
    public void StrokeClosedSquareMiterJoinMatchesOffsetRing()
    {
        var square = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));

        var mesh = UiShapeTessellator.Stroke(new[] { square }, 2.0,
            StrokeLineCap.Round, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Equal(80.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeClosedSquareBevelJoinTrimsOuterCorners()
    {
        var square = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));

        var mesh = UiShapeTessellator.Stroke(new[] { square }, 2.0,
            StrokeLineCap.Butt, StrokeLineJoin.Bevel, 4.0, 0.01);

        Assert.Equal(78.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeClosedSquareRoundJoinRoundsOuterCorners()
    {
        var square = Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10));

        var mesh = UiShapeTessellator.Stroke(new[] { square }, 2.0,
            StrokeLineCap.Butt, StrokeLineJoin.Round, 4.0, 0.01);

        Assert.Equal(76.0 + Math.PI, TotalArea(mesh), 0.1);
    }

    [Fact]
    public void StrokeOpenPolylineWithReversalKeepsBothSegments()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(0, 0)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Equal(20.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeZeroLengthRoundCapProducesDisk()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(5, 5), new Point(5, 5)) },
            2.0, StrokeLineCap.Round, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Equal(Math.PI, TotalArea(mesh), 0.1);
    }

    [Fact]
    public void StrokeZeroLengthSquareCapProducesSquare()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(5, 5), new Point(5, 5)) },
            2.0, StrokeLineCap.Square, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Equal(4.0, TotalArea(mesh), AreaTolerance);
    }

    [Fact]
    public void StrokeZeroLengthButtCapProducesNothing()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(5, 5), new Point(5, 5)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Miter, 4.0, 0.01);

        Assert.Empty(mesh.Indices);
    }

    [Fact]
    public void StrokeZeroThicknessProducesEmptyMesh()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0)) },
            0.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, 0.01);

        Assert.Same(UiTriangleMesh.Empty, mesh);
    }

    [Fact]
    public void StrokeSinglePointMoveProducesNothingForAnyCap()
    {
        foreach (var cap in new[] { StrokeLineCap.Butt, StrokeLineCap.Square, StrokeLineCap.Round })
        {
            var mesh = UiShapeTessellator.Stroke(
                new[] { Contour(false, new Point(5, 5)) },
                2.0, cap, StrokeLineJoin.Round, 4.0, 0.01);

            Assert.Empty(mesh.Indices);
        }
    }

    [Fact]
    public void StrokeSinglePointMoveDiffersFromZeroLengthLine()
    {
        var move = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(5, 5)) },
            2.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, 0.01);
        var line = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(5, 5), new Point(5, 5)) },
            2.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, 0.01);

        Assert.Empty(move.Indices);
        Assert.Equal(Math.PI, TotalArea(line), 0.1);
    }

    [Fact]
    public void StrokeFlattenedLoneMoveProducesNothingButZeroLengthLineDraws()
    {
        var move = SvgPathGeometry.Parse("M5,5").Flatten(0.01);
        var line = SvgPathGeometry.Parse("M5,5 L5,5").Flatten(0.01);

        var moveMesh = UiShapeTessellator.Stroke(
            move, 2.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, 0.01);
        var lineMesh = UiShapeTessellator.Stroke(
            line, 2.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, 0.01);

        Assert.Empty(moveMesh.Indices);
        Assert.Equal(Math.PI, TotalArea(lineMesh), 0.1);
    }

    [Fact]
    public void StrokeRoundJoinAtReversalAddsOuterSemicircle()
    {
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(0, 0)) },
            2.0, StrokeLineCap.Butt, StrokeLineJoin.Round, 4.0, 0.01);

        Assert.Equal(20.0 + Math.PI / 2.0, TotalArea(mesh), 0.1);
        Assert.True(mesh.Contains(new Point(10.5, 0)));
        Assert.False(mesh.Contains(new Point(-0.5, 0)));
    }

    [Fact]
    public void StrokeRoundCapHonorsToleranceForLargeRadius()
    {
        const double half = 500.0;
        const double tolerance = 0.25;
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(0, 0)) },
            half * 2.0, StrokeLineCap.Round, StrokeLineJoin.Round, 4.0, tolerance);

        double probeRadius = half - tolerance;
        for (int step = 0; step < 360 * 4; step++)
        {
            double angle = step * Math.PI / (180 * 4);
            var probe = new Point(Math.Cos(angle) * probeRadius, Math.Sin(angle) * probeRadius);
            Assert.True(mesh.Contains(probe), $"disk boundary is too coarse near angle {angle}");
        }
    }

    [Fact]
    public void StrokeRoundJoinHonorsToleranceForLargeRadius()
    {
        const double half = 500.0;
        const double tolerance = 0.25;
        var mesh = UiShapeTessellator.Stroke(
            new[] { Contour(false, new Point(0, 0), new Point(10, 0), new Point(10, 10)) },
            half * 2.0, StrokeLineCap.Butt, StrokeLineJoin.Round, 4.0, tolerance);

        double probeRadius = half - tolerance;
        for (int step = 1; step < 90; step++)
        {
            double angle = -Math.PI / 2.0 + step * Math.PI / 180.0;
            var probe = new Point(10.0 + Math.Cos(angle) * probeRadius, Math.Sin(angle) * probeRadius);
            Assert.True(mesh.Contains(probe), $"join boundary is too coarse near angle {angle}");
        }
    }

    [Fact]
    public void ContainsIncludesInteriorAndBoundary()
    {
        var mesh = UiShapeTessellator.Fill(
            new[] { Contour(true, new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10)) },
            ShapeFillRule.NonZero);

        Assert.True(mesh.Contains(new Point(5, 5)));
        Assert.True(mesh.Contains(new Point(0, 5)));
        Assert.True(mesh.Contains(new Point(10, 10)));
        Assert.False(mesh.Contains(new Point(-1, 5)));
        Assert.False(mesh.Contains(new Point(11, 5)));
    }

    [Fact]
    public void EmptyMeshContainsNoPoint()
    {
        Assert.Empty(UiTriangleMesh.Empty.Vertices);
        Assert.Empty(UiTriangleMesh.Empty.Indices);
        Assert.Equal(Rect.Zero, UiTriangleMesh.Empty.Bounds);
        Assert.False(UiTriangleMesh.Empty.Contains(new Point(0, 0)));
    }

    [Fact]
    public void DegenerateTriangleDoesNotContainOffAxisPoint()
    {
        var mesh = new UiTriangleMesh(
            new[] { new Point(0, 0), new Point(10, 0), new Point(20, 0) },
            new uint[] { 0, 1, 2 });

        Assert.False(mesh.Contains(new Point(10, 5)));
        Assert.False(mesh.Contains(new Point(30, 0)));
    }

    [Fact]
    public void TransformScalesTranslatesAndRebuildsBounds()
    {
        var mesh = UiShapeTessellator.Fill(
            new[] { Contour(true, new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)) },
            ShapeFillRule.NonZero);

        var transformed = mesh.Transform(2.0, 3.0, 5.0, 7.0);

        Assert.Equal(new Rect(5, 7, 2, 3), transformed.Bounds);
        Assert.True(transformed.Contains(new Point(5.5, 7.5)));
        Assert.True(transformed.Contains(new Point(5.5, 9.5)));
        Assert.False(transformed.Contains(new Point(5.5, 10.5)));
    }

    [Fact]
    public void BoundsAreEmptyForEmptyInputAndTightForGeometry()
    {
        Assert.Equal(Rect.Zero, UiTriangleMesh.Empty.Bounds);

        var mesh = UiShapeTessellator.Fill(
            new[] { Contour(true, new Point(-3, -2), new Point(4, -2), new Point(4, 6), new Point(-3, 6)) },
            ShapeFillRule.NonZero);

        Assert.Equal(new Rect(-3, -2, 7, 8), mesh.Bounds);
    }

    [Fact]
    public void ClosedTwoPointRoundStrokeIncludesBothReversalJoins()
    {
        var mesh = UiShapeTessellator.Stroke(
            [Contour(true, new Point(0, 0), new Point(10, 0))],
            2, StrokeLineCap.Butt, StrokeLineJoin.Round, 4, .001);
        Assert.True(mesh.Contains(new Point(-.5, 0)));
        Assert.True(mesh.Contains(new Point(10.5, 0)));
        Assert.Equal(20 + Math.PI, TotalArea(mesh), AreaTolerance);
    }

    private static FlattenedContour Contour(bool closed, params Point[] points) => new(points, closed);

    private static double TotalArea(UiTriangleMesh mesh)
    {
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        double total = 0.0;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            double doubled = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            total += Math.Abs(doubled) * 0.5;
        }
        return total;
    }
}
