using PanguEngine.Registries;

namespace PanguEngine.Audio;

internal static class BuiltinSoundEvents
{
    internal static SoundEvent BlockBreak { get; } = new(BuiltinSoundCategories.SoundEffects);

    internal static SoundEvent BlockPlace { get; } = new(BuiltinSoundCategories.SoundEffects);

    internal static void Register(IWritableRegistry<SoundEvent> registry)
    {
        registry.Register(ResourceKey.Parse("pangu:block_break"), BlockBreak);
        registry.Register(ResourceKey.Parse("pangu:block_place"), BlockPlace);
    }
}
