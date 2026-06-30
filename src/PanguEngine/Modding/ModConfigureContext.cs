namespace PanguEngine.Modding;

/// <summary>
/// Provides services for the configure lifecycle stage.
/// </summary>
public sealed class ModConfigureContext : ModLifecycleContext
{
    /// <summary>
    /// Initializes a configure lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    internal ModConfigureContext(ModContainer mod) : base(mod)
    {
    }
}