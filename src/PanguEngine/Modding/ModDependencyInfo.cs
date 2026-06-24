using PanguEngine.Versioning;

namespace PanguEngine.Modding;

/// <summary>
/// Describes a dependency declared by a loaded mod.
/// </summary>
/// <param name="Id">The dependency mod identifier.</param>
/// <param name="VersionRange">The accepted dependency version range.</param>
/// <param name="Optional">Whether the dependency is optional.</param>
public sealed record ModDependencyInfo(string Id, SemVersionRange? VersionRange, bool Optional);