using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests;

public sealed class UiGeometryRenderingTests
{
    [Fact]
    public void HalfPixelTriangleProducesHalfCoverage()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 10, 10));

        Assert.Equal(new[] { new UiCoverageQuad(0, 0, 1, 1, 0.5f) }, quads);
    }

    [Fact]
    public void SharedDiagonalDoesNotDoubleCover()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(2, 0), new Point(2, 2), new Point(0, 2)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var rasterizer = new UiGeometryRasterizer();

        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 10, 10));

        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
            Assert.Equal(x < 2 && y < 2 ? 1f : 0f, CoverageAt(quads, x, y));
    }

    [Fact]
    public void ThinTriangleRetainsNonZeroCoverage()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(0.25, 0), new Point(0, 0.25)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var quad = Assert.Single(rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 4, 4)));

        Assert.Equal(new UiCoverageQuad(0, 0, 1, 1, 0.03125f), quad);
    }

    [Fact]
    public void ScissorLimitsCoveredPixels()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(4, 0), new Point(4, 4), new Point(0, 4)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var rasterizer = new UiGeometryRasterizer();

        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(1, 1, 2, 2));

        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
            Assert.Equal(x >= 1 && x < 3 && y >= 1 && y < 3 ? 1f : 0f, CoverageAt(quads, x, y));
    }

    [Fact]
    public void IntegerTranslationMovesCoverageWithRelativeClip()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var first = rasterizer.Rasterize(mesh, 1, 0.5, 0.5, new UiScissor(0, 0, 10, 10));
        var second = rasterizer.Rasterize(mesh, 1, 3.5, 4.5, new UiScissor(3, 4, 10, 10));

        Assert.Equal(0.5d, first.Sum(quad => (double)quad.Width * quad.Height * quad.Coverage));
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            Assert.Equal(CoverageAt(first, x, y), CoverageAt(second, x + 3, y + 4));
    }

    [Fact]
    public void FractionalTranslationProducesQuarterCoverageRows()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Transform(new Point(0.5, 0.5), 1),
                Geometry(mesh, new Color(255, 255, 255)),
                Pop),
            10,
            10,
            false);

        var vertices = builder.Vertices.ToArray();
        Assert.NotEmpty(vertices);
        Assert.All(vertices, vertex => Assert.Equal(0.25f, vertex.A));
        Assert.Equal(0f, vertices.Min(vertex => vertex.X));
        Assert.Equal(0f, vertices.Min(vertex => vertex.Y));
        Assert.Equal(2f, vertices.Max(vertex => vertex.X));
        Assert.Equal(2f, vertices.Max(vertex => vertex.Y));
        Assert.Equal(1f, vertices.Chunk(4).Sum(quad =>
            (quad[2].X - quad[0].X) * (quad[2].Y - quad[0].Y) * quad[0].A));
    }

    [Fact]
    public void BuilderMultipliesCoverageColorAndOpacity()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Opacity(0.5),
                Geometry(mesh, new Color(128, 64, 32, 200)),
                Pop),
            10,
            10,
            false);

        var vertex = builder.Vertices[0];
        Assert.Equal(128 / 255f, vertex.R);
        Assert.Equal(64 / 255f, vertex.G);
        Assert.Equal(32 / 255f, vertex.B);
        Assert.Equal(200 / 255.0 * 0.5 * 0.5, vertex.A, 6);
        Assert.Equal(UiMaterialKind.Solid, vertex.MaterialKind);
    }

    [Fact]
    public void BuilderAppliesScaleAndTranslationToMeshVertices()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Transform(new Point(1, 1), 2),
                Geometry(mesh, new Color(255, 255, 255)),
                Pop),
            10,
            10,
            false);

        var vertices = builder.Vertices.ToArray();
        Assert.Equal(1f, vertices.Min(vertex => vertex.X));
        Assert.Equal(1f, vertices.Min(vertex => vertex.Y));
        Assert.Equal(3f, vertices.Max(vertex => vertex.X));
        Assert.Equal(3f, vertices.Max(vertex => vertex.Y));
        Assert.All(vertices, vertex => Assert.Equal(1f, vertex.A));
        Assert.Equal(4f, vertices.Chunk(4).Sum(quad =>
            (quad[2].X - quad[0].X) * (quad[2].Y - quad[0].Y) * quad[0].A));
    }

    [Fact]
    public void EmptyMeshAndTransparentColorEmitNoCommands()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var commands = new List<UiDrawCommand>();
        var context = new UiDrawingContext(commands);

        context.DrawGeometry(UiTriangleMesh.Empty, new Color(1, 2, 3));
        context.DrawGeometry(mesh, new Color(1, 2, 3, 0));

        Assert.Empty(commands);

        context.DrawGeometry(mesh, new Color(1, 2, 3));

        Assert.Single(commands.OfType<UiDrawGeometryCommand>());
    }

    [Fact]
    public void LargeScreenIntegerTranslationKeepsTinyCoverage()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(0.25, 0), new Point(0, 0.25)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var quad = Assert.Single(rasterizer.Rasterize(
            mesh,
            1,
            10_000_000,
            10_000_000,
            new UiScissor(10_000_000, 10_000_000, 8, 8)));

        Assert.Equal(new UiCoverageQuad(10_000_000, 10_000_000, 1, 1, 0.03125f), quad);
    }

    [Fact]
    public void DiagonalThinTriangleCoverageStaysOnTheDiagonal()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(8, 8), new Point(0, 1)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 16, 16));

        var area = 0.0;
        foreach (var quad in quads)
        {
            Assert.True(quad.X <= quad.Y);
            area += (double)quad.Width * quad.Height * quad.Coverage;
        }

        Assert.Equal(4.0, area, 6);
    }

    [Fact]
    public void LargeFilledRectangleEvaluatesOnlyBoundaryCells()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1024, 0), new Point(1024, 512), new Point(0, 512)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var rasterizer = new UiGeometryRasterizer();
        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 1024, 512));
        Assert.Equal(1024d * 512, quads.Sum(quad => (double)quad.Width * quad.Height * quad.Coverage));
        Assert.All(quads, quad => Assert.Equal(1f, quad.Coverage));
        Assert.True(rasterizer.CellCoverageEvaluations < 8192);
    }

    [Fact]
    public void VerticalStripsPreserveCoverageWithoutFillingGaps()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0.25, 0), new Point(1.25, 0), new Point(1.25, 64), new Point(0.25, 64),
             new Point(0.25, 66), new Point(1.25, 66), new Point(1.25, 68), new Point(0.25, 68)],
            [0u, 1u, 2u, 0u, 2u, 3u, 4u, 5u, 6u, 4u, 6u, 7u]);
        var quads = new UiGeometryRasterizer().Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 4, 70));
        for (var y = 0; y < 70; y++)
        {
            var covered = y < 64 || (y >= 66 && y < 68);
            Assert.Equal(covered ? 0.75f : 0f, CoverageAt(quads, 0, y));
            Assert.Equal(covered ? 0.25f : 0f, CoverageAt(quads, 1, y));
            Assert.Equal(0f, CoverageAt(quads, 2, y));
        }
    }

    [Fact]
    public void FractionalRectangleSpansPreserveEdgeCoverage()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0.25, 0.25), new Point(20.25, 0.25), new Point(20.25, 10.25), new Point(0.25, 10.25)],
            [0u, 1u, 2u, 0u, 2u, 3u]);
        var rasterizer = new UiGeometryRasterizer();
        var quads = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 24, 16));
        Assert.Equal(200d, quads.Sum(quad => (double)quad.Width * quad.Height * quad.Coverage), 6);
        Assert.Equal(0.5625f, CoverageAt(quads, 0, 0));
        Assert.Equal(0.1875f, CoverageAt(quads, 20, 0));
        Assert.Equal(0.0625f, CoverageAt(quads, 20, 10));
        Assert.Equal(1f, CoverageAt(quads, 10, 5));
    }

    [Fact]
    public void OverlappingSpansAndPartialCellsAreClampedBeforeDrawing()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(20, 0), new Point(20, 10), new Point(0, 10),
             new Point(0.5, 0.5), new Point(20.5, 0.5), new Point(20.5, 10.5), new Point(0.5, 10.5)],
            [0u, 1u, 2u, 0u, 2u, 3u, 4u, 5u, 6u, 4u, 6u, 7u]);
        var quads = new UiGeometryRasterizer().Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 24, 16));
        Assert.All(quads, quad => Assert.InRange(quad.Coverage, 0f, 1f));
        Assert.Equal(214.75d, quads.Sum(quad => (double)quad.Width * quad.Height * quad.Coverage), 6);
    }

    [Theory]
    [InlineData(1, 0.25, 0.75)]
    [InlineData(1.5, -2.25, 3.125)]
    public void RowSpansMatchPolygonIntersectionArea(double scale, double translateX, double translateY)
    {
        var mesh = new UiTriangleMesh(
            [new Point(-4, 2), new Point(5, -3), new Point(12, 8),
             new Point(0.5, 0.25), new Point(7, 11), new Point(11, 1),
             new Point(2, 4), new Point(6, 4), new Point(10, 4)],
            [0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u]);
        var quads = new UiGeometryRasterizer().Rasterize(mesh, scale, translateX, translateY,
            new UiScissor(0, 0, 12, 12));
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 12; x++)
        {
            var area = 0d;
            for (var i = 0; i < mesh.Indices.Length; i += 3)
            {
                var triangle = new Point[3];
                for (var j = 0; j < 3; j++)
                {
                    var point = mesh.Vertices[mesh.Indices[i + j]];
                    triangle[j] = new Point(point.X * scale + translateX, point.Y * scale + translateY);
                }
                var polygon = UiConvexClip.IntersectPolygon(triangle, UiConvexClip.Rectangle(new Rect(x, y, 1, 1)));
                var twiceArea = 0d;
                for (var j = 0; j < polygon.Length; j++)
                {
                    var a = polygon[j];
                    var b = polygon[(j + 1) % polygon.Length];
                    twiceArea += (a.X - x) * (b.Y - y) - (b.X - x) * (a.Y - y);
                }
                area += Math.Abs(twiceArea) / 2;
            }
            var actual = quads.Where(quad => x >= quad.X && x < quad.X + quad.Width &&
                y >= quad.Y && y < quad.Y + quad.Height).Sum(quad => quad.Coverage);
            Assert.InRange(Math.Abs(Math.Min(1, area) - actual), 0d, 1e-7);
        }
    }

    private static UiDrawCommandList CommandList(params UiDrawCommand[] commands) =>
        new([.. commands]);

    private static float CoverageAt(IEnumerable<UiCoverageQuad> quads, int x, int y) =>
        quads.Where(quad => x >= quad.X && x < quad.X + quad.Width &&
            y >= quad.Y && y < quad.Y + quad.Height).Sum(quad => quad.Coverage);

    private static UiPushTransformCommand Transform(Point translation, double scale = 1) =>
        new(translation, scale);

    private static UiPushOpacityCommand Opacity(double opacity) =>
        new(opacity);

    private static UiDrawGeometryCommand Geometry(UiTriangleMesh mesh, Color color) =>
        new(mesh, color);

    private static UiDrawCommand Pop => UiPopCommand.Instance;
}
