namespace PanguEngine.UI;

/// <summary>
/// Runs a callback once per UI frame while started.
/// </summary>
/// <remarks>
/// The toolkit supplies the same absolute monotonic time to all tickers in a UI frame.
/// Consumers manage their time baselines and decide when to start, stop, or resume their tasks.
/// Scene and element lifecycle changes do not stop a ticker. Active registrations retain their callbacks.
/// Callback exceptions propagate immediately. Tickers must not be reused after toolkit shutdown.
/// </remarks>
public sealed class Ticker
{
    private readonly Action<TimeSpan> _onTick;
    private LinkedListNode<Ticker>? _registration;

    internal Ticker(Action<TimeSpan> onTick) => _onTick = onTick;

    /// <summary>Gets whether this ticker is registered for execution.</summary>
    public bool IsActive => _registration is not null;

    /// <summary>
    /// Starts a run, or leaves an already started run unchanged.
    /// </summary>
    /// <remarks>
    /// Requires the initialized host UI thread. Registrations created after the frame snapshot
    /// wait until the next UI frame. No callback runs synchronously, and starting does not rebase frame time.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the toolkit is not initialized, or in Debug builds for access outside its UI thread.
    /// </exception>
    public void Start()
    {
        UIToolkit.VerifyInitialized();
        UIToolkit.VerifyAccess();
        _registration ??= UIToolkit.RegisterTicker(this);
    }

    /// <summary>
    /// Stops the current run, or does nothing when already stopped.
    /// </summary>
    /// <remarks>
    /// Stopping an active run requires the host UI thread. An executing callback continues normally.
    /// Stopping does not invoke the callback or imply a business cancellation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown in Debug builds when an active run is stopped from outside the host UI thread.</exception>
    public void Stop()
    {
        if (!IsActive)
            return;

        UIToolkit.VerifyAccess();
        StopCore();
    }

    internal void StopCore()
    {
        if (_registration is null)
            return;

        UIToolkit.UnregisterTicker(_registration);
        _registration = null;
    }

    internal void Tick(LinkedListNode<Ticker> registration, TimeSpan frameTime)
    {
        if (!ReferenceEquals(_registration, registration))
            return;

        _onTick(frameTime);
    }
}
