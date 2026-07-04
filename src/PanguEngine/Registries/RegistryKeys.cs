namespace PanguEngine.Registries;

/// <summary>
/// Provides resource keys for built-in registries.
/// </summary>
public static class RegistryKeys
{
    /// <summary>The key of the registry catalog.</summary>
    public static ResourceKey Registry { get; } = ResourceKey.Create("pangu", "registry");

    /// <summary>The key of the block registry.</summary>
    public static ResourceKey Block { get; } = ResourceKey.Create("pangu", "block");
}