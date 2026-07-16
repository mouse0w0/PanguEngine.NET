using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkMeshBuilderTests
{
    [Fact]
    public void BuildSingleSolidBlockEmitsSixFaces()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());

        var mesh = new ChunkMeshBuilder().Build(world, chunk);

        Assert.False(mesh.IsEmpty);
        Assert.Equal(36, mesh.VertexCount);
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.X));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.X));
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.Y));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.Y));
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.Z));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.Z));
        Assert.Contains(mesh.Vertices, vertex =>
            vertex.R == 0.55f && vertex.G == 0.55f && vertex.B == 0.55f && vertex.A == 1f);
    }

    [Fact]
    public void BuildAdjacentBlocksCullsTouchingFaces()
    {
        var world = new ClientWorld();
        var first = new BlockPos(32, 32, 32);
        var second = new BlockPos(33, 32, 32);
        world.SetBlock(first, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(second, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, first.ToChunkPos());

        var mesh = new ChunkMeshBuilder().Build(world, chunk);

        Assert.Equal(60, mesh.VertexCount);
    }

    [Fact]
    public void BuildSingleSolidBlockAppliesDirectionalFaceShading()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());

        var mesh = new ChunkMeshBuilder().Build(world, chunk);

        AssertFaceColor(mesh, 0, 0.275f);
        AssertFaceColor(mesh, 1, 0.55f);
        AssertFaceColor(mesh, 2, 0.44f);
        AssertFaceColor(mesh, 3, 0.44f);
        AssertFaceColor(mesh, 4, 0.33f);
        AssertFaceColor(mesh, 5, 0.33f);
    }

    [Fact]
    public void BuildSingleSolidBlockUsesOutwardFaceWinding()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());

        var mesh = new ChunkMeshBuilder().Build(world, chunk);

        AssertFaceNormal(mesh, 0, 0, -1, 0);
        AssertFaceNormal(mesh, 1, 0, 1, 0);
        AssertFaceNormal(mesh, 2, 0, 0, -1);
        AssertFaceNormal(mesh, 3, 0, 0, 1);
        AssertFaceNormal(mesh, 4, -1, 0, 0);
        AssertFaceNormal(mesh, 5, 1, 0, 0);
    }

    [Fact]
    public void BuildUsesWorldNeighborsAcrossChunkBoundaries()
    {
        var world = new ClientWorld();
        var boundary = new BlockPos(47, 32, 32);
        var neighbor = new BlockPos(48, 32, 32);
        world.SetBlock(boundary, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(neighbor, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, boundary.ToChunkPos());

        var mesh = new ChunkMeshBuilder().Build(world, chunk);

        Assert.Equal(30, mesh.VertexCount);
    }

    private static Chunk GetChunk(ClientWorld world, ChunkPos position)
    {
        return world.Chunks.EnumerateChunks().Single(chunk => chunk.Position == position);
    }

    private static void AssertFaceColor(ChunkMesh mesh, int faceIndex, float expectedRgb)
    {
        var faceVertices = mesh.Vertices.Skip(faceIndex * 6).Take(6);

        Assert.All(faceVertices, vertex =>
        {
            Assert.InRange(vertex.R, expectedRgb - 0.0001f, expectedRgb + 0.0001f);
            Assert.InRange(vertex.G, expectedRgb - 0.0001f, expectedRgb + 0.0001f);
            Assert.InRange(vertex.B, expectedRgb - 0.0001f, expectedRgb + 0.0001f);
            Assert.Equal(1f, vertex.A);
        });
    }

    private static void AssertFaceNormal(
        ChunkMesh mesh,
        int faceIndex,
        float expectedX,
        float expectedY,
        float expectedZ)
    {
        var first = mesh.Vertices[faceIndex * 6];
        var second = mesh.Vertices[faceIndex * 6 + 1];
        var third = mesh.Vertices[faceIndex * 6 + 2];
        var ab = (X: second.X - first.X, Y: second.Y - first.Y, Z: second.Z - first.Z);
        var ac = (X: third.X - first.X, Y: third.Y - first.Y, Z: third.Z - first.Z);
        var normal = (
            X: ab.Y * ac.Z - ab.Z * ac.Y,
            Y: ab.Z * ac.X - ab.X * ac.Z,
            Z: ab.X * ac.Y - ab.Y * ac.X);

        Assert.Equal(expectedX, normal.X);
        Assert.Equal(expectedY, normal.Y);
        Assert.Equal(expectedZ, normal.Z);
    }
}