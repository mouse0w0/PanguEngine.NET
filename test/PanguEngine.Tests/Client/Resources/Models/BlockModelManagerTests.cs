using Microsoft.Extensions.Logging;
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

    private static readonly BlockProperty<bool> Powered = BlockProperty.CreateBoolean("powered");

    private static readonly BlockProperty<Direction> Facing = BlockProperty.CreateEnum(
        "facing",
        Direction.North,
        Direction.South);

    [Fact]
    public void MissingAppearanceUsesMissingCube()
    {
        var registry = new Registry<Block>(RegistryKeys.Block);
        var stone = new Block();
        registry.Register(ResourceKey.Create("test", "stone"), stone);
        using var resources = new ResourceManager([]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() => manager.Get(stone.DefaultState, default));
        Assert.Throws<InvalidOperationException>(() => manager.Atlas);

        manager.Load();

        var writer = new RecordingWriter();
        var model = manager.Get(stone.DefaultState, default);
        Assert.Same(model, manager.Get(new Block().DefaultState, default));
        model.Emit(default, DirectionFlags.None, writer);

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
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/empty.json", """
                                                                               { "elements": [] }
                                                                               """);
        WriteAppearance(directory, "test", "air", "test:block/empty");
        var registry = new Registry<Block>(RegistryKeys.Block);
        var air = new AirBlock();
        var registered = new Block();
        registry.Register(ResourceKey.Create("test", "air"), air);
        registry.Register(ResourceKey.Create("test", "registered"), registered);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);
        manager.Load();

        var airWriter = new RecordingWriter();
        var airModel = manager.Get(air.DefaultState, default);
        airModel.Emit(default, DirectionFlags.None, airWriter);
        Assert.Empty(airWriter.Vertices);

        var unknownWriter = new RecordingWriter();
        var missingModel = manager.Get(new Block().DefaultState, default);
        missingModel.Emit(default, DirectionFlags.None, unknownWriter);
        Assert.NotSame(missingModel, airModel);
        Assert.Equal(24, unknownWriter.Vertices.Count);
    }

    [Theory]
    [InlineData("down", 1f, 5f, 9f, 13f)]
    [InlineData("up", 1f, 3f, 9f, 11f)]
    [InlineData("north", 7f, 6f, 15f, 14f)]
    [InlineData("south", 1f, 6f, 9f, 14f)]
    [InlineData("west", 3f, 6f, 11f, 14f)]
    [InlineData("east", 5f, 6f, 13f, 14f)]
    public void ResolvesAutomaticUv(
        string direction,
        float u0,
        float v0,
        float u1,
        float v1)
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", $$"""
                                                                                 {
                                                                                   "elements": [
                                                                                     {
                                                                                       "from": [1, 2, 3],
                                                                                       "to": [9, 10, 11],
                                                                                       "faces": { "{{direction}}": { "texture": "block/stone" } }
                                                                                     }
                                                                                   ]
                                                                                 }
                                                                                 """);
        TestDirectory.WriteResource(directory, "test/textures/block/stone.png", OnePixelPng);
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
        var region = manager.Atlas.GetRegion(ResourceKey.Create("test", "block/stone"));

        Vector2D<float> Map(float u, float v) => new(
            region.U0 + u / 16 * (region.U1 - region.U0),
            region.V0 + v / 16 * (region.V1 - region.V0));

        Assert.Equal(
            [Map(u0, v0), Map(u0, v1), Map(u1, v1), Map(u1, v0)],
            writer.TexCoords);
    }

    [Fact]
    public void ResolvesExplicitUvFromJson()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/model.json", """
                                                                               {
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": {
                                                                                       "up": { "texture": "block/stone", "uv": [2, 4, 10, 12] }
                                                                                     }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        TestDirectory.WriteResource(directory, "test/textures/block/stone.png", OnePixelPng);
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
        var region = manager.Atlas.GetRegion(ResourceKey.Create("test", "block/stone"));

        Vector2D<float> Map(float u, float v) => new(
            region.U0 + u / 16 * (region.U1 - region.U0),
            region.V0 + v / 16 * (region.V1 - region.V0));

        Assert.Equal(
            [Map(2, 4), Map(2, 12), Map(10, 12), Map(10, 4)],
            writer.TexCoords);
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
        WriteAppearance(directory, "test", "child", "test:block/child");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "child"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
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
        WriteAppearance(directory, "test", "a", "test:block/a");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "a"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
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
        WriteAppearance(directory, "child", "model", "child:block/model");
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
        WriteAppearance(directory, "first", "a", "first:block/a");
        WriteAppearance(directory, "second", "b", "second:block/b");
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
        manager.Get(first.DefaultState, default).Emit(default, DirectionFlags.None, firstWriter);
        manager.Get(second.DefaultState, default).Emit(default, DirectionFlags.None, secondWriter);
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
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
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
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
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
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
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
        WriteAppearance(directory, "test", "model", "test:block/model");
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void MissingAirAppearanceUsesMissingCube()
    {
        var air = new AirBlock();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "air"), air);
        using var resources = new ResourceManager([]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(air.DefaultState, default).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void MissingCandidateFallsBackEntireAppearance()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/valid.json", """
                                                                               {
                                                                                 "elements": [
                                                                                   {
                                                                                     "from": [0, 0, 0],
                                                                                     "to": [16, 16, 16],
                                                                                     "faces": { "up": { "texture": "block/stone" } }
                                                                                   }
                                                                                 ]
                                                                               }
                                                                               """);
        TestDirectory.WriteResource(directory, "test/appearances/block/model.json", """
            {
              "variants": {
                "": [
                  { "model": "test:block/valid" },
                  { "model": "test:block/missing" }
                ]
              }
            }
            """);
        var block = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "model"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var writer = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, writer);
        Assert.Equal(24, writer.Vertices.Count);
        Assert.Equal(36, writer.Indices.Count);
    }

    [Fact]
    public void SharesBakedModelForMatchingModelAndRotation()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/shared.json", """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "block/stone" } }
                }
              ]
            }
            """);
        TestDirectory.WriteResource(directory, "test/appearances/block/first.json", """
            {
              "variants": {
                "": [
                  { "model": "test:block/shared", "rotation": { "y": 90 } }
                ]
              }
            }
            """);
        TestDirectory.WriteResource(directory, "test/appearances/block/second.json", """
            {
              "variants": {
                "": [
                  { "model": "test:block/shared", "rotation": { "y": 90 } }
                ]
              }
            }
            """);
        TestDirectory.WriteResource(directory, "test/appearances/block/third.json", """
            {
              "variants": {
                "": [
                  { "model": "test:block/shared", "rotation": { "y": 180 } }
                ]
              }
            }
            """);
        var first = new Block();
        var second = new Block();
        var third = new Block();
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "first"), first);
        registry.Register(ResourceKey.Create("test", "second"), second);
        registry.Register(ResourceKey.Create("test", "third"), third);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var firstModel = manager.Get(first.DefaultState, default);
        var secondModel = manager.Get(second.DefaultState, default);
        var thirdModel = manager.Get(third.DefaultState, default);
        Assert.Same(firstModel, secondModel);
        Assert.NotSame(firstModel, thirdModel);
    }

    [Fact]
    public void SelectsModelFromCanonicalBlockState()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/off.json", """
                                                                             {
                                                                               "elements": [
                                                                                 {
                                                                                   "from": [0, 0, 0],
                                                                                   "to": [16, 16, 16],
                                                                                   "faces": { "up": { "texture": "block/stone" } }
                                                                                 }
                                                                               ]
                                                                             }
                                                                             """);
        TestDirectory.WriteResource(directory, "test/models/block/on.json", """
                                                                            {
                                                                              "elements": [
                                                                                {
                                                                                  "from": [0, 0, 0],
                                                                                  "to": [16, 16, 16],
                                                                                  "faces": {
                                                                                    "up": { "texture": "block/stone" },
                                                                                    "down": { "texture": "block/stone" }
                                                                                  }
                                                                                }
                                                                              ]
                                                                            }
                                                                            """);
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "powered=false": [
                  { "model": "test:block/off" }
                ],
                "powered=true": [
                  { "model": "test:block/on" }
                ]
              }
            }
            """);
        var block = new Block(Powered);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "machine"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        var offWriter = new RecordingWriter();
        manager.Get(block.DefaultState, default).Emit(default, DirectionFlags.None, offWriter);
        var onWriter = new RecordingWriter();
        manager.Get(block.DefaultState.With(Powered, true), default)
            .Emit(default, DirectionFlags.None, onWriter);
        Assert.Equal(4, offWriter.Vertices.Count);
        Assert.Equal(8, onWriter.Vertices.Count);
    }

    [Fact]
    public void MissingModelInExactVariantFallsBackEntireAppearance()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/models/block/off.json", """
                                                                             {
                                                                               "elements": [
                                                                                 {
                                                                                   "from": [0, 0, 0],
                                                                                   "to": [16, 16, 16],
                                                                                   "faces": { "up": { "texture": "block/stone" } }
                                                                                 }
                                                                               ]
                                                                             }
                                                                             """);
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "": [
                  { "model": "test:block/off" }
                ],
                "powered=true": [
                  { "model": "test:block/missing" }
                ]
              }
            }
            """);
        var block = new Block(Powered);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "machine"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var manager = new BlockModelManager(resources, registry, 4096u, NullLogger.Instance);

        manager.Load();

        foreach (var state in block.StateDefinition.States)
        {
            var writer = new RecordingWriter();
            manager.Get(state, default).Emit(default, DirectionFlags.None, writer);
            Assert.Equal(24, writer.Vertices.Count);
            Assert.Equal(36, writer.Indices.Count);
        }
    }

    [Fact]
    public void OverlappingVariantsLogErrorAndFallBackEntireAppearance()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "facing=north": { "model": "test:block/north" },
                "powered=true": { "model": "test:block/on" },
                "": { "model": "test:block/fallback" }
              }
            }
            """);
        var block = new Block(Facing, Powered);
        var registry = new Registry<Block>(RegistryKeys.Block);
        registry.Register(ResourceKey.Create("test", "machine"), block);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var logger = new CapturingLogger();
        var manager = new BlockModelManager(resources, registry, 4096u, logger);

        manager.Load();

        var entry = Assert.Single(logger.Entries.Where(entry => entry.Level == LogLevel.Error));
        Assert.Contains("Falling back to missing block model", entry.Message);
        foreach (var state in block.StateDefinition.States)
        {
            var writer = new RecordingWriter();
            manager.Get(state, default).Emit(default, DirectionFlags.None, writer);
            Assert.Equal(24, writer.Vertices.Count);
            Assert.Equal(36, writer.Indices.Count);
        }
    }

    private static void WriteAppearance(
        TestDirectory directory,
        string ns,
        string blockPath,
        string modelReference)
    {
        TestDirectory.WriteResource(
            directory,
            $"{ns}/appearances/block/{blockPath}.json",
            $$"""
              {
                "variants": {
                  "": [
                    { "model": "{{modelReference}}" }
                  ]
                }
              }
              """);
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        internal IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
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