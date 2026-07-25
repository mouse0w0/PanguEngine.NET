using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockAppearanceCandidate
{
    [JsonPropertyName("model")] public JsonElement Model { get; init; }

    [JsonPropertyName("weight")] public JsonElement Weight { get; init; }

    [JsonPropertyName("rotation")] public JsonElement Rotation { get; init; }
}