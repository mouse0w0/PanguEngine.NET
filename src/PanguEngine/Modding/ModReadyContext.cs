namespace PanguEngine.Modding;

/// <summary>
/// Provides services for the ready lifecycle stage.
/// </summary>
public sealed class ModReadyContext : ModLifecycleContext
{
    /// <summary>
    /// Initializes a ready lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    internal ModReadyContext(ModContainer mod) : base(mod)
    {
    }
}