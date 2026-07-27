namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanResourceLifetime
{
    private readonly Action _destroy;
    private readonly Action<ulong, Action> _enqueue;
    private ulong _lastUseValue;
    private int _holdCount;
    private bool _destroyRequested;
    private bool _destroyEnqueued;

    internal VulkanResourceLifetime(
        GraphicsResource resource,
        Action destroy,
        Action<ulong, Action> enqueue)
    {
        Resource = resource;
        _destroy = destroy;
        _enqueue = enqueue;
    }

    internal GraphicsResource Resource { get; }

    internal bool TryAcquireHold()
    {
        if (_destroyRequested)
            return false;

        _holdCount++;
        return true;
    }

    internal void ReleaseHold()
    {
        ReleaseHoldCore(null);
    }

    internal void ReleaseHold(ulong submissionValue)
    {
        ReleaseHoldCore(submissionValue);
    }

    internal void RequestDestroy()
    {
        _destroyRequested = true;
        TryEnqueueDestroy();
    }

    private void ReleaseHoldCore(ulong? submissionValue)
    {
        if (_holdCount == 0)
            throw new InvalidOperationException("Vulkan resource lifetime hold count cannot be negative.");

        if (submissionValue.HasValue)
            _lastUseValue = Math.Max(_lastUseValue, submissionValue.Value);

        _holdCount--;
        TryEnqueueDestroy();
    }

    private void TryEnqueueDestroy()
    {
        if (_holdCount != 0 || !_destroyRequested || _destroyEnqueued)
            return;

        _destroyEnqueued = true;
        _enqueue(_lastUseValue, _destroy);
    }
}