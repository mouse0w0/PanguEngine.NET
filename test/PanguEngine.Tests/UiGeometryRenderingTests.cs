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

        Assert.Equal(
            new[] { new UiCoverageQuad(0, 0, 2, 1, 1f), new UiCoverageQuad(0, 1, 2, 1, 1f) },
            quads);
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

        Assert.Equal(
            new[] { new UiCoverageQuad(1, 1, 2, 1, 1f), new UiCoverageQuad(1, 2, 2, 1, 1f) },
            quads);
    }

    [Fact]
    public void IdenticalKeysReuseTheCachedResult()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var first = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 10, 10));
        var second = rasterizer.Rasterize(mesh, 1, 0, 0, new UiScissor(0, 0, 10, 10));

        Assert.Same(first, second);
        Assert.Equal(1, rasterizer.CacheMisses);
        Assert.Equal(1, rasterizer.CacheHits);
    }

    [Fact]
    public void IntegerTranslationReusesCoverageWithRelativeClip()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var rasterizer = new UiGeometryRasterizer();

        var first = rasterizer.Rasterize(mesh, 1, 0.5, 0.5, new UiScissor(0, 0, 10, 10));
        var second = rasterizer.Rasterize(mesh, 1, 3.5, 4.5, new UiScissor(3, 4, 10, 10));

        Assert.Equal(1, rasterizer.CacheMisses);
        Assert.Equal(1, rasterizer.CacheHits);
        Assert.Equal(
            first.Select(quad => new UiCoverageQuad(quad.X + 3, quad.Y + 4, quad.Width, quad.Height, quad.Coverage)),
            second);
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

        Assert.Equal(8, builder.VertexCount);
        Assert.Equal(0.25f, builder.Vertices[0].A);
        Assert.Equal(0f, builder.Vertices[0].X);
        Assert.Equal(0f, builder.Vertices[0].Y);
        Assert.Equal(2f, builder.Vertices[2].X);
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
        Assert.Equal((uint)UiMaterialKind.Solid, vertex.MaterialData);
        Assert.Equal(4, builder.VertexCount);
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

        Assert.Equal(1f, builder.Vertices[0].X);
        Assert.Equal(1f, builder.Vertices[0].Y);
        Assert.Equal(3f, builder.Vertices[2].X);
        Assert.Equal(2f, builder.Vertices[2].Y);
        Assert.Equal(1f, builder.Vertices[0].A);
    }

    [Fact]
    public void GeometryAndRectangleReportRealCounts()
    {
        var mesh = new UiTriangleMesh(
            [new Point(0, 0), new Point(1, 0), new Point(0, 1)],
            [0u, 1u, 2u]);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Fill(new Rect(5, 5, 2, 2), new Color(255, 0, 0)),
                Geometry(mesh, new Color(0, 255, 0))),
            10,
            10,
            false);

        Assert.Equal(builder.Vertices.Length, builder.VertexCount);
        Assert.Equal(builder.Indices.Length, builder.IndexCount);
        Assert.Equal(8, builder.VertexCount);
        Assert.Equal(12, builder.IndexCount);
        Assert.Equal(1, builder.RectangleCount);
        Assert.NotEqual(builder.VertexCount / 4, builder.RectangleCount);
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

    private static UiDrawCommandList CommandList(params UiDrawCommand[] commands) =>
        new([.. commands]);

    private static UiPushTransformCommand Transform(Point translation, double scale = 1) =>
        new(translation, scale);

    private static UiPushOpacityCommand Opacity(double opacity) =>
        new(opacity);

    private static UiFillRectangleCommand Fill(Rect bounds, Color color) =>
        new(bounds, color);

    private static UiDrawGeometryCommand Geometry(UiTriangleMesh mesh, Color color) =>
        new(mesh, color);

    private static UiDrawCommand Pop => UiPopCommand.Instance;
}
