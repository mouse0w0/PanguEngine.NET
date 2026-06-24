using System.Collections.Concurrent;

namespace PanguEngine.Modding;

/// <summary>
/// Stores work scheduled during mod lifecycle stages.
/// </summary>
internal sealed class ModLifecycleTaskQueue
{
    private readonly ConcurrentQueue<QueuedLifecycleTask> _tasks = new();

    /// <summary>
    /// Adds work for a loaded mod.
    /// </summary>
    /// <param name="mod">The loaded mod container.</param>
    /// <param name="action">The work to add.</param>
    public void Enqueue(ModContainer mod, Action action)
    {
        _tasks.Enqueue(new QueuedLifecycleTask(mod, action));
    }

    /// <summary>
    /// Runs queued lifecycle work.
    /// </summary>
    /// <param name="addError">The handler that records queued work failures.</param>
    public void Drain(Action<ModContainer, Exception> addError)
    {
        ArgumentNullException.ThrowIfNull(addError);

        while (_tasks.TryDequeue(out var task))
        {
            try
            {
                task.Action();
            }
            catch (Exception ex)
            {
                addError(task.Mod, ex);
            }
        }
    }

    private sealed record QueuedLifecycleTask(ModContainer Mod, Action Action);
}