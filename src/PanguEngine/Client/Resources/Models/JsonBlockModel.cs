using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockModel
{
    [JsonPropertyName("parent")] public string? Parent { get; init; }

    [JsonPropertyName("textures")] public Dictionary<string, string?>? Textures { get; init; }

    [JsonPropertyName("elements")] public JsonBlockModelElement?[]? Elements { get; init; }
}