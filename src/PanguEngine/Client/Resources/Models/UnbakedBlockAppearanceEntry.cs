using PanguEngine.Registries;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnbakedBlockAppearanceEntry(
    ResourceKey ModelKey,
    int Weight,
    BlockModelRotation Rotation);