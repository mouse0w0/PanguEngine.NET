using PanguEngine.Graphics.Vulkan;
using PanguEngine.Input;

namespace PanguEngine.Tests.Graphics;

public sealed class SdlMouseMappingTests
{
    [Theory]
    [InlineData(1, MouseButton.Left)]
    [InlineData(2, MouseButton.Middle)]
    [InlineData(3, MouseButton.Right)]
    [InlineData(4, MouseButton.Button4)]
    [InlineData(12, MouseButton.Button12)]
    public void MapsSdlButtonNumbers(byte button, MouseButton expected)
    {
        Assert.True(SdlMouseMapping.TryGetButton(button, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RejectsUnsupportedSdlButtonNumbers(byte button)
    {
        Assert.False(SdlMouseMapping.TryGetButton(button, out _));
    }
}
