using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockAppearance
{
    [JsonPropertyName("variants")] public JsonElement Variants { get; init; }
}