namespace PanguEngine.Threading;

/// <summary>
/// Owns the FIFO work queue for an engine loop thread and tracks explicitly scheduled operations.
/// </summary>
/// <remarks>
/// The queue holds two sets with different lifetimes. <see cref="PostContinuation"/> and the work item
/// of <see cref="TryPostOperation"/> both enqueue to the same FIFO and may be dropped on
/// <see cref="Destroy"/>. Operations additionally stay registered until their callback is removed by
/// <see cref="CompleteOperation"/>, so an operation that has already started and is awaiting an inner
/// task can still be aborted by <see cref="Destroy"/>.
/// </remarks>
internal sealed class EngineWorkQueue
{
    private readonly Lock _sync = new();
    private readonly Queue<Action> _pending = [];
    private readonly List<Action> _abortCallbacks = [];
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private bool _isDraining;
    private bool _destroyed;

    /// <summary>Gets the number of queued work items that have not started yet.</summary>
    internal int PendingCount
    {
        get
        {
            lock (_sync)
                return _pending.Count;
        }
    }

    /// <summary>Gets whether the calling thread owns this queue.</summary>
    internal bool IsOwnerThread => _ownerThreadId == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Registers an operation and queues its work atomically. The operation stays tracked until
    /// <paramref name="abortCallback"/> is removed by <see cref="CompleteOperation"/>.
    /// </summary>
    /// <param name="work">The work to run on the queue owner thread.</param>
    /// <param name="abortCallback">
    /// Called once by <see cref="Destroy"/> when the operation has not reached a terminal state. It must
    /// not throw, because <see cref="Destroy"/> relies on every callback running.
    /// </param>
    internal bool TryPostOperation(Action work, Action abortCallback)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(abortCallback);
        lock (_sync)
        {
            if (_destroyed)
                return false;

            _abortCallbacks.Add(abortCallback);
            _pending.Enqueue(work);
            return true;
        }
    }

    /// <summary>
    /// Removes a registered abort callback after its operation reached a terminal state. Removal uses
    /// reference identity, so the same delegate instance may be registered for several operations.
    /// </summary>
    internal void CompleteOperation(Action abortCallback)
    {
        ArgumentNullException.ThrowIfNull(abortCallback);
        lock (_sync)
        {
            for (var index = 0; index < _abortCallbacks.Count; index++)
            {
                if (!ReferenceEquals(_abortCallbacks[index], abortCallback))
                    continue;

                _abortCallbacks.RemoveAt(index);
                return;
            }
        }
    }

    /// <summary>Queues a continuation for the next drain, or drops it when the queue is destroyed.</summary>
    internal void PostContinuation(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        lock (_sync)
        {
            if (_destroyed)
                return;

            _pending.Enqueue(continuation);
        }
    }

    /// <summary>Runs the current fixed batch on the owning thread.</summary>
    internal void RunPending()
    {
        int batchSize;
        lock (_sync)
        {
            if (_isDraining)
                throw new InvalidOperationException("The engine work queue is already draining.");

            VerifyAccessLocked();
            _isDraining = true;
            batchSize = _pending.Count;
        }

        try
        {
            for (var index = 0; index < batchSize; index++)
            {
                Action continuation;
                lock (_sync)
                {
                    if (_pending.Count == 0)
                        break;
                    continuation = _pending.Dequeue();
                }

                continuation();
            }
        }
        finally
        {
            lock (_sync)
                _isDraining = false;
        }
    }

    /// <summary>
    /// Drops queued continuations, aborts every tracked operation and permanently closes the queue.
    /// </summary>
    internal void Destroy()
    {
        Action[] abortCallbacks;
        lock (_sync)
        {
            if (_destroyed)
                return;

            VerifyAccessLocked();
            _destroyed = true;
            _pending.Clear();
            abortCallbacks = [.. _abortCallbacks];
            _abortCallbacks.Clear();
        }

        foreach (var abortCallback in abortCallbacks)
            abortCallback();
    }

    /// <summary>Verifies that the calling thread may run or close the queue.</summary>
    internal void VerifyAccess()
    {
        lock (_sync)
            VerifyAccessLocked();
    }

    private void VerifyAccessLocked()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Engine work queue access requires its owner thread.");
    }
}
