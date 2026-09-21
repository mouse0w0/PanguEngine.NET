using PanguEngine.Registries;

namespace PanguEngine.Input;

/// <summary>
/// Provides the built-in input contexts.
/// </summary>
public static class BuiltinInputContexts
{
    /// <summary>The general game input context.</summary>
    public static InputContext Game { get; } = new(InputScope.Game);

    /// <summary>The UI input context.</summary>
    public static InputContext Ui { get; } = new(
        InputScope.Ui,
        captureMask: InputCaptureMask.All,
        pointerCapturePolicy: PointerCapturePolicy.Suspend);

    internal static void Register(IWritableRegistry<InputContext> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "game"), Game);
        registry.Register(ResourceKey.Create("pangu", "ui"), Ui);
    }
}
