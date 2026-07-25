using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockModel
{
    [JsonPropertyName("parent")] public string? Parent { get; init; }

    [JsonPropertyName("textures")] public Dictionary<string, string?>? Textures { get; init; }

    [JsonPropertyName("elements")] public JsonBlockModelElement?[]? Elements { get; init; }
}

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
internal sealed class JsonBlockModelElement
{
    [JsonPropertyName("from")] public float[]? From { get; init; }

    [JsonPropertyName("to")] public float[]? To { get; init; }

    [JsonPropertyName("faces")] public Dictionary<string, JsonBlockModelFace?>? Faces { get; init; }
}

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
internal sealed class JsonBlockModelFace
{
    [JsonPropertyName("texture")] public string? Texture { get; init; }

    [JsonPropertyName("uv")] public float[]? Uv { get; init; }

    [JsonPropertyName("rotation")] public JsonElement Rotation { get; init; }

    [JsonPropertyName("cull")] public string[]? Cull { get; init; }
}