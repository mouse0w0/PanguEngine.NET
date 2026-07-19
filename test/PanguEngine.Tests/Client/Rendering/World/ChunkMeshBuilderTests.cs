using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkMeshBuilderTests
{
    [Fact]
    public void BuildSingleSolidBlockEmitsIndexedSixFaces()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());
        using var resources = new ResourceManager([]);
        var models = CreateModels(resources);

        var mesh = new ChunkMeshBuilder(models).Build(world, chunk);

        Assert.False(mesh.IsEmpty);
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(36, mesh.IndexCount);
        Assert.Equal(
            Enumerable.Range(0, 6)
                .SelectMany(face => new uint[]
                {
                    (uint)(face * 4),
                    (uint)(face * 4 + 1),
                    (uint)(face * 4 + 2),
                    (uint)(face * 4),
                    (uint)(face * 4 + 2),
                    (uint)(face * 4 + 3)
                }),
            mesh.Indices);
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.X));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.X));
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.Y));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.Y));
        Assert.Equal(0f, mesh.Vertices.Min(vertex => vertex.Z));
        Assert.Equal(1f, mesh.Vertices.Max(vertex => vertex.Z));
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
        using var resources = new ResourceManager([]);
        var models = CreateModels(resources);

        var mesh = new ChunkMeshBuilder(models).Build(world, chunk);

        Assert.Equal(40, mesh.VertexCount);
        Assert.Equal(60, mesh.IndexCount);
    }

    [Fact]
    public void BuildUsesOutwardNormalsAndChunkLocalPosition()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());
        using var resources = new ResourceManager([]);
        var models = CreateModels(resources);

        var mesh = new ChunkMeshBuilder(models).Build(world, chunk);

        Assert.Equal(new[]
            {
                (0f, -1f, 0f), (0f, 1f, 0f), (0f, 0f, -1f),
                (0f, 0f, 1f), (-1f, 0f, 0f), (1f, 0f, 0f)
            },
            mesh.Vertices.Chunk(4).Select(group =>
                (group[0].NX, group[0].NY, group[0].NZ)).ToArray());
        Assert.All(mesh.Vertices, vertex =>
        {
            Assert.InRange(vertex.X, 0f, 1f);
            Assert.InRange(vertex.Y, 0f, 1f);
            Assert.InRange(vertex.Z, 0f, 1f);
        });
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
        using var resources = new ResourceManager([]);
        var models = CreateModels(resources);

        var mesh = new ChunkMeshBuilder(models).Build(world, chunk);

        Assert.Equal(20, mesh.VertexCount);
        Assert.Equal(30, mesh.IndexCount);
    }

    private static BlockModelManager CreateModels(ResourceManager resources)
    {
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("pangu", "air"), BuiltinBlocks.Air);
        registry.Register(ResourceKey.Create("pangu", "stone"), BuiltinBlocks.Stone);
        registry.Register(ResourceKey.Create("pangu", "grass"), BuiltinBlocks.Grass);
        registry.Register(ResourceKey.Create("pangu", "dirt"), BuiltinBlocks.Dirt);
        var models = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        models.Load();
        return models;
    }

    private static Chunk GetChunk(ClientWorld world, ChunkPos position)
    {
        return world.Chunks.EnumerateChunks().Single(chunk => chunk.Position == position);
    }
}