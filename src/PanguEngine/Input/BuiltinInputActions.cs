using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Input;

/// <summary>
/// Provides the built-in logical input actions.
/// </summary>
public static class BuiltinInputActions
{
    /// <summary>The two-dimensional movement action.</summary>
    public static InputAction Move { get; } = new(InputValueType.Axis2D,
    [
        InputBinding.Axis2D("forward", BuiltinInputContexts.Game, Key.W, new Vector2D<double>(0, 1)),
        InputBinding.Axis2D("backward", BuiltinInputContexts.Game, Key.S, new Vector2D<double>(0, -1)),
        InputBinding.Axis2D("left", BuiltinInputContexts.Game, Key.A, new Vector2D<double>(-1, 0)),
        InputBinding.Axis2D("right", BuiltinInputContexts.Game, Key.D, new Vector2D<double>(1, 0))
    ]);

    /// <summary>The relative pointer look action.</summary>
    public static InputAction Look { get; } = new(InputValueType.Axis2D,
    [
        InputBinding.Axis2D(
            "default",
            BuiltinInputContexts.Game,
            InputSource.MouseMove,
            new Vector2D<double>(1, 1))
    ]);

    /// <summary>The block breaking action.</summary>
    public static InputAction BreakBlock { get; } = new(InputValueType.Button,
        [InputBinding.Button("default", BuiltinInputContexts.Game, MouseButton.Left)]);

    /// <summary>The block placement action.</summary>
    public static InputAction PlaceBlock { get; } = new(InputValueType.Button,
        [InputBinding.Button("default", BuiltinInputContexts.Game, MouseButton.Right)]);

    /// <summary>The pointer capture action.</summary>
    public static InputAction CapturePointer { get; } = new(InputValueType.Button,
        [InputBinding.Button("default", BuiltinInputContexts.Game, MouseButton.Left, priority: 100)]);

    /// <summary>The pause toggle action.</summary>
    public static InputAction TogglePause { get; } = new(InputValueType.Button,
    [
        InputBinding.Button("game", BuiltinInputContexts.Game, Key.Escape),
        InputBinding.Button("ui", BuiltinInputContexts.Ui, Key.Escape)
    ]);

    internal static void Register(IWritableRegistry<InputAction> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "move"), Move);
        registry.Register(ResourceKey.Create("pangu", "look"), Look);
        registry.Register(ResourceKey.Create("pangu", "break_block"), BreakBlock);
        registry.Register(ResourceKey.Create("pangu", "place_block"), PlaceBlock);
        registry.Register(ResourceKey.Create("pangu", "capture_pointer"), CapturePointer);
        registry.Register(ResourceKey.Create("pangu", "toggle_pause"), TogglePause);
    }
}
