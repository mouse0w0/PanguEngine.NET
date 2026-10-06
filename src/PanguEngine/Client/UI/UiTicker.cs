namespace PanguEngine.Client.UI;

/// <summary>
/// Runs a callback tied to a UI node once per prepared frame while active.
/// </summary>
/// <remarks>
/// The owning screen supplies the same absolute monotonic time to all tickers in a client frame.
/// Changing the node's screen ownership or closing its screen stops the current run without invoking business callbacks.
/// Remounting or reopening does not resume a stopped run. Callback exceptions propagate immediately.
/// Consumers retain tickers for reuse. Nodes and screens register active runs only.
/// </remarks>
public sealed class UiTicker
{
    private readonly UiNode _owner;
    private readonly Action<TimeSpan> _onTick;
    private UiScreen? _screen;
    private LinkedListNode<UiTicker>? _registration;

    internal UiTicker(UiNode owner, Action<TimeSpan> onTick)
    {
        _owner = owner;
        _onTick = onTick;
    }

    /// <summary>
    /// Gets whether this ticker has an active run.
    /// </summary>
    public bool IsActive => _registration is not null;

    /// <summary>
    /// Starts a new run, or leaves an already active run unchanged.
    /// </summary>
    /// <remarks>
    /// Requires screen ownership, the owning screen's UI thread, and an open screen that has not begun closing.
    /// Screen ownership is determined by the node's Screen property.
    /// Starting after the owning screen begins frame preparation takes effect no earlier than its next prepared frame.
    /// No callback runs synchronously; consumers reset their time baseline for each new run.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node has no screen ownership, its screen is closed or closing, or access is on the wrong thread.
    /// </exception>
    public void Start()
    {
        var screen = _owner.Screen
            ?? throw new InvalidOperationException("The UI node has no screen ownership.");
        screen.VerifyTickerStart();
        if (_registration is not null)
            return;

        _screen = screen;
        _registration = screen.RegisterTicker(this);
        _owner.RegisterTicker(this);
    }

    /// <summary>
    /// Stops the current run, or does nothing when already stopped.
    /// </summary>
    /// <remarks>
    /// Stopping an active run requires its owning UI thread. An executing callback continues normally.
    /// Stopping does not invoke the callback or imply a business cancellation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when an active run is stopped from the wrong thread.</exception>
    public void Stop()
    {
        if (_registration is null)
            return;

        _screen!.VerifyOwnerThread();
        StopCore();
    }

    internal void StopCore()
    {
        if (_registration is null)
            return;

        _screen!.UnregisterTicker(_registration);
        _owner.UnregisterTicker(this);
        _registration = null;
        _screen = null;
    }

    internal void Tick(LinkedListNode<UiTicker> registration, TimeSpan frameTime)
    {
        if (ReferenceEquals(_registration, registration))
            _onTick(frameTime);
    }
}
