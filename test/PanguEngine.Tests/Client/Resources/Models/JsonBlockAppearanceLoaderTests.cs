using PanguEngine.Client.Resources.Models;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World;
using PanguEngine.World.Blocks;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class JsonBlockAppearanceLoaderTests
{
    private static readonly BlockProperty<Direction> Facing = BlockProperty.CreateEnum(
        "facing",
        Direction.North,
        Direction.South);

    private static readonly BlockProperty<bool> Powered = BlockProperty.CreateBoolean("powered");

    [Fact]
    public void BindsPartialAndDefaultVariantsToCanonicalStates()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "": [
                  { "model": "block/fallback" }
                ],
                "facing=north": [
                  {
                    "model": "other:block/north",
                    "weight": 3,
                    "rotation": { "x": 90, "y": 180, "z": 270 }
                  }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block(Facing, Powered);

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "machine"), block);

        var northOff = block.DefaultState;
        var northOn = northOff.With(Powered, true);
        var south = northOff.With(Facing, Direction.South);
        Assert.Equal(ResourceKey.Create("other", "block/north"), appearance.Variants[northOff][0].ModelKey);
        Assert.Equal(ResourceKey.Create("other", "block/north"), appearance.Variants[northOn][0].ModelKey);
        Assert.Equal(3, appearance.Variants[northOn][0].Weight);
        Assert.Equal(new BlockModelRotation(90, 180, 270), appearance.Variants[northOn][0].Rotation);
        Assert.Equal(ResourceKey.Create("test", "block/fallback"), appearance.Variants[south][0].ModelKey);
        Assert.Equal(block.StateDefinition.States.Count, appearance.Variants.Count);
    }

    [Fact]
    public void AcceptsConditionsInAnyPropertyOrder()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "powered=true,facing=north": { "model": "block/on" },
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block(Facing, Powered);

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "machine"), block);

        var state = block.DefaultState.With(Powered, true);
        Assert.Equal(ResourceKey.Create("test", "block/on"), appearance.Variants[state][0].ModelKey);
    }

    [Fact]
    public void RejectsOverlappingPartialVariants()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "facing=north": { "model": "block/north" },
                "powered=true": { "model": "block/on" },
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        var exception = Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(Facing, Powered)));

        Assert.Contains("facing=north", exception.Message);
        Assert.Contains("powered=true", exception.Message);
        Assert.Contains("facing=north,powered=true", exception.Message);
    }

    [Fact]
    public void RejectsOverlappingPartialAndCompleteVariants()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "facing=north": { "model": "block/north" },
                "facing=north,powered=true": { "model": "block/north_on" },
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(Facing, Powered)));
    }

    [Fact]
    public void RejectsEquivalentVariantsWithDifferentConditionOrder()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "facing=north,powered=true": { "model": "block/first" },
                "powered=true,facing=north": { "model": "block/second" },
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(Facing, Powered)));
    }

    [Fact]
    public void LoadsExhaustiveVariantsWithoutDefault()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "powered=false": [
                  { "model": "block/off" }
                ],
                "powered=true": [
                  { "model": "block/on" }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block(Powered);

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "machine"), block);

        Assert.Equal(
            ResourceKey.Create("test", "block/off"),
            appearance.Variants[block.DefaultState][0].ModelKey);
        Assert.Equal(
            ResourceKey.Create("test", "block/on"),
            appearance.Variants[block.DefaultState.With(Powered, true)][0].ModelKey);
    }

    [Fact]
    public void LoadsSingleCandidateObject()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/stone.json", """
            {
              "variants": {
                "": {
                  "model": "block/a",
                  "weight": 2,
                  "rotation": { "y": 90 }
                }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block();

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "stone"), block);

        var candidate = Assert.Single(appearance.Variants[block.DefaultState]);
        Assert.Equal(ResourceKey.Create("test", "block/a"), candidate.ModelKey);
        Assert.Equal(2, candidate.Weight);
        Assert.Equal(new BlockModelRotation(0, 90, 0), candidate.Rotation);
    }

    [Fact]
    public void DefaultsMissingRotationAxesToZero()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/stone.json", """
            {
              "variants": {
                "": [
                  { "model": "block/a", "rotation": { "y": 90 } }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block();

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "stone"), block);

        Assert.Equal(new BlockModelRotation(0, 90, 0), appearance.Variants[block.DefaultState][0].Rotation);
    }

    [Fact]
    public void PreservesDuplicateCandidates()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/stone.json", """
            {
              "variants": {
                "": [
                  { "model": "block/a", "weight": 2 },
                  { "model": "block/a", "weight": 5 }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block();

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "stone"), block);

        var candidates = appearance.Variants[block.DefaultState];
        Assert.Equal(2, candidates.Count);
        Assert.Equal(2, candidates[0].Weight);
        Assert.Equal(5, candidates[1].Weight);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"variants\": null }")]
    [InlineData("{ \"variants\": [] }")]
    [InlineData("{ \"variants\": {} }")]
    [InlineData("{ \"models\": [{ \"model\": \"block/a\" }] }")]
    [InlineData("{ \"variants\": { \"powered=false\": [{ \"model\": \"block/a\" }] } }")]
    [InlineData("{ \"variants\": { \"powered=false,facing=north\": [{ \"model\": \"block/a\" }] } }")]
    [InlineData("{ \"variants\": { \"facing=north,powered=false,extra=true\": [{ \"model\": \"block/a\" }] } }")]
    [InlineData("{ \"variants\": { \"facing=up,powered=false\": [{ \"model\": \"block/a\" }] } }")]
    [InlineData(
        "{ \"variants\": { \"facing=north,facing=south\": [{ \"model\": \"block/a\" }], \"\": [{ \"model\": \"block/fallback\" }] } }")]
    [InlineData(
        "{ \"variants\": { \"facing=north,\": [{ \"model\": \"block/a\" }], \"\": [{ \"model\": \"block/fallback\" }] } }")]
    [InlineData(
        "{ \"variants\": { \"facing==north\": [{ \"model\": \"block/a\" }], \"\": [{ \"model\": \"block/fallback\" }] } }")]
    [InlineData("{ \"variants\": { \"\": null } }")]
    [InlineData("{ \"variants\": { \"\": \"block/a\" } }")]
    [InlineData("{ \"variants\": { \"\": 1 } }")]
    [InlineData("{ \"variants\": { \"\": true } }")]
    [InlineData("{ \"variants\": { \"\": {} } }")]
    [InlineData("{ \"variants\": { \"\": [] } }")]
    [InlineData("{ \"variants\": { \"\": [null] } }")]
    [InlineData("{ \"variants\": { \"\": [{}] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": null }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"\" }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": 1 }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"weight\": 0 }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"weight\": -1 }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"weight\": 1.0 }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"weight\": \"1\" }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"rotation\": 90 }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"rotation\": { \"x\": 45 } }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"rotation\": { \"x\": 90.0 } }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"rotation\": { \"unknown\": 0 } }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\", \"unknown\": true }] } }")]
    [InlineData("{ \"variants\": { \"\": [{ \"model\": \"block/a\" }] }, \"unknown\": true }")]
    public void RejectsInvalidAppearance(string json)
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", json);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(Facing, Powered)));
    }

    [Fact]
    public void RejectsConditionForStatelessBlock()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "powered=true": { "model": "block/on" },
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block()));
    }

    [Fact]
    public void RejectsWeightOverflowWithinVariant()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "": [
                  { "model": "block/a", "weight": 2147483647 },
                  { "model": "block/b", "weight": 1 }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block()));
    }

    [Fact]
    public void AllowsMaximumWeightInSeparateVariants()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/machine.json", """
            {
              "variants": {
                "powered=false": [
                  { "model": "block/off", "weight": 2147483647 }
                ],
                "powered=true": [
                  { "model": "block/on", "weight": 2147483647 }
                ]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var block = new Block(Powered);

        var appearance = new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "machine"), block);

        Assert.Equal(int.MaxValue, appearance.Variants[block.DefaultState][0].Weight);
        Assert.Equal(
            int.MaxValue,
            appearance.Variants[block.DefaultState.With(Powered, true)][0].Weight);
    }

    [Fact]
    public void RejectsDuplicateVariantKeys()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "": [{ "model": "block/a" }],
                "": [{ "model": "block/b" }]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block()));
    }

    [Fact]
    public void RejectsIncompleteCoverageWithoutDefault()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "powered=false": [{ "model": "block/off" }]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(Powered)));
    }

    [Fact]
    public void RejectsAmbiguousPropertyValueKeysEvenWithOnlyFallback()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "": [{ "model": "block/model" }]
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var mode = BlockProperty.CreateEnum("mode", AmbiguousValue.Value, AmbiguousValue.VALUE);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(mode)));
    }

    [Fact]
    public void RejectsPropertyValueKeyContainingSeparatorEvenWithOnlyFallback()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "test/appearances/block/invalid.json", """
            {
              "variants": {
                "": { "model": "block/fallback" }
              }
            }
            """);
        using var resources = new ResourceManager([new DirectoryResourceSource(directory.Path)]);
        var mode = BlockProperty.CreateEnum("mode", CombinedValue.First | CombinedValue.Second);

        Assert.Throws<InvalidDataException>(() => new JsonBlockAppearanceLoader(resources)
            .Load(ResourceKey.Create("test", "invalid"), new Block(mode)));
    }

    private enum AmbiguousValue
    {
        Value,
        VALUE
    }

    [Flags]
    private enum CombinedValue
    {
        First = 1,
        Second = 2
    }
}