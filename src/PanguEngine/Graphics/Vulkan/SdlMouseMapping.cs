using PanguEngine.Input;

namespace PanguEngine.Graphics.Vulkan;

internal static class SdlMouseMapping
{
    internal static IReadOnlyList<MouseButton> SupportedButtons { get; } =
    [
        MouseButton.Left,
        MouseButton.Middle,
        MouseButton.Right,
        MouseButton.Button4,
        MouseButton.Button5,
        MouseButton.Button6,
        MouseButton.Button7,
        MouseButton.Button8,
        MouseButton.Button9,
        MouseButton.Button10,
        MouseButton.Button11,
        MouseButton.Button12
    ];

    internal static bool TryGetButton(byte button, out MouseButton result)
    {
        result = button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            >= 4 and <= 12 => (MouseButton)(button - 1),
            _ => MouseButton.Unknown
        };

        return result != MouseButton.Unknown;
    }
}
