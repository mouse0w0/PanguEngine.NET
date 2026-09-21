using PanguEngine.Registries;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the built-in HUD component definitions.
/// </summary>
public static class BuiltinHuds
{
    /// <summary>
    /// Gets the definition of the built-in crosshair component.
    /// </summary>
    public static HudDefinition Crosshair { get; } = new(static () => new CrosshairHud());

    /// <summary>
    /// Gets the definition of the built-in debug information component.
    /// </summary>
    public static HudDefinition DebugInfo { get; } = new(static () => new DebugInfoHud());

    internal static void Register(IWritableRegistry<HudDefinition> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "crosshair"), Crosshair);
        registry.Register(ResourceKey.Create("pangu", "debug_info"), DebugInfo);
    }
}
