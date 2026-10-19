namespace PanguEngine.UI;

public partial class UIScene
{
    private readonly LinkedList<Ticker> _activeTickers = new();

    internal void VerifyTickerStart()
    {
        lock (_stateSync)
        {
            VerifyOwnerThreadCore();
            if (_isClosing)
                throw new InvalidOperationException("The UI scene is closing.");
        }
    }

    internal LinkedListNode<Ticker> RegisterTicker(Ticker ticker) =>
        _activeTickers.AddLast(ticker);

    internal void UnregisterTicker(LinkedListNode<Ticker> registration) =>
        _activeTickers.Remove(registration);

    private void StopAllTickers()
    {
        while (_activeTickers.First is { } registration)
            registration.Value.StopCore();
    }

    private LinkedListNode<Ticker>[] FreezeTickers()
    {
        var registrations = new LinkedListNode<Ticker>[_activeTickers.Count];
        var index = 0;
        for (var registration = _activeTickers.First; registration is not null; registration = registration.Next)
            registrations[index++] = registration;

        return registrations;
    }

    private static void AdvanceTickers(LinkedListNode<Ticker>[] registrations, TimeSpan frameTime)
    {
        foreach (var registration in registrations)
            registration.Value.Tick(registration, frameTime);
    }
}