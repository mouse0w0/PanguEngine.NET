using PanguEngine.Registries;

namespace PanguEngine.Audio;

/// <summary>
/// Provides the built-in sound categories.
/// </summary>
public static class BuiltinSoundCategories
{
    /// <summary>The built-in music category.</summary>
    public static SoundCategory Music { get; } = new(ignoresUiPause: true);

    /// <summary>The built-in environmental ambience category.</summary>
    public static SoundCategory Ambient { get; } = new();

    /// <summary>The built-in gameplay sound effects category.</summary>
    public static SoundCategory SoundEffects { get; } = new();

    /// <summary>The built-in user interface category.</summary>
    public static SoundCategory UserInterface { get; } = new(ignoresUiPause: true);

    /// <summary>The built-in spoken voice category.</summary>
    public static SoundCategory Voice { get; } = new();

    internal static void Register(IWritableRegistry<SoundCategory> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "music"), Music);
        registry.Register(ResourceKey.Create("pangu", "ambient"), Ambient);
        registry.Register(ResourceKey.Create("pangu", "sound_effects"), SoundEffects);
        registry.Register(ResourceKey.Create("pangu", "user_interface"), UserInterface);
        registry.Register(ResourceKey.Create("pangu", "voice"), Voice);
    }
}
