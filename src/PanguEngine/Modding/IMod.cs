namespace PanguEngine.Modding;

/// <summary>
/// Defines a mod entry point.
/// </summary>
public interface IMod
{
    /// <summary>
    /// Configures the mod after it is loaded.
    /// </summary>
    /// <param name="container">The loaded mod container.</param>
    void Configure(ModContainer container);
}