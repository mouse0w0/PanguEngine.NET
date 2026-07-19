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
        Assert.Equal(["up"], face.Cull);
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