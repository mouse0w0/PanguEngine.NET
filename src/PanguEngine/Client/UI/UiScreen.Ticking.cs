namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    private readonly LinkedList<UiTicker> _activeTickers = new();

    internal void VerifyTickerStart()
    {
        lock (_stateSync)
        {
            VerifyOwnerThreadCore();
            if (_isClosing)
                throw new InvalidOperationException("The UI screen is closing.");
        }
    }

    internal LinkedListNode<UiTicker> RegisterTicker(UiTicker ticker) =>
        _activeTickers.AddLast(ticker);

    internal void UnregisterTicker(LinkedListNode<UiTicker> registration) =>
        _activeTickers.Remove(registration);

    private void StopAllTickers()
    {
        while (_activeTickers.First is { } registration)
            registration.Value.StopCore();
    }

    private LinkedListNode<UiTicker>[] FreezeTickers()
    {
        var registrations = new LinkedListNode<UiTicker>[_activeTickers.Count];
        var index = 0;
        for (var registration = _activeTickers.First; registration is not null; registration = registration.Next)
            registrations[index++] = registration;

        return registrations;
    }

    private static void AdvanceTickers(LinkedListNode<UiTicker>[] registrations, TimeSpan frameTime)
    {
        foreach (var registration in registrations)
            registration.Value.Tick(registration, frameTime);
    }
}
