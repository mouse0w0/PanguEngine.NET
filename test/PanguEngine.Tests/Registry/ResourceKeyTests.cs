using PanguEngine.Registry;

namespace PanguEngine.Tests.Registry;

public sealed class ResourceKeyTests
{
    [Theory]
    [InlineData("pangu:stone", "pangu", "stone")]
    [InlineData("pangu:block/stone", "pangu", "block/stone")]
    [InlineData("my.mod:block_name", "my.mod", "block_name")]
    public void ParseAcceptsValidKeys(string text, string expectedNamespace, string expectedPath)
    {
        var key = ResourceKey.Parse(text);

        Assert.Equal(expectedNamespace, key.Namespace);
        Assert.Equal(expectedPath, key.Path);
        Assert.Equal(text, key.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("pangu")]
    [InlineData("pangu:")]
    [InlineData(":stone")]
    [InlineData("pan-gu:stone")]
    [InlineData("pangu:stone-brick")]
    [InlineData("Pangu:stone")]
    [InlineData("pangu:Stone")]
    [InlineData("pangu:stone brick")]
    [InlineData("pangu:stone:extra")]
    public void ParseRejectsInvalidKeys(string text)
    {
        Assert.Throws<FormatException>(() => ResourceKey.Parse(text));
    }

    [Fact]
    public void TryParseReturnsFalseForInvalidKey()
    {
        var parsed = ResourceKey.TryParse("pangu:stone-brick", out var key);

        Assert.False(parsed);
        Assert.Equal(default, key);
    }

    [Fact]
    public void CreateRejectsInvalidParts()
    {
        Assert.Throws<ArgumentException>(() => ResourceKey.Create("", "stone"));
        Assert.Throws<ArgumentException>(() => ResourceKey.Create("pangu", ""));
        Assert.Throws<ArgumentException>(() => ResourceKey.Create("pan-gu", "stone"));
        Assert.Throws<ArgumentException>(() => ResourceKey.Create("pangu", "stone-brick"));
    }
}