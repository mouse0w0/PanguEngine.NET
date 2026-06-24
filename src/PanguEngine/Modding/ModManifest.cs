using System.Text.Json.Serialization;

namespace PanguEngine.Modding;

/// <summary>
/// Represents the serialized metadata for a mod package.
/// </summary>
internal sealed record ModManifest
{
    /// <summary>
    /// Gets the mod identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the mod version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the mod assembly file name.
    /// </summary>
    [JsonPropertyName("assembly")]
    public string? Assembly { get; init; }

    /// <summary>
    /// Gets the mod entry point type name.
    /// </summary>
    [JsonPropertyName("entry")]
    public string? Entry { get; init; }

    /// <summary>
    /// Gets the dependencies declared by the mod.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IReadOnlyList<ModDependencyManifest>? Dependencies { get; init; }
}

/// <summary>
/// Represents a dependency entry in a mod manifest.
/// </summary>
internal sealed record ModDependencyManifest
{
    /// <summary>
    /// Gets the dependency mod identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the accepted dependency version range.
    /// </summary>
    [JsonPropertyName("version_range")]
    public string? VersionRange { get; init; }

    /// <summary>
    /// Gets a value indicating whether the dependency is optional.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}