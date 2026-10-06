namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private HashSet<UiTicker>? _activeTickers;

    /// <summary>
    /// Creates an inactive frame ticker associated with this node.
    /// </summary>
    /// <param name="onTick">The callback receiving the absolute monotonic frame time.</param>
    /// <returns>An independent ticker that can be started while this node's screen is open.</returns>
    /// <remarks>
    /// Tickers may be created before mounting. When the node's screen is open, creation and use require its UI thread.
    /// Keep a reference to reuse the ticker. Only active tickers are registered with this node.
    /// Frame time uses the monotonic clock's origin, not the start of a ticker run.
    /// Consumers establish a baseline on the first callback of each new run and calculate subsequent differences.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onTick"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an open screen is accessed from the wrong thread.</exception>
    protected UiTicker CreateTicker(Action<TimeSpan> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        Screen?.VerifyTreeAccess();
        return new UiTicker(this, onTick);
    }

    internal void RegisterTicker(UiTicker ticker) =>
        (_activeTickers ??= []).Add(ticker);

    internal void UnregisterTicker(UiTicker ticker) =>
        _activeTickers!.Remove(ticker);

    private void StopActiveTickers()
    {
        while (_activeTickers is { Count: > 0 })
            _activeTickers.First().StopCore();
    }
}
