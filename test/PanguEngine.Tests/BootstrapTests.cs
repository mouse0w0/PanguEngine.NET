using PanguEngine.Graphics.Vulkan;
using Silk.NET.Maths;

namespace PanguEngine.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void ParseOptionsSeparatesRemoteAddressAndPort()
    {
        var remote = Bootstrap.ParseOptions(["--connect", "::1", "--username", "Alice"]);
        Assert.Equal(System.Net.IPAddress.IPv6Loopback, remote.ConnectAddress);
        Assert.Equal(20202, remote.Port);
        Assert.Equal("Alice", remote.Username);
        var server = Bootstrap.ParseOptions(["--server", "--listen", "0.0.0.0", "--port", "20203"]);
        Assert.Equal(System.Net.IPAddress.Any, server.ListenAddress);
        Assert.Equal(20203, server.Port);
    }

    [Fact]
    public void ParseOptionsRejectsInvalidNetworkAndUsernameArguments()
    {
        string[][] inputs =
        [
            ["--connect"], ["--listen"], ["--port"],
            ["--connect", "localhost"], ["--connect", "127.0.0.1:20202"],
            ["--server", "--connect", "127.0.0.1"], ["--listen", "127.0.0.1"],
            ["--port", "20202"], ["--server", "--port", "0"],
            ["--server", "--port", "65536"], ["--server", "--port", "abc"],
            ["--connect", "::1", "--connect", "::1"],
            ["--server", "--listen", "::1", "--listen", "::1"],
            ["--server", "--port", "20202", "--port", "20202"],
            ["--connect", "127.0.0.1"], ["--username"],
            ["--username", "Alice", "--username", "Bob"]
        ];
        foreach (var input in inputs)
            Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(input));
    }

    [Fact]
    public void ParseOptionsAcceptsLocalUsernameWithoutInventingOne()
    {
        Assert.Null(Bootstrap.ParseOptions([]).Username);
        Assert.Equal("Local_Player", Bootstrap.ParseOptions(["--username", "Local_Player"]).Username);
    }

    [Fact]
    public void ParseOptionsDisablesVulkanValidationByDefault()
    {
        var options = Bootstrap.ParseOptions([]);

        Assert.Equal(VulkanValidationChecks.None, options.VulkanValidation);
        Assert.False(options.Server);
    }

    [Fact]
    public void ParseOptionsSelectsDedicatedServerAndPreservesModPaths()
    {
        var options = Bootstrap.ParseOptions(["--server", "--mod", "A"]);

        Assert.True(options.Server);
        Assert.Equal(["A"], options.ModPaths);
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

    [Theory]
    [InlineData("core", VulkanValidationChecks.Core)]
    [InlineData("sync", VulkanValidationChecks.Synchronization)]
    [InlineData("gpu-assisted", VulkanValidationChecks.GpuAssisted)]
    [InlineData("best-practices", VulkanValidationChecks.BestPractices)]
    [InlineData("legacy", VulkanValidationChecks.Legacy)]
    public void ParseOptionsSelectsVulkanValidationGroup(string name, VulkanValidationChecks expected)
    {
        var options = Bootstrap.ParseOptions(["--vulkan-validation", name]);

        Assert.Equal(expected, options.VulkanValidation);
    }

    [Theory]
    [InlineData("core,sync")]
    [InlineData("sync,core")]
    [InlineData("core,core,sync")]
    [InlineData(" CoRe , SyNc ")]
    public void ParseOptionsCombinesVulkanValidationGroups(string value)
    {
        var options = Bootstrap.ParseOptions(["--vulkan-validation", value]);

        Assert.Equal(
            VulkanValidationChecks.Core | VulkanValidationChecks.Synchronization,
            options.VulkanValidation);
    }

    [Fact]
    public void ParseOptionsMergesRepeatedVulkanValidationArguments()
    {
        var options = Bootstrap.ParseOptions(
            ["--vulkan-validation", "core", "--vulkan-validation", "sync,core"]);

        Assert.Equal(
            VulkanValidationChecks.Core | VulkanValidationChecks.Synchronization,
            options.VulkanValidation);
    }

    [Fact]
    public void ParseOptionsCombinesVulkanValidationWithModArguments()
    {
        var options = Bootstrap.ParseOptions(["--mod", "A", "--vulkan-validation", "core"]);

        Assert.Equal(["A"], options.ModPaths);
        Assert.Equal(VulkanValidationChecks.Core, options.VulkanValidation);
    }

    [Fact]
    public void ParseOptionsRejectsMissingVulkanValidationGroups()
    {
        Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--vulkan-validation"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(",core")]
    [InlineData("core,")]
    [InlineData("core,,sync")]
    [InlineData("core, ,sync")]
    [InlineData("core,unknown")]
    [InlineData("1")]
    public void ParseOptionsRejectsInvalidVulkanValidationGroups(string value)
    {
        Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions(["--vulkan-validation", value]));
    }

    [Theory]
    [InlineData("--gpu-validation")]
    [InlineData("--sync-validation")]
    public void ParseOptionsRejectsRemovedValidationArguments(string argument)
    {
        Assert.Throws<ArgumentException>(() => Bootstrap.ParseOptions([argument]));
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
