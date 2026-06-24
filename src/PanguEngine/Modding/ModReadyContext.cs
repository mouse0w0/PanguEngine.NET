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
    /// <param name="taskQueue">The lifecycle task queue.</param>
    internal ModReadyContext(ModContainer mod, ModLifecycleTaskQueue taskQueue) : base(mod, taskQueue)
    {
    }
}