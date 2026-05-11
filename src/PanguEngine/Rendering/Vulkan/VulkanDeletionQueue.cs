namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// Queues deferred deletion of Vulkan resources.
/// </summary>
public static class VulkanDeletionQueue
{
    private static readonly Lock Lock = new();
    private static readonly PriorityQueue<Action, ulong> Pending = new();
    private static bool _drained;

    /// <summary>
    /// Enqueues a deferred deletion action.
    /// </summary>
    /// <param name="retireValue">The timeline value at which the action is safe to execute.</param>
    /// <param name="destroy">The deletion action.</param>
    public static void Enqueue(ulong retireValue, Action destroy)
    {
        lock (Lock)
        {
            if (_drained)
            {
                destroy();
                return;
            }

            Pending.Enqueue(destroy, retireValue);
        }
    }

    /// <summary>
    /// Executes all deferred deletion actions that are safe to release.
    /// </summary>
    public static void Collect()
    {
        VulkanContext.Vk.GetSemaphoreCounterValue(
            VulkanContext.Device, VulkanContext.GlobalTimelineSemaphore, out var gpuTimeline);

        var expired = new List<Action>();
        lock (Lock)
        {
            while (Pending.TryPeek(out _, out var retireValue) && retireValue <= gpuTimeline)
            {
                expired.Add(Pending.Dequeue());
            }
        }

        foreach (var action in expired)
        {
            action();
        }
    }

    /// <summary>
    /// Executes all pending destruction actions unconditionally.
    /// </summary>
    public static void Drain()
    {
        var remaining = new List<Action>();
        lock (Lock)
        {
            _drained = true;
            while (Pending.Count > 0)
            {
                remaining.Add(Pending.Dequeue());
            }
        }

        foreach (var action in remaining)
        {
            action();
        }
    }
}
