using PanguEngine.Desktop;

namespace PanguEngine.Tests.Client;

public sealed class FileDialogFilterTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("tar.gz")]
    [InlineData("file-type_2")]
    public void ValidExtensionsArePreserved(string extension)
    {
        var filter = new FileDialogFilter("Files", extension);

        Assert.Equal("Files", filter.Name);
        Assert.Equal([extension], filter.Extensions);
    }

    [Fact]
    public void MultipleExtensionsArePreserved()
    {
        var filter = new FileDialogFilter("Images", "png", "jpg", "jpeg");

        Assert.Equal(["png", "jpg", "jpeg"], filter.Extensions);
    }

    [Fact]
    public void AllFilesFilterIsAccepted()
    {
        var filter = new FileDialogFilter("All files", "*");

        Assert.Equal(["*"], filter.Extensions);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyOrWhitespaceNameIsRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new FileDialogFilter(name, "png"));
    }

    [Fact]
    public void NullNameIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new FileDialogFilter(null!, "png"));
    }

    [Fact]
    public void NullExtensionsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new FileDialogFilter("Files", null!));
    }

    [Fact]
    public void EmptyExtensionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new FileDialogFilter("Files"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".png")]
    [InlineData("my file")]
    [InlineData("images/png")]
    [InlineData("images\\png")]
    [InlineData("png;")]
    public void InvalidExtensionIsRejected(string extension)
    {
        Assert.Throws<ArgumentException>(() => new FileDialogFilter("Files", extension));
    }

    [Fact]
    public void AllFilesCannotBeCombinedWithExtensions()
    {
        Assert.Throws<ArgumentException>(() => new FileDialogFilter("Files", "*", "png"));
    }

    [Fact]
    public void ExtensionsAreCopied()
    {
        var extensions = new[] { "png" };
        var filter = new FileDialogFilter("Images", extensions);

        extensions[0] = "jpg";

        Assert.Equal(["png"], filter.Extensions);
    }
}
