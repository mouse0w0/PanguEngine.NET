namespace PanguEngine.Client.UI;

internal interface IUiPropertySubscription
{
    bool TryInvoke(UiNode sender, UiPropertyChangedEventArgs eventArgs);
}

internal sealed class UiPropertySubscription<T>(EventHandler<UiPropertyChangedEventArgs<T>> handler)
    : IUiPropertySubscription
{
    public bool TryInvoke(UiNode sender, UiPropertyChangedEventArgs eventArgs)
    {
        handler(sender, (UiPropertyChangedEventArgs<T>)eventArgs);
        return true;
    }
}

internal sealed class WeakUiPropertySubscription<T> : IUiPropertySubscription
{
    private readonly WeakReference<EventHandler<UiPropertyChangedEventArgs<T>>> _handler;

    public WeakUiPropertySubscription(EventHandler<UiPropertyChangedEventArgs<T>> handler)
    {
        _handler = new WeakReference<EventHandler<UiPropertyChangedEventArgs<T>>>(handler);
    }

    public bool TryInvoke(UiNode sender, UiPropertyChangedEventArgs eventArgs)
    {
        if (!_handler.TryGetTarget(out var promotedHandler))
            return false;

        promotedHandler(sender, (UiPropertyChangedEventArgs<T>)eventArgs);
        return true;
    }
}

internal sealed class UiPropertySubscriptionToken(
    UiNode owner,
    UiProperty property,
    IUiPropertySubscription subscription) : IDisposable
{
    private UiNode? _owner = owner;
    private IUiPropertySubscription? _subscription = subscription;

    public void Dispose()
    {
        var subscription = Interlocked.Exchange(ref _subscription, null);
        if (subscription is null)
            return;

        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.RemoveSubscription(property, subscription);
    }
}