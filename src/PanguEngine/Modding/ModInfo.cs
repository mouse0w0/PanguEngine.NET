using PanguEngine.Versioning;

namespace PanguEngine.Modding;

/// <summary>
/// Describes a loaded mod.
/// </summary>
/// <param name="Id">The mod identifier.</param>
/// <param name="Version">The mod semantic version.</param>
public sealed record ModInfo(string Id, SemVersion Version);