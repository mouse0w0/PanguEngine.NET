namespace PanguEngine.Modding;

/// <summary>
/// Provides services for a mod lifecycle stage.
/// </summary>
public abstract class ModLifecycleContext
{
    /// <summary>
    /// Initializes a lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    internal ModLifecycleContext(ModContainer mod)
    {
        Mod = mod;
    }

    /// <summary>
    /// Gets the loaded mod container.
    /// </summary>
    public ModContainer Mod { get; }
}