namespace PanguEngine.Registries;

/// <summary>
/// Stores one registry's identifier mapping.
/// </summary>
public sealed class RegistryIdMapping
{
    /// <summary>
    /// Creates a mapping for a registry.
    /// </summary>
    /// <param name="registryKey">The registry key.</param>
    /// <param name="keys">The resource keys ordered by identifier, which must remain unmodified.</param>
    public RegistryIdMapping(ResourceKey registryKey, IReadOnlyList<ResourceKey> keys)
    {
        RegistryKey = registryKey;
        Keys = keys;
    }

    /// <summary>The registry described by this mapping.</summary>
    public ResourceKey RegistryKey { get; }

    /// <summary>The resource keys whose positions are their identifiers.</summary>
    public IReadOnlyList<ResourceKey> Keys { get; }
}
