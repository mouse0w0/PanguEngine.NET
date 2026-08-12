using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanguEngine.Audio;

internal sealed class JsonSoundEvent
{
    [JsonPropertyName("variants")] public JsonElement Variants { get; init; }
}

internal sealed class JsonSoundVariant
{
    [JsonPropertyName("resource")] public string? Resource { get; init; }

    [JsonPropertyName("weight")] public int Weight { get; init; } = 1;

    [JsonPropertyName("volume")] public JsonElement Volume { get; init; }

    [JsonPropertyName("pitch")] public JsonElement Pitch { get; init; }
}
