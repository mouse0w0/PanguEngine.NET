using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnbakedBlockAppearance(
    ResourceKey SourceKey,
    IReadOnlyDictionary<BlockState, IReadOnlyList<UnbakedBlockAppearanceEntry>> Variants);