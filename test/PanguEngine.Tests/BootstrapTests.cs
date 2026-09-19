using Silk.NET.Maths;

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
    public void ParseOptionsLeavesWindowOptionsUnsetByDefault()
    {
        var options = Bootstrap.ParseOptions([]);

        Assert.Null(options.WindowTitle);
        Assert.Null(options.WindowSize);
        Assert.Null(options.WindowMode);
    }

    [Fact]
    public void ParseOptionsParsesWindowTitleAndSize()
    {
        var options = Bootstrap.ParseOptions(["--window-title", "Example Mod", "--window-size", "1280x720"]);

        Assert.Equal("Example Mod", options.WindowTitle);
        Assert.True(options.WindowSize.HasValue);
        Assert.Equal(new Vector2D<int>(1280, 720), options.WindowSize.Value);
    }

    [Theory]
    [InlineData("windowed", WindowMode.Windowed)]
    [InlineData("maximized", WindowMode.Maximized)]
    [InlineData("fullscreen", WindowMode.Fullscreen)]
    [InlineData("borderless", WindowMode.Borderless)]
    public void ParseOptionsParsesWindowMode(string value, WindowMode expected)
    {
        var options = Bootstrap.ParseOptions(["--window-mode", value]);

        Assert.True(options.WindowMode.HasValue);
        Assert.Equal(expected, options.WindowMode.Value);
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
    public void ParseOptionsRejectsMissingWindowTitle()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-title"]));

        Assert.Contains("--window-title requires a title", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsEmptyWindowTitle()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-title", ""]));

        Assert.Contains("--window-title cannot be empty", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsMissingWindowSize()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-size"]));

        Assert.Contains("--window-size requires a size", exception.Message);
    }

    [Theory]
    [InlineData("1280")]
    [InlineData("1280x")]
    [InlineData("x720")]
    [InlineData("0x720")]
    [InlineData("1280x0")]
    [InlineData("-1x720")]
    public void ParseOptionsRejectsInvalidWindowSize(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-size", value]));

        Assert.Contains("--window-size must use a positive WIDTHxHEIGHT value", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsMissingWindowMode()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-mode"]));

        Assert.Contains("--window-mode requires a mode", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsUnknownWindowMode()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--window-mode", "floating"]));

        Assert.Contains("Unknown window mode 'floating'", exception.Message);
    }

    [Fact]
    public void ParseOptionsRejectsUnknownArgument()
    {
        var exception = Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--unknown"]));

        Assert.Contains("Unknown argument '--unknown'", exception.Message);
    }
}