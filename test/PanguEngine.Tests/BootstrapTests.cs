namespace PanguEngine.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void ParseOptionsCollectsRepeatedModArguments()
    {
        var options = Bootstrap.ParseOptions(["--mod", "A", "--mod", "B"]);

        Assert.Equal(["A", "B"], options.ModPaths);
    }

    [Fact]
    public void ParseOptionsRejectsMissingModPath()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--mod"]));

        Assert.Contains("--mod requires a path", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsEmptyModPath()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--mod", ""]));

        Assert.Contains("--mod path cannot be empty", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsUnknownArgument()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--unknown"]));

        Assert.Contains("Unknown argument '--unknown'", exception.Message);
    }
}