namespace PanguEngine.Registries;

/// <summary>
/// Provides metadata and operations for a current session identifier table.
/// </summary>
public interface IRegistryIdTable
{
    /// <summary>The key of the source registry.</summary>
    ResourceKey Key { get; }

    /// <summary>The local definition type.</summary>
    Type ValueType { get; }

    /// <summary>The number of bound definitions.</summary>
    int Count { get; }

    /// <summary>
    /// Allocates consecutive identifiers in the frozen source registry's registration order.
    /// </summary>
    void Allocate();

    /// <summary>
    /// Binds an authoritative mapping to definitions in the frozen local registry.
    /// </summary>
    /// <param name="mapping">The identifier mapping.</param>
    void Bind(RegistryIdMapping mapping);

    /// <summary>
    /// Removes all current identifier mappings.
    /// </summary>
    void Clear();

    /// <summary>
    /// Exports the identifier mapping without local definition instances.
    /// </summary>
    /// <returns>The mapping snapshot.</returns>
    RegistryIdMapping ExportMapping();
}
