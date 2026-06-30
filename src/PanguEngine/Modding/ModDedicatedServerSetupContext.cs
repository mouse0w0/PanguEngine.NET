namespace PanguEngine.Modding;

/// <summary>
/// Provides services for the dedicated server setup lifecycle stage.
/// </summary>
public sealed class ModDedicatedServerSetupContext : ModLifecycleContext
{
    /// <summary>
    /// Initializes a dedicated server setup lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    internal ModDedicatedServerSetupContext(ModContainer mod) : base(mod)
    {
    }
}