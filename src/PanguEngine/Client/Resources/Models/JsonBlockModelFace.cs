using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockModelFace
{
    [JsonPropertyName("texture")] public string? Texture { get; init; }

    [JsonPropertyName("uv")] public float[]? Uv { get; init; }

    [JsonPropertyName("rotation")] public JsonElement Rotation { get; init; }

    [JsonPropertyName("cull")] public string[]? Cull { get; init; }
}