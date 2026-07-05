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
        Assert.Equal(32f, mesh.Vertices.Min(vertex => vertex.X));
        Assert.Equal(33f, mesh.Vertices.Max(vertex => vertex.X));
        Assert.Equal(32f, mesh.Vertices.Min(vertex => vertex.Y));
        Assert.Equal(33f, mesh.Vertices.Max(vertex => vertex.Y));
        Assert.Equal(32f, mesh.Vertices.Min(vertex => vertex.Z));
        Assert.Equal(33f, mesh.Vertices.Max(vertex => vertex.Z));
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
}