namespace PanguEngine.Event;

/// <summary>
/// Dispatches runtime events to registered listeners.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Registers instance listener methods on an object.
    /// </summary>
    /// <param name="listener">The listener instance.</param>
    void Register(object listener);

    /// <summary>
    /// Registers static listener methods on a type.
    /// </summary>
    /// <param name="listenerType">The listener type.</param>
    void Register(Type listenerType);

    /// <summary>
    /// Registers a delegate listener for a normal event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="listener">The listener delegate.</param>
    /// <param name="order">The listener order.</param>
    /// <param name="receiveCanceled">Whether the listener receives canceled events.</param>
    void Register<TEvent>(Action<TEvent> listener, Order order = Order.Default, bool receiveCanceled = false)
        where TEvent : class, IEvent;

    /// <summary>
    /// Unregisters all listener methods for an object.
    /// </summary>
    /// <param name="listener">The listener instance.</param>
    void Unregister(object listener);

    /// <summary>
    /// Unregisters all static listener methods for a type.
    /// </summary>
    /// <param name="listenerType">The listener type.</param>
    void Unregister(Type listenerType);

    /// <summary>
    /// Unregisters a delegate listener for an event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="listener">The listener delegate.</param>
    void Unregister<TEvent>(Action<TEvent> listener) where TEvent : class, IEvent;

    /// <summary>
    /// Publishes an event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventInstance">The event instance.</param>
    void Publish<TEvent>(TEvent eventInstance) where TEvent : class, IEvent;
}