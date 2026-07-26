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
    [JsonPropertyName("model")] public JsonElement Model { get; init; }

    [JsonPropertyName("weight")] public JsonElement Weight { get; init; }

    [JsonPropertyName("rotation")] public JsonElement Rotation { get; init; }
}

internal sealed class JsonBlockAppearanceRotation
{
    [JsonPropertyName("x")] public JsonElement X { get; init; }

    [JsonPropertyName("y")] public JsonElement Y { get; init; }

    [JsonPropertyName("z")] public JsonElement Z { get; init; }
}