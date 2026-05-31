namespace PanguEngine.Registry;

/// <summary>
/// Identifies an entry inside a registry.
/// </summary>
public readonly record struct ResourceAddress
{
    /// <summary>The key of the registry that contains the entry.</summary>
    public ResourceKey RegistryKey { get; }

    /// <summary>The key of the entry inside the registry.</summary>
    public ResourceKey EntryKey { get; }

    /// <summary>
    /// Creates an address for a registry entry.
    /// </summary>
    /// <param name="registryKey">The key of the registry that contains the entry.</param>
    /// <param name="entryKey">The key of the entry inside the registry.</param>
    public ResourceAddress(ResourceKey registryKey, ResourceKey entryKey)
    {
        if (!ResourceKey.IsValid(registryKey))
            throw new ArgumentException("Invalid registry key.", nameof(registryKey));
        if (!ResourceKey.IsValid(entryKey))
            throw new ArgumentException("Invalid entry key.", nameof(entryKey));

        RegistryKey = registryKey;
        EntryKey = entryKey;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{RegistryKey}/{EntryKey}";
}