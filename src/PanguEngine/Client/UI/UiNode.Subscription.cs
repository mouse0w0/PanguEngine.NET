namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private Dictionary<UiProperty, UiPropertySubscriptionList>? _subscriptions;

    /// <summary>
    /// Subscribes to changes of one property without sending its current value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <param name="handler">The change handler.</param>
    /// <returns>A token that removes this subscription when disposed.</returns>
    public IDisposable Subscribe<T>(
        UiProperty<T> property,
        EventHandler<UiPropertyChangedEventArgs<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(handler);
        property.VerifyOwner(this);

        return AddSubscription(property, new UiPropertySubscription<T>(handler));
    }

    /// <summary>
    /// Subscribes weakly to changes of one property without sending its current value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <param name="handler">The change handler held by weak reference.</param>
    /// <returns>A token that removes this subscription when disposed.</returns>
    /// <remarks>
    /// The returned token does not keep <paramref name="handler"/> alive. Keep a separate strong
    /// reference to the handler for as long as notifications are required. A compiler-cached
    /// delegate may never be collected, so callers cannot rely on handler collection.
    /// </remarks>
    public IDisposable SubscribeWeak<T>(
        UiProperty<T> property,
        EventHandler<UiPropertyChangedEventArgs<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(handler);
        property.VerifyOwner(this);

        return AddSubscription(property, new WeakUiPropertySubscription<T>(handler));
    }

    private UiPropertySubscriptionToken AddSubscription(
        UiProperty property,
        IUiPropertySubscription subscription)
    {
        _subscriptions ??= [];
        if (!_subscriptions.TryGetValue(property, out var subscriptions))
        {
            subscriptions = new UiPropertySubscriptionList();
            _subscriptions.Add(property, subscriptions);
        }
        else if (subscriptions.ReaderCount > 0)
        {
            subscriptions = subscriptions.Clone();
            _subscriptions[property] = subscriptions;
        }

        subscriptions.Items.Add(subscription);
        return new UiPropertySubscriptionToken(this, property, subscription);
    }

    internal void RemoveSubscription(UiProperty property, IUiPropertySubscription subscription)
    {
        if (_subscriptions is null || !_subscriptions.TryGetValue(property, out var subscriptions))
            return;

        var index = subscriptions.Items.IndexOf(subscription);
        if (index < 0)
            return;

        if (subscriptions.Items.Count == 1)
        {
            _subscriptions.Remove(property);
            if (_subscriptions.Count == 0)
                _subscriptions = null;
            return;
        }

        if (subscriptions.ReaderCount > 0)
        {
            subscriptions = subscriptions.Clone();
            _subscriptions[property] = subscriptions;
        }

        subscriptions.Items.RemoveAt(index);
    }

    private UiPropertySubscriptionList? BeginSubscriptionNotification(UiProperty property)
    {
        if (_subscriptions is null || !_subscriptions.TryGetValue(property, out var subscriptions))
            return null;

        subscriptions.ReaderCount++;
        return subscriptions;
    }

    private static void EndSubscriptionNotification(UiPropertySubscriptionList? subscriptions)
    {
        if (subscriptions is not null)
            subscriptions.ReaderCount--;
    }

    private void NotifySubscriptions(
        UiPropertyChangedEventArgs eventArgs,
        UiPropertySubscriptionList? subscriptions)
    {
        if (subscriptions is null)
            return;

        foreach (var subscription in subscriptions.Items)
        {
            if (!subscription.TryInvoke(this, eventArgs))
                RemoveSubscription(eventArgs.Property, subscription);
        }
    }

    private sealed class UiPropertySubscriptionList
    {
        public UiPropertySubscriptionList()
        {
            Items = [];
        }

        private UiPropertySubscriptionList(List<IUiPropertySubscription> items)
        {
            Items = items;
        }

        public List<IUiPropertySubscription> Items { get; }

        public int ReaderCount { get; set; }

        public UiPropertySubscriptionList Clone() =>
            new([.. Items]);
    }
}