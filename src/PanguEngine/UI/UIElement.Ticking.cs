namespace PanguEngine.UI;

public abstract partial class UIElement
{
    private HashSet<Ticker>? _activeTickers;

    /// <summary>
    /// Creates an inactive frame ticker associated with this element.
    /// </summary>
    /// <param name="onTick">The callback receiving the absolute monotonic frame time.</param>
    /// <returns>An independent ticker that can be started while this element's scene is open.</returns>
    /// <remarks>
    /// Creating an inactive ticker does not require an open scene or UI thread access. Starting and stopping active runs require the host UI thread.
    /// Keep a reference to reuse the ticker. Only active tickers are registered with this element.
    /// Frame time uses the monotonic clock's origin, not the start of a ticker run.
    /// Consumers establish a baseline on the first callback of each new run and calculate subsequent differences.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onTick"/> is null.</exception>
    protected Ticker CreateTicker(Action<TimeSpan> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        return new Ticker(this, onTick);
    }

    internal void RegisterTicker(Ticker ticker) =>
        (_activeTickers ??= []).Add(ticker);

    internal void UnregisterTicker(Ticker ticker) =>
        _activeTickers!.Remove(ticker);

    private void StopActiveTickers()
    {
        while (_activeTickers is { Count: > 0 })
            _activeTickers.First().StopCore();
    }
}
