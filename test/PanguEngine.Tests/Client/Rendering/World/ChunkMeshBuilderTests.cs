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
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static readonly BlockProperty<bool> Powered = BlockProperty.CreateBoolean("powered");

    [Fact]
    public void BuildSingleSolidBlockEmitsIndexedSixFaces()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 32, 32);
        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var chunk = GetChunk(world, position.ToChunkPos());
        var models = CreateModels();

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
        var models = CreateModels();

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
        var models = CreateModels();

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
        var models = CreateModels();

        var mesh = new ChunkMeshBuilder(models).Build(world, chunk);

        Assert.Equal(20, mesh.VertexCount);
        Assert.Equal(30, mesh.IndexCount);
    }

    [Fact]
    public void BuildSelectsAppearanceFromWorldPosition()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/appearances/block/stone.json", """
            {
              "variants": {
                "": [
                  { "model": "pangu:block/one_face" },
                  { "model": "pangu:block/two_faces" }
                ]
              }
            }
            """);
        WriteModel(directory, "one_face", "\"up\": { \"texture\": \"pangu:block/test\" }");
        WriteModel(
            directory,
            "two_faces",
            "\"up\": { \"texture\": \"pangu:block/test\" }, \"down\": { \"texture\": \"pangu:block/test\" }");
        TestDirectory.WriteResource(
            directory,
            "pangu/textures/block/test.png",
            OnePixelPng);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("pangu", "stone"), BuiltinBlocks.Stone);
        var models = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        models.Load();
        var world = new ClientWorld();
        var firstPosition = new BlockPos(32, 0, 32);
        var secondPosition = new BlockPos(48, 0, 32);
        world.SetBlock(firstPosition, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(secondPosition, BuiltinBlocks.Stone.DefaultState);

        var firstMesh = new ChunkMeshBuilder(models)
            .Build(world, GetChunk(world, firstPosition.ToChunkPos()));
        var secondMesh = new ChunkMeshBuilder(models)
            .Build(world, GetChunk(world, secondPosition.ToChunkPos()));

        Assert.Equal(4, firstMesh.VertexCount);
        Assert.Equal(8, secondMesh.VertexCount);
    }

    [Fact]
    public void BuildSelectsAppearanceFromCanonicalBlockState()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/appearances/block/machine.json", """
            {
              "variants": {
                "powered=false": [
                  { "model": "pangu:block/one_face" }
                ],
                "powered=true": [
                  { "model": "pangu:block/two_faces" }
                ]
              }
            }
            """);
        WriteModel(directory, "one_face", "\"up\": { \"texture\": \"pangu:block/test\" }");
        WriteModel(
            directory,
            "two_faces",
            "\"up\": { \"texture\": \"pangu:block/test\" }, \"down\": { \"texture\": \"pangu:block/test\" }");
        TestDirectory.WriteResource(
            directory,
            "pangu/textures/block/test.png",
            OnePixelPng);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block(Powered);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("pangu", "machine"), block);
        var models = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        models.Load();
        var world = new ClientWorld();
        var offPosition = new BlockPos(32, 0, 32);
        var onPosition = new BlockPos(48, 0, 32);
        world.SetBlock(offPosition, block.DefaultState);
        world.SetBlock(onPosition, block.DefaultState.With(Powered, true));

        var offMesh = new ChunkMeshBuilder(models)
            .Build(world, GetChunk(world, offPosition.ToChunkPos()));
        var onMesh = new ChunkMeshBuilder(models)
            .Build(world, GetChunk(world, onPosition.ToChunkPos()));

        Assert.Equal(4, offMesh.VertexCount);
        Assert.Equal(8, onMesh.VertexCount);
    }

    private static BlockModelManager CreateModels()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/appearances/block/stone.json", """
            {
              "variants": {
                "": [
                  { "model": "pangu:block/stone" }
                ]
              }
            }
            """);
        WriteModel(
            directory,
            "stone",
            "\"down\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"down\"] }, " +
            "\"up\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"up\"] }, " +
            "\"north\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"north\"] }, " +
            "\"south\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"south\"] }, " +
            "\"west\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"west\"] }, " +
            "\"east\": { \"texture\": \"pangu:block/stone\", \"cull\": [\"east\"] }");
        TestDirectory.WriteResource(
            directory,
            "pangu/textures/block/stone.png",
            OnePixelPng);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("pangu", "stone"), BuiltinBlocks.Stone);
        var models = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        models.Load();
        return models;
    }

    private static void WriteModel(
        TestDirectory directory,
        string name,
        string faces)
    {
        TestDirectory.WriteResource(
            directory,
            $"pangu/models/block/{name}.json",
            $$"""
              {
                "elements": [
                  {
                    "from": [0, 0, 0],
                    "to": [16, 16, 16],
                    "faces": { {{faces}} }
                  }
                ]
              }
              """);
    }

    private static Chunk GetChunk(ClientWorld world, ChunkPos position)
    {
        return world.Chunks.EnumerateChunks().Single(chunk => chunk.Position == position);
    }
}