using PanguEngine.Versioning;

namespace PanguEngine.Modding;

/// <summary>
/// Describes a loaded mod.
/// </summary>
public sealed record ModInfo
{
    /// <summary>
    /// Initializes the runtime metadata for a mod.
    /// </summary>
    /// <param name="id">The mod identifier.</param>
    /// <param name="version">The mod semantic version.</param>
    /// <param name="assembly">The mod assembly file name.</param>
    /// <param name="entry">The mod entry point type name.</param>
    /// <param name="dependencies">The dependencies declared by the mod.</param>
    internal ModInfo(string id, SemVersion version, string assembly, string entry,
        IReadOnlyList<ModDependencyInfo> dependencies)
    {
        Id = id;
        Version = version;
        Assembly = assembly;
        Entry = entry;
        Dependencies = dependencies;
    }

    /// <summary>
    /// Gets the mod identifier.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the mod semantic version.
    /// </summary>
    public SemVersion Version { get; init; }

    /// <summary>
    /// Gets the mod assembly file name.
    /// </summary>
    public string Assembly { get; init; }

    /// <summary>
    /// Gets the mod entry point type name.
    /// </summary>
    public string Entry { get; init; }

    /// <summary>
    /// Gets the dependencies declared by the mod.
    /// </summary>
    public IReadOnlyList<ModDependencyInfo> Dependencies { get; init; }
}