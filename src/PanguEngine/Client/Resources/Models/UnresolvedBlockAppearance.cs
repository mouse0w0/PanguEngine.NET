using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnresolvedBlockAppearance(
    ResourceKey SourceKey,
    IReadOnlyDictionary<BlockState, IReadOnlyList<UnresolvedBlockAppearanceEntry>> Variants);

internal sealed record UnresolvedBlockAppearanceEntry(
    ResourceKey ModelKey,
    int Weight,
    BlockModelRotation Rotation);