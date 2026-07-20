using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class BlockModelManagerTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public void MissingRootModelUsesMissingCube()
    {
        var registry = new Registry<Block>(RegistryKeys.Block);
        var stone = new Block();
        registry.Register(ResourceKey.Create("test", "stone"), stone);
        using var resources = new ResourceManager([]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() => manager.Get(stone.DefaultState));
        Assert.Throws<InvalidOperationException>(() => manager.Atlas);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(stone.DefaultState).Emit(default, DirectionFlags.None, writer);

        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void AtlasDeviceLimitFailureDoesNotPublishSnapshot()
    {
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "stone"), new Block());
        using var resources = new ResourceManager([]);
        var manager = new BlockModelManager(resources, registry, 1u, NullLogger.Instance);

        Assert.Throws<ArgumentException>(() => manager.Load());
        Assert.Throws<InvalidOperationException>(() => manager.Atlas);
    }

    [Fact]
    public void AirUsesEmptyModelAndUnknownBlockUsesMissingModel()
    {
        var registry = new Registry<Block>(RegistryKeys.Block);
        var air = new AirBlock();
        var registered = new Block();
        registry.Register(ResourceKey.Create("test", "air"), air);
        registry.Register(ResourceKey.Create("test", "registered"), registered);
        using var resources = new ResourceManager([]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        manager.Load();

        var airWriter = new RecordingWriter();
        manager.Get(air.DefaultState).Emit(default, DirectionFlags.None, airWriter);
        Assert.Empty(airWriter.Vertices);

        var unknownWriter = new RecordingWriter();
        manager.Get(new Block().DefaultState).Emit(default, DirectionFlags.None, unknownWriter);
        Assert.Equal(24, unknownWriter.Vertices.Count);
    }

    [Fact]
    public void InheritsParentGeometryAndPreservesRotationThroughMissingTextureReplacement()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/base.json", """
                                                                              {
                                                                                "textures": { "all": "block/base" },
                                                                                "elements": [
                                                                                  {
                                                                                    "from": [0, 0, 0],
                                                                                    "to": [16, 16, 16],
                                                                                    "faces": { "up": { "texture": "#all", "rotation": 90 } }
                                                                                  }
                                                                                ]
                                                                              }
                                                                              """);
        TestDirectory.WriteResource(directory, "test/models/block/child.json", """
                                                                               {
                                                                                 "parent": "test:block/base"
                                                                               }
                                                                               """);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "child"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(4, writer.Vertices.Count);
        Assert.Equal(6, writer.Indices.Count);
        var u0 = writer.TexCoords.Min(value => value.X);
        var u1 = writer.TexCoords.Max(value => value.X);
        var v0 = writer.TexCoords.Min(value => value.Y);
        var v1 = writer.TexCoords.Max(value => value.Y);
        Assert.Equal(
            [
                new Vector2D<float>(u1, v0),
                new Vector2D<float>(u0, v0),
                new Vector2D<float>(u0, v1),
                new Vector2D<float>(u1, v1)
            ],
            writer.TexCoords);
    }

    [Fact]
    public void ParentCycleFallsBackToMissingCube()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/a.json", """
                                                                           { "parent": "test:block/b" }
                                                                           """);
        TestDirectory.WriteResource(directory, "test/models/block/b.json", """
                                                                           { "parent": "test:block/a" }
                                                                           """);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "a"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void InheritedReferencesUseDeclaringModelNamespace()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "parent/models/block/base.json", """
            {
              "textures": { "all": "block/table" },
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": {
                    "up": { "texture": "#all" },
                    "down": { "texture": "block/direct" }
                  }
                }
              ]
            }
            """);
        TestDirectory.WriteResource(directory, "child/models/block/model.json", """
            { "parent": "parent:block/base" }
            """);
        TestDirectory.WriteResource(directory, "parent/textures/block/table.png", OnePixelPng);
        TestDirectory.WriteResource(directory, "parent/textures/block/direct.png", OnePixelPng);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("child", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        Assert.Equal(1, manager.Atlas.GetRegion(ResourceKey.Create("parent", "block/table")).Width);
        Assert.Equal(1, manager.Atlas.GetRegion(ResourceKey.Create("parent", "block/direct")).Width);
    }

    [Fact]
    public void SharedParentCacheDoesNotShareRootTextureOverrides()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "shared/models/block/base.json", """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "#all" } }
                }
              ]
            }
            """);
        TestDirectory.WriteResource(directory, "first/models/block/a.json", """
                                                                            {
                                                                              "parent": "shared:block/base",
                                                                              "textures": { "all": "block/a" }
                                                                            }
                                                                            """);
        TestDirectory.WriteResource(directory, "second/models/block/b.json", """
                                                                             {
                                                                               "parent": "shared:block/base",
                                                                               "textures": { "all": "block/b" }
                                                                             }
                                                                             """);
        TestDirectory.WriteResource(directory, "first/textures/block/a.png", OnePixelPng);
        TestDirectory.WriteResource(directory, "second/textures/block/b.png", OnePixelPng);
        var first = new Block();
        var second = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("first", "a"), first);
        registry.Register(ResourceKey.Create("second", "b"), second);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        Assert.Equal(1, manager.Atlas.GetRegion(ResourceKey.Create("first", "block/a")).Width);
        Assert.Equal(1, manager.Atlas.GetRegion(ResourceKey.Create("second", "block/b")).Width);
        var firstWriter = new RecordingWriter();
        var secondWriter = new RecordingWriter();
        manager.Get(first.DefaultState).Emit(default, DirectionFlags.None, firstWriter);
        manager.Get(second.DefaultState).Emit(default, DirectionFlags.None, secondWriter);
        Assert.Equal(4, firstWriter.Vertices.Count);
        Assert.Equal(6, firstWriter.Indices.Count);
        Assert.Equal(4, secondWriter.Vertices.Count);
        Assert.Equal(6, secondWriter.Indices.Count);
    }

    [Fact]
    public void ResolvesTextureVariableChain()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", """
                                                                               {
                                                                                 "textures": {
                                                                                   "a": "#b",
                                                                                   "b": "block/stone"
                                                                                 },
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": { "up": { "texture": "#a" } }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        TestDirectory.WriteResource(directory, "test/textures/block/stone.png", OnePixelPng);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(4, writer.Vertices.Count);
        Assert.Equal(6, writer.Indices.Count);
    }

    [Fact]
    public void IgnoresUnusedMissingTextureVariable()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", """
                                                                               {
                                                                                 "textures": {
                                                                                    "unused": "#missing",
                                                                                    "all": "block/stone"
                                                                                 },
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": { "up": { "texture": "#all" } }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        TestDirectory.WriteResource(directory, "test/textures/block/stone.png", OnePixelPng);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(4, writer.Vertices.Count);
        Assert.Equal(6, writer.Indices.Count);
    }

    [Fact]
    public void UsedMissingTextureVariableFallsBackToMissingCube()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", """
                                                                               {
                                                                                 "textures": { "all": "#missing" },
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": { "up": { "texture": "#all" } }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void TextureVariableCycleFallsBackToMissingCube()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", """
                                                                               {
                                                                                 "textures": {
                                                                                   "a": "#b",
                                                                                   "b": "#a"
                                                                                 },
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": { "up": { "texture": "#a" } }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    private sealed class RecordingWriter : IBlockMeshWriter
    {
        public List<(float X, float Y, float Z)> Vertices { get; } = [];

        public List<Vector2D<float>> TexCoords { get; } = [];

        public List<uint> Indices { get; } = [];

        public uint VertexCount => checked((uint)Vertices.Count);

        public void WriteVertex(
            Vector3D<float> position,
            Vector2D<float> texCoord,
            Vector3D<float> normal)
        {
            Vertices.Add((position.X, position.Y, position.Z));
            TexCoords.Add(texCoord);
        }

        public void WriteIndex(uint index)
        {
            Indices.Add(index);
        }
    }
}