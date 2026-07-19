using System.Text.Json.Serialization;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockModelElement
{
    [JsonPropertyName("from")] public float[]? From { get; init; }

    [JsonPropertyName("to")] public float[]? To { get; init; }

    [JsonPropertyName("faces")] public Dictionary<string, JsonBlockModelFace?>? Faces { get; init; }
}