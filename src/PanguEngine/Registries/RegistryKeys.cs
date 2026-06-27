namespace PanguEngine.Registries;

/// <summary>
/// Provides resource keys for built-in registries.
/// </summary>
public static class RegistryKeys
{
    /// <summary>The key of the registry catalog.</summary>
    public static ResourceKey Registries { get; } = ResourceKey.Create("pangu", "registries");
}