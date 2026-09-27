namespace PanguEngine.ComponentModel;

internal interface IPropertySubscription
{
    bool TryInvoke(ObservableObject sender, PropertyChangedEventArgs eventArgs);
}

internal sealed class PropertySubscription<T>(EventHandler<PropertyChangedEventArgs<T>> handler)
    : IPropertySubscription
{
    public bool TryInvoke(ObservableObject sender, PropertyChangedEventArgs eventArgs)
    {
        handler(sender, (PropertyChangedEventArgs<T>)eventArgs);
        return true;
    }
}

internal sealed class WeakPropertySubscription<T> : IPropertySubscription
{
    private readonly WeakReference<EventHandler<PropertyChangedEventArgs<T>>> _handler;

    public WeakPropertySubscription(EventHandler<PropertyChangedEventArgs<T>> handler)
    {
        _handler = new WeakReference<EventHandler<PropertyChangedEventArgs<T>>>(handler);
    }

    public bool TryInvoke(ObservableObject sender, PropertyChangedEventArgs eventArgs)
    {
        if (!_handler.TryGetTarget(out var promotedHandler))
            return false;

        promotedHandler(sender, (PropertyChangedEventArgs<T>)eventArgs);
        return true;
    }
}

internal sealed class PropertySubscriptionToken(
    ObservableObject owner,
    Property property,
    IPropertySubscription subscription) : IDisposable
{
    private ObservableObject? _owner = owner;
    private IPropertySubscription? _subscription = subscription;

    public void Dispose()
    {
        var subscription = Interlocked.Exchange(ref _subscription, null);
        if (subscription is null)
            return;

        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.RemoveSubscription(property, subscription);
    }
}
