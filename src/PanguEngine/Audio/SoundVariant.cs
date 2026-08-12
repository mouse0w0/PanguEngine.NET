using PanguEngine.Registries;

namespace PanguEngine.Audio;

internal sealed record SoundVariant(
    ResourceKey Resource,
    int Weight,
    float MinVolume,
    float MaxVolume,
    float MinPitch,
    float MaxPitch);
