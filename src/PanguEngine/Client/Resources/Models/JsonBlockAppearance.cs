using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockAppearance
{
    [JsonPropertyName("parent")] public string? Parent { get; init; }

    [JsonPropertyName("models")] public Dictionary<string, string?>? Models { get; init; }

    [JsonPropertyName("variants")] public JsonElement Variants { get; init; }
}

internal sealed class JsonBlockAppearanceEntry
{
    [JsonPropertyName("model")] public string? Model { get; init; }

    [JsonPropertyName("weight")] public int Weight { get; init; } = 1;

    [JsonPropertyName("rotation")] public JsonBlockAppearanceRotation Rotation { get; init; }
}

internal struct JsonBlockAppearanceRotation
{
    [JsonPropertyName("x")] public int X { get; init; }

    [JsonPropertyName("y")] public int Y { get; init; }

    [JsonPropertyName("z")] public int Z { get; init; }
}