using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed record UnbakedBlockModel(
    ResourceKey SourceKey,
    string? ParentReference,
    IReadOnlyDictionary<string, BlockTextureValue> Textures,
    IReadOnlyList<UnbakedElement>? Elements);

internal sealed record UnbakedElement(
    Vector3D<float> From,
    Vector3D<float> To,
    IReadOnlyDictionary<string, UnbakedFace> Faces);

internal sealed record UnbakedFace(
    BlockTextureValue Texture,
    float[]? Uv,
    IReadOnlyList<string> Cull);