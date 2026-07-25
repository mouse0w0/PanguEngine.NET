using PanguEngine.Client.Resources.Models;
using PanguEngine.Registries;
using PanguEngine.Resources;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class JsonBlockModelLoaderTests
{
    [Fact]
    public void LoadsModelJsonWithoutResolvingParent()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/stone.json", """
            {
              "parent": "base",
              "textures": {
                "all": "block/stone",
                "detail": "other:block/detail"
              },
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "#all", "cull": ["up"] } }
                }
              ]
            }
            """);
        using var resources = CreateResources(directory.Path);

        var model = new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/stone"));

        Assert.Equal(ResourceKey.Create("pangu", "block/stone"), model.SourceKey);
        Assert.Equal("base", model.ParentReference);
        Assert.Equal(
            new BlockTextureValue.Resource(ResourceKey.Create("pangu", "block/stone")),
            model.Textures["all"]);
        Assert.Equal(
            new BlockTextureValue.Resource(ResourceKey.Create("other", "block/detail")),
            model.Textures["detail"]);
        var element = Assert.Single(model.Elements!);
        var face = element.Faces["up"];
        Assert.Equal(new BlockTextureValue.Variable("all"), face.Texture);
        Assert.Equal(0, face.Rotation);
        Assert.Equal(["up"], face.Cull);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("90", 90)]
    [InlineData("180", 180)]
    [InlineData("270", 270)]
    public void LoadsFaceRotation(string jsonRotation, int expectedRotation)
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/model.json", $$"""
              {
                "elements": [
                  {
                    "from": [0, 0, 0],
                    "to": [16, 16, 16],
                    "faces": {
                      "up": { "texture": "block/test", "rotation": {{jsonRotation}} }
                    }
                  }
                ]
              }
              """);
        using var resources = CreateResources(directory.Path);

        var model = new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/model"));

        Assert.Equal(expectedRotation, model.Elements!.Single().Faces["up"].Rotation);
    }

    [Theory]
    [InlineData("-90")]
    [InlineData("45")]
    [InlineData("90.0")]
    [InlineData("90.5")]
    [InlineData("null")]
    [InlineData("\"90\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    public void RejectsInvalidFaceRotation(string jsonRotation)
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", $$"""
              {
                "elements": [
                  {
                    "from": [0, 0, 0],
                    "to": [16, 16, 16],
                    "faces": {
                      "up": { "texture": "block/test", "rotation": {{jsonRotation}} }
                    }
                  }
                ]
              }
              """);
        using var resources = CreateResources(directory.Path);

        var exception = Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));

        Assert.Contains("Block model 'pangu:block/invalid'", exception.Message);
        Assert.Contains("element 0", exception.Message);
        Assert.Contains("face 'up'", exception.Message);
    }

    [Fact]
    public void PreservesExplicitEmptyElements()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/empty.json", """
            {
              "elements": []
            }
            """);
        using var resources = CreateResources(directory.Path);

        var model = new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/empty"));

        Assert.NotNull(model.Elements);
        Assert.Empty(model.Elements);
    }

    [Fact]
    public void RejectsUnknownJsonFields()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", """
            {
              "unknown": true
            }
            """);
        using var resources = CreateResources(directory.Path);

        Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));
    }

    [Fact]
    public void RejectsInvalidFaceData()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", """
            {
              "elements": [
                {
                  "from": [0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "block/test", "cull": ["invalid"] } }
                }
              ]
            }
            """);
        using var resources = CreateResources(directory.Path);

        Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));
    }

    [Fact]
    public void RejectsUnknownFaceDirection()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "invalid": { "texture": "block/test" } }
                }
              ]
            }
            """);
        using var resources = CreateResources(directory.Path);

        var exception = Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));

        Assert.Contains("unknown face direction 'invalid'", exception.Message);
    }

    [Fact]
    public void RejectsUnknownCullDirection()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "block/test", "cull": ["invalid"] } }
                }
              ]
            }
            """);
        using var resources = CreateResources(directory.Path);

        var exception = Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));

        Assert.Contains("face 'up' has unknown cull direction 'invalid'", exception.Message);
    }

    [Fact]
    public void RejectsEmptyTextureVariable()
    {
        using var directory = TestDirectory.Create();
        TestDirectory.WriteResource(directory, "pangu/models/block/invalid.json", """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "faces": { "up": { "texture": "#" } }
                }
              ]
            }
            """);
        using var resources = CreateResources(directory.Path);

        Assert.Throws<InvalidDataException>(() => new JsonBlockModelLoader(resources)
            .Load(ResourceKey.Create("pangu", "block/invalid")));
    }

    private static ResourceManager CreateResources(string root)
    {
        return new ResourceManager([new DirectoryResourceSource(root)]);
    }
}