using PanguEngine.Client.Rendering.World;
using PanguEngine.World.Blocks;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class SelectionMeshBuilderTests
{
    [Fact]
    public void EmptyShapeProducesNoVertices()
    {
        Assert.Empty(SelectionMeshBuilder.Build(BlockShape.Empty));
    }

    [Fact]
    public void FullBlockProducesTwelveSolidEdges()
    {
        var vertices = SelectionMeshBuilder.Build(BlockShape.FullBlock);

        Assert.Equal(432, vertices.Length);
    }

    [Fact]
    public void MultipleBoxesProduce432VerticesEach()
    {
        var shape = new BlockShape(
            new Box3D<double>(0, 0, 0, 0.5, 1, 1),
            new Box3D<double>(0.5, 0, 0, 1, 1, 1));

        var vertices = SelectionMeshBuilder.Build(shape);

        Assert.Equal(864, vertices.Length);
    }

    [Fact]
    public void FullBlockVerticesAreRelativeToBlockOrigin()
    {
        var vertices = SelectionMeshBuilder.Build(BlockShape.FullBlock);

        Assert.Equal(-0.007f, vertices.Min(vertex => vertex.X), 4);
        Assert.Equal(-0.007f, vertices.Min(vertex => vertex.Y), 4);
        Assert.Equal(-0.007f, vertices.Min(vertex => vertex.Z), 4);
        Assert.Equal(1.007f, vertices.Max(vertex => vertex.X), 4);
        Assert.Equal(1.007f, vertices.Max(vertex => vertex.Y), 4);
        Assert.Equal(1.007f, vertices.Max(vertex => vertex.Z), 4);
    }

    [Fact]
    public void CustomSelectionBoxProducesFiniteSolidEdges()
    {
        var shape = new SelectionBoxShape(
            new Box3D<double>(0.1, 0.2, 0.3, 0.9, 0.8, 0.7));

        var vertices = SelectionMeshBuilder.Build(shape);

        Assert.Equal(432, vertices.Length);
        Assert.All(vertices, vertex =>
        {
            Assert.True(float.IsFinite(vertex.X));
            Assert.True(float.IsFinite(vertex.Y));
            Assert.True(float.IsFinite(vertex.Z));
            Assert.Equal(0, vertex.R);
            Assert.Equal(0, vertex.G);
            Assert.Equal(0, vertex.B);
            Assert.Equal(1, vertex.A);
        });
    }

    private sealed class SelectionBoxShape(params Box3D<double>[] boxes) : IBlockShape
    {
        public bool TryRaycast(in Ray3D<double> ray, double maxDistance, out BlockShapeHit hit)
        {
            hit = default;
            return false;
        }

        public IReadOnlyList<Box3D<double>> GetSelectionBoxes()
        {
            return boxes;
        }
    }
}