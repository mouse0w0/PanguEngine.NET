using PanguEngine.Registries;

namespace PanguEngine.Client.Resources.Models;

internal abstract record BlockModelValue
{
    internal sealed record Variable(string Name) : BlockModelValue;

    internal sealed record Resource(ResourceKey Key) : BlockModelValue;
}

internal sealed record UnresolvedBlockAppearanceEntry(
    BlockModelValue Model,
    int Weight,
    BlockModelRotation Rotation);

internal sealed record UnresolvedBlockAppearance(
    ResourceKey SourceKey,
    string? ParentReference,
    IReadOnlyDictionary<string, BlockModelValue> Models,
    IReadOnlyDictionary<string, IReadOnlyList<UnresolvedBlockAppearanceEntry>>? Variants);