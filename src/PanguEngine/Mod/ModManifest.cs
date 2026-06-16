using System.Text.Json.Serialization;

namespace PanguEngine.Mod;

internal sealed record ModManifest
{
    [JsonPropertyName("id")] public string? Id { get; init; }

    [JsonPropertyName("version")] public string? Version { get; init; }

    [JsonPropertyName("assembly")] public string? Assembly { get; init; }

    [JsonPropertyName("entry")] public string? Entry { get; init; }

    [JsonPropertyName("dependencies")] public IReadOnlyList<ModDependencyManifest>? Dependencies { get; init; }
}

internal sealed record ModDependencyManifest
{
    [JsonPropertyName("id")] public string? Id { get; init; }

    [JsonPropertyName("version")] public string? Version { get; init; }

    [JsonPropertyName("optional")] public bool Optional { get; init; }
}