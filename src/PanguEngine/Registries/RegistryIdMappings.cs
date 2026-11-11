namespace PanguEngine.Registries;

/// <summary>
/// Stores a session's registry identifier mappings.
/// </summary>
public sealed class RegistryIdMappings
{
    /// <summary>
    /// Creates a container for the supplied registry mappings.
    /// </summary>
    /// <param name="tables">The registry mappings, which must remain unmodified.</param>
    public RegistryIdMappings(IReadOnlyList<RegistryIdMapping> tables)
    {
        Tables = tables;
    }

    /// <summary>The registry mappings.</summary>
    public IReadOnlyList<RegistryIdMapping> Tables { get; }
}
