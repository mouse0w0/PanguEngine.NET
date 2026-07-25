using PanguEngine.Registries;

namespace PanguEngine.Client.Resources.Models;

internal sealed record ResolvedBlockCandidate(
    ResourceKey ModelKey,
    BlockModelRotation Rotation,
    int Weight,
    UnbakedBlockModel Model);