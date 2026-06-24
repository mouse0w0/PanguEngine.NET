namespace PanguEngine.Modding;

/// <summary>
/// Provides services for the common setup lifecycle stage.
/// </summary>
public sealed class ModCommonSetupContext : ModLifecycleContext
{
    /// <summary>
    /// Initializes a common setup lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    /// <param name="taskQueue">The lifecycle task queue.</param>
    internal ModCommonSetupContext(ModContainer mod, ModLifecycleTaskQueue taskQueue) : base(mod, taskQueue)
    {
    }
}