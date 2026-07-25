using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Client.Resources.Models;

internal sealed record ResolvedBlockAppearance(
    ResourceKey BlockKey,
    ResourceKey SourceKey,
    IReadOnlyDictionary<BlockState, IReadOnlyList<ResolvedBlockCandidate>> Variants);