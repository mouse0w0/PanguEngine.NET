namespace PanguEngine.ComponentModel;

public abstract partial class ObservableObject
{
    private Dictionary<Property, PropertySubscriptionList>? _subscriptions;

    /// <summary>
    /// Subscribes to changes of one property without sending its current value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <param name="handler">The change handler.</param>
    /// <returns>A token that removes this subscription when disposed.</returns>
    public IDisposable Subscribe<T>(
        Property<T> property,
        EventHandler<PropertyChangedEventArgs<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(handler);
        property.VerifyOwner(this);

        return AddSubscription(property, new PropertySubscription<T>(handler));
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
        Property<T> property,
        EventHandler<PropertyChangedEventArgs<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(handler);
        property.VerifyOwner(this);

        return AddSubscription(property, new WeakPropertySubscription<T>(handler));
    }

    private PropertySubscriptionToken AddSubscription(
        Property property,
        IPropertySubscription subscription)
    {
        _subscriptions ??= [];
        if (!_subscriptions.TryGetValue(property, out var subscriptions))
        {
            subscriptions = new PropertySubscriptionList();
            _subscriptions.Add(property, subscriptions);
        }
        else if (subscriptions.ReaderCount > 0)
        {
            subscriptions = subscriptions.Clone();
            _subscriptions[property] = subscriptions;
        }

        subscriptions.Items.Add(subscription);
        return new PropertySubscriptionToken(this, property, subscription);
    }

    internal void RemoveSubscription(Property property, IPropertySubscription subscription)
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

    private PropertySubscriptionList? BeginSubscriptionNotification(Property property)
    {
        if (_subscriptions is null || !_subscriptions.TryGetValue(property, out var subscriptions))
            return null;

        subscriptions.ReaderCount++;
        return subscriptions;
    }

    private static void EndSubscriptionNotification(PropertySubscriptionList? subscriptions)
    {
        if (subscriptions is not null)
            subscriptions.ReaderCount--;
    }

    private void NotifySubscriptions(
        PropertyChangedEventArgs eventArgs,
        PropertySubscriptionList? subscriptions)
    {
        if (subscriptions is null)
            return;

        foreach (var subscription in subscriptions.Items)
        {
            if (!subscription.TryInvoke(this, eventArgs))
                RemoveSubscription(eventArgs.Property, subscription);
        }
    }

    private sealed class PropertySubscriptionList
    {
        public PropertySubscriptionList()
        {
            Items = [];
        }

        private PropertySubscriptionList(List<IPropertySubscription> items)
        {
            Items = items;
        }

        public List<IPropertySubscription> Items { get; }

        public int ReaderCount { get; set; }

        public PropertySubscriptionList Clone() =>
            new([.. Items]);
    }
}
