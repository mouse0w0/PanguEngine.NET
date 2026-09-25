using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Queues deferred deletion of Vulkan resources.
/// </summary>
internal static class VulkanDeletionQueue
{
    private static readonly PriorityQueue<Action, ulong> Pending = new();
    private static bool _drained;

    /// <summary>
    /// Enqueues a deferred deletion action.
    /// </summary>
    /// <param name="retireValue">The timeline value at which the action is safe to execute.</param>
    /// <param name="destroy">The deletion action.</param>
    internal static void Enqueue(ulong retireValue, Action destroy)
    {
        VulkanContext.EnsureRenderThread();
        if (_drained)
            throw new InvalidOperationException("The Vulkan deletion queue has already been drained.");

        Pending.Enqueue(destroy, retireValue);
    }

    /// <summary>
    /// Executes all deferred deletion actions that are safe to release.
    /// </summary>
    internal static void Collect()
    {
        VulkanContext.EnsureRenderThread();
        if (Pending.Count == 0)
            return;

        var result = VulkanContext.Vk.GetSemaphoreCounterValue(
            VulkanContext.Device, VulkanContext.GlobalTimelineSemaphore, out var gpuTimeline);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to query the global Vulkan timeline: {result}.");

        List<Action>? expired = null;
        while (Pending.TryPeek(out _, out var retireValue) && retireValue <= gpuTimeline)
        {
            expired ??= [];
            expired.Add(Pending.Dequeue());
        }

        if (expired == null)
            return;

        foreach (var action in expired)
            action();
    }

    /// <summary>
    /// Executes all pending destruction actions unconditionally.
    /// </summary>
    internal static void Drain()
    {
        VulkanContext.EnsureRenderThread();
        while (true)
        {
            var remaining = new List<Action>();
            if (_drained)
                return;

            if (Pending.Count == 0)
            {
                _drained = true;
                break;
            }

            while (Pending.Count > 0)
                remaining.Add(Pending.Dequeue());

            foreach (var action in remaining)
                action();
        }
    }
}
