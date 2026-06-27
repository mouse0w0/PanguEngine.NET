namespace PanguEngine.Registries;

/// <summary>
/// Provides internal resolution for registry-backed holders.
/// </summary>
internal interface IHolder
{
    /// <summary>
    /// Resolves this holder using the specified registry manager.
    /// </summary>
    /// <param name="manager">The registry manager that owns the registered entries.</param>
    void Resolve(RegistryManager manager);
}