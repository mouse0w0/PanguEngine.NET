namespace PanguEngine.Threading;

/// <summary>
/// Adapts <see cref="SynchronizationContext"/> to an <see cref="EngineWorkQueue"/>.
/// </summary>
/// <remarks>
/// The context only routes continuations into the queue. Draining and closing the queue belong to the
/// engine loop that owns it.
/// </remarks>
internal sealed class EngineSynchronizationContext(EngineWorkQueue queue) : SynchronizationContext
{
    private readonly EngineWorkQueue _queue = queue;

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => this;

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _queue.PostContinuation(() => d(state));
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _queue.VerifyAccess();
        d(state);
    }
}
