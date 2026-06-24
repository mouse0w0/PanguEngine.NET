namespace PanguEngine.Modding;

/// <summary>
/// Provides services for the client setup lifecycle stage.
/// </summary>
public sealed class ModClientSetupContext : ModLifecycleContext
{
    /// <summary>
    /// Initializes a client setup lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    /// <param name="taskQueue">The lifecycle task queue.</param>
    internal ModClientSetupContext(ModContainer mod, ModLifecycleTaskQueue taskQueue) : base(mod, taskQueue)
    {
    }
}