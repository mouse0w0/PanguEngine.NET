namespace PanguEngine.Registries;

/// <summary>
/// Provides resource keys for built-in registries.
/// </summary>
public static class RegistryKeys
{
    /// <summary>The key of the registry catalog.</summary>
    public static ResourceKey Registry { get; } = ResourceKey.Create("pangu", "registry");

    /// <summary>The key of the block registry.</summary>
    public static ResourceKey Block { get; } = ResourceKey.Create("pangu", "block");

    /// <summary>The key of the sound category registry.</summary>
    public static ResourceKey SoundCategory { get; } = ResourceKey.Create("pangu", "sound_category");

    /// <summary>The key of the sound event registry.</summary>
    public static ResourceKey SoundEvent { get; } = ResourceKey.Create("pangu", "sound_event");

    /// <summary>The key of the HUD component registry.</summary>
    public static ResourceKey Hud { get; } = ResourceKey.Create("pangu", "hud");

    /// <summary>The key of the input action registry.</summary>
    public static ResourceKey InputAction { get; } = ResourceKey.Create("pangu", "input_action");

    /// <summary>The key of the input context registry.</summary>
    public static ResourceKey InputContext { get; } = ResourceKey.Create("pangu", "input_context");
}
