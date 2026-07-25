using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnresolvedBlockModel(
    ResourceKey SourceKey,
    string? ParentReference,
    IReadOnlyDictionary<string, BlockTextureValue> Textures,
    IReadOnlyList<UnresolvedBlockElement>? Elements);

internal sealed record UnresolvedBlockElement(
    Vector3D<float> From,
    Vector3D<float> To,
    IReadOnlyDictionary<string, UnresolvedBlockFace> Faces);

internal sealed record UnresolvedBlockFace(
    BlockTextureValue Texture,
    BlockFaceUv? Uv,
    int Rotation,
    IReadOnlyList<string> Cull);

internal abstract record BlockTextureValue
{
    internal sealed record Variable(string Name) : BlockTextureValue;

    internal sealed record Resource(ResourceKey Key) : BlockTextureValue;
}