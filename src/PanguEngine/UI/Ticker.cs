namespace PanguEngine.UI;

/// <summary>
/// Runs a callback tied to a UI element once per prepared frame while active.
/// </summary>
/// <remarks>
/// The owning scene supplies the same absolute monotonic time to all tickers in a client frame.
/// Changing the element's scene ownership or closing its scene stops the current run without invoking business callbacks.
/// Remounting or reopening does not resume a stopped run. Callback exceptions propagate immediately.
/// Consumers retain tickers for reuse. Elements and scenes register active runs only.
/// </remarks>
public sealed class Ticker
{
    private readonly UIElement _owner;
    private readonly Action<TimeSpan> _onTick;
    private UIScene? _scene;
    private LinkedListNode<Ticker>? _registration;

    internal Ticker(UIElement owner, Action<TimeSpan> onTick)
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
    /// Requires scene ownership, the owning scene's UI thread, and an open scene that has not begun closing.
    /// Scene ownership is determined by the element's Scene property.
    /// Starting after the owning scene begins frame preparation takes effect no earlier than its next prepared frame.
    /// No callback runs synchronously; consumers reset their time baseline for each new run.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the element has no scene ownership, its scene is closed or closing, or access is on the wrong thread.
    /// </exception>
    public void Start()
    {
        var scene = _owner.Scene
                    ?? throw new InvalidOperationException("The UI node has no scene ownership.");
        scene.VerifyTickerStart();
        if (_registration is not null)
            return;

        _scene = scene;
        _registration = scene.RegisterTicker(this);
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

        _scene!.VerifyOwnerThread();
        StopCore();
    }

    internal void StopCore()
    {
        if (_registration is null)
            return;

        _scene!.UnregisterTicker(_registration);
        _owner.UnregisterTicker(this);
        _registration = null;
        _scene = null;
    }

    internal void Tick(LinkedListNode<Ticker> registration, TimeSpan frameTime)
    {
        if (ReferenceEquals(_registration, registration))
            _onTick(frameTime);
    }
}