using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnbakedBlockAppearance(
    ResourceKey SourceKey,
    IReadOnlyDictionary<BlockState, IReadOnlyList<UnbakedBlockAppearanceEntry>> Variants);

internal sealed record UnbakedBlockAppearanceEntry(
    ResourceKey ModelKey,
    int Weight,
    BlockModelRotation Rotation);