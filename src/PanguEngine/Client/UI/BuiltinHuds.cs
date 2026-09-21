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

    internal static void Register(IWritableRegistry<HudDefinition> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "crosshair"), Crosshair);
    }
}
