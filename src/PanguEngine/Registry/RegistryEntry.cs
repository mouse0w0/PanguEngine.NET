namespace PanguEngine.Registry;

/// <summary>
/// Represents a value registered in a registry.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
/// <param name="Id">The registry-local numeric identifier.</param>
/// <param name="Key">The stable resource key.</param>
/// <param name="Value">The registered value.</param>
public record RegistryEntry<T>(int Id, ResourceKey Key, T Value) where T : class;