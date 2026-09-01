using PanguEngine.Graphics.Vulkan;
using PanguEngine.Input;
using SDL;

namespace PanguEngine.Tests.Graphics;

public sealed class SdlKeyMappingTests
{
    [Theory]
    [InlineData(SDL_Scancode.SDL_SCANCODE_A, Key.A)]
    [InlineData(SDL_Scancode.SDL_SCANCODE_0, Key.Number0)]
    [InlineData(SDL_Scancode.SDL_SCANCODE_LCTRL, Key.ControlLeft)]
    [InlineData(SDL_Scancode.SDL_SCANCODE_RGUI, Key.SuperRight)]
    [InlineData(SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH, Key.World1)]
    [InlineData(SDL_Scancode.SDL_SCANCODE_NONUSHASH, Key.World2)]
    public void MapsSupportedScancodes(SDL_Scancode scancode, Key expected)
    {
        Assert.True(SdlKeyMapping.TryGetKey(scancode, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UnknownScancodeDoesNotMapToUnknownKey()
    {
        Assert.False(SdlKeyMapping.TryGetKey(SDL_Scancode.SDL_SCANCODE_UNKNOWN, out _));
    }

    [Fact]
    public void KeyMappingRoundTripsThroughScancode()
    {
        foreach (var key in SdlKeyMapping.SupportedKeys)
        {
            Assert.True(SdlKeyMapping.TryGetScancode(key, out var scancode));
            Assert.True(SdlKeyMapping.TryGetKey(scancode, out var roundTripped));
            Assert.Equal(key, roundTripped);
        }
    }
}
