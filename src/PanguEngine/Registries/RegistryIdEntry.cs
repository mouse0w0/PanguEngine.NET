namespace PanguEngine.Registries;

/// <summary>
/// Associates a session identifier and resource key with a local definition.
/// </summary>
/// <typeparam name="T">The definition type.</typeparam>
/// <param name="Id">The session-local numeric identifier.</param>
/// <param name="Key">The resource key.</param>
/// <param name="Value">The local definition instance.</param>
public readonly record struct RegistryIdEntry<T>(int Id, ResourceKey Key, T Value) where T : class;
