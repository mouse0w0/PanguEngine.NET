namespace PanguEngine.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void ParseOptionsDisablesGpuValidationByDefault()
    {
        var options = Bootstrap.ParseOptions([]);

        Assert.False(options.GpuValidation);
    }

    [Fact]
    public void ParseOptionsEnablesGpuValidation()
    {
        var options = Bootstrap.ParseOptions(["--gpu-validation"]);

        Assert.True(options.GpuValidation);
    }

    [Fact]
    public void ParseOptionsAllowsRepeatedGpuValidationArguments()
    {
        var options = Bootstrap.ParseOptions(["--gpu-validation", "--gpu-validation"]);

        Assert.True(options.GpuValidation);
    }

    [Fact]
    public void ParseOptionsCombinesGpuValidationWithModArguments()
    {
        var options = Bootstrap.ParseOptions(["--mod", "A", "--gpu-validation"]);

        Assert.Equal(["A"], options.ModPaths);
        Assert.True(options.GpuValidation);
    }

    [Fact]
    public void ParseOptionsRejectsSeparatedGpuValidationValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--gpu-validation", "true"]));

        Assert.Contains("Unknown argument 'true'", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsInlineGpuValidationValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--gpu-validation=true"]));

        Assert.Contains("Unknown argument '--gpu-validation=true'", exception.Message);
    }

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