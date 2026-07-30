namespace PanguEngine.Client.UI;

/// <summary>
/// Dispatches UI work to a single bound client thread.
/// </summary>
public sealed class UiDispatcher
{
    private readonly Lock _sync = new();
    private readonly Queue<Action> _pendingActions = [];
    private readonly int _ownerThreadId;
    private bool _isDraining;
    private bool _isShutdown;

    /// <summary>
    /// Initializes a dispatcher bound to the current managed thread.
    /// </summary>
    internal UiDispatcher()
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Gets whether the current thread can access this dispatcher.
    /// </summary>
    /// <returns>Whether the dispatcher is active and bound to the current thread.</returns>
    public bool CheckAccess()
    {
        lock (_sync)
        {
            return !_isShutdown &&
                   _ownerThreadId == Environment.CurrentManagedThreadId;
        }
    }

    /// <summary>
    /// Verifies that this dispatcher is active and bound to the current thread.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current thread does not own the dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher is shut down.</exception>
    public void VerifyAccess()
    {
        lock (_sync)
        {
            VerifyAccessCore();
        }
    }

    /// <summary>
    /// Enqueues an action for a future drain on the bound client thread.
    /// </summary>
    /// <param name="action">The action to enqueue.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher is shut down.</exception>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_sync)
        {
            ThrowIfShutdown();
            _pendingActions.Enqueue(action);
        }
    }

    /// <summary>
    /// Drains the actions that were pending when the current batch started.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current thread does not own the dispatcher or a drain is already active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher is shut down.</exception>
    internal void DrainPending()
    {
        int batchSize;
        lock (_sync)
        {
            if (_isDraining)
                throw new InvalidOperationException("The UI dispatcher is already draining pending actions.");

            VerifyAccessCore();
            _isDraining = true;
            batchSize = _pendingActions.Count;
        }

        try
        {
            for (var index = 0; index < batchSize; index++)
            {
                Action action;
                lock (_sync)
                {
                    if (_isShutdown)
                        break;

                    action = _pendingActions.Dequeue();
                }

                action();
            }
        }
        finally
        {
            lock (_sync)
            {
                _isDraining = false;
            }
        }
    }

    /// <summary>
    /// Shuts down the dispatcher and discards all pending actions.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the dispatcher has not shut down and the current thread does not own it.
    /// </exception>
    internal void Shutdown()
    {
        lock (_sync)
        {
            if (_isShutdown)
                return;
            if (_ownerThreadId != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("UI dispatcher access requires its bound thread.");

            _isShutdown = true;
            _pendingActions.Clear();
        }
    }

    private void VerifyAccessCore()
    {
        ThrowIfShutdown();
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI dispatcher access requires its bound thread.");
    }

    private void ThrowIfShutdown()
    {
        if (_isShutdown)
            throw new ObjectDisposedException(nameof(UiDispatcher));
    }
}