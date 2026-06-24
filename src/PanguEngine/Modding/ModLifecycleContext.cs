namespace PanguEngine.Modding;

/// <summary>
/// Provides services for a mod lifecycle stage.
/// </summary>
public abstract class ModLifecycleContext
{
    private readonly ModLifecycleTaskQueue _taskQueue;

    /// <summary>
    /// Initializes a lifecycle context.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    /// <param name="taskQueue">The lifecycle task queue.</param>
    internal ModLifecycleContext(ModContainer mod, ModLifecycleTaskQueue taskQueue)
    {
        Mod = mod;
        _taskQueue = taskQueue;
    }

    /// <summary>
    /// Gets the loaded mod container.
    /// </summary>
    public ModContainer Mod { get; }

    /// <summary>
    /// Enqueues work to run serially for the current lifecycle stage.
    /// </summary>
    /// <param name="action">The work to enqueue.</param>
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _taskQueue.Enqueue(Mod, action);
    }
}