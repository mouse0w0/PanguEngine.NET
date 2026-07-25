using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockAppearanceRotation
{
    [JsonPropertyName("x")] public JsonElement X { get; init; }

    [JsonPropertyName("y")] public JsonElement Y { get; init; }

    [JsonPropertyName("z")] public JsonElement Z { get; init; }
}