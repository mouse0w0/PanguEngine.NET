using System.Reflection;

namespace PanguEngine.Event;

/// <summary>
/// Default event bus implementation.
/// </summary>
public sealed class EventBus(IEventExceptionHandler exceptionHandler) : IEventBus
{
    private readonly IEventExceptionHandler _exceptionHandler =
        exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));

    private readonly Dictionary<object, List<EventListener>> _registrations =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Type, EventListenerList> _listenerLists = [];

    /// <inheritdoc />
    public void Register(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (listener is Type listenerType)
        {
            Register(listenerType);
            return;
        }

        if (_registrations.ContainsKey(listener))
            throw new InvalidOperationException($"Listener instance '{listener.GetType()}' is already registered.");

        var registrations = CreateMethodRegistrations(listener.GetType(), listener, false);
        _registrations.Add(listener, registrations);
        RegisterRegistrations(registrations);
    }

    /// <inheritdoc />
    public void Register(Type listenerType)
    {
        ArgumentNullException.ThrowIfNull(listenerType);
        if (_registrations.ContainsKey(listenerType))
            throw new InvalidOperationException($"Listener type '{listenerType}' is already registered.");

        var registrations = CreateMethodRegistrations(listenerType, null, true);
        _registrations.Add(listenerType, registrations);
        RegisterRegistrations(registrations);
    }

    /// <inheritdoc />
    public void Register<TEvent>(Action<TEvent> listener, Order order = Order.Default, bool receiveCanceled = false)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (_registrations.ContainsKey(listener))
            throw new InvalidOperationException($"Listener delegate '{listener.Method}' is already registered.");

        var registration = new EventListener(
            typeof(TEvent),
            listener.Method.DeclaringType ?? listener.GetType(),
            listener.Method,
            eventInstance => listener((TEvent)eventInstance),
            order,
            receiveCanceled);

        _registrations.Add(listener, [registration]);
        RegisterRegistrations([registration]);
    }

    /// <inheritdoc />
    public void Unregister(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        if (!_registrations.Remove(listener, out var registrations))
            return;

        UnregisterRegistrations(registrations);
    }

    /// <inheritdoc />
    public void Unregister(Type listenerType)
    {
        ArgumentNullException.ThrowIfNull(listenerType);
        if (!_registrations.Remove(listenerType, out var registrations))
            return;

        UnregisterRegistrations(registrations);
    }

    /// <inheritdoc />
    public void Unregister<TEvent>(Action<TEvent> listener) where TEvent : class, IEvent
    {
        Unregister((object)listener);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventInstance) where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(eventInstance);
        if (eventInstance.GetType().IsValueType)
            throw new ArgumentException("Event instances must be reference types.", nameof(eventInstance));

        var listenerList = GetListenerList(eventInstance.GetType());
        var listeners = listenerList.Listeners;
        for (var index = 0; index < listeners.Count; index++)
        {
            var listener = listeners[index];
            if (eventInstance is ICancelableEvent { IsCanceled: true } && !listener.ReceiveCanceled)
                continue;

            try
            {
                listener.Invoke(eventInstance);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                _exceptionHandler.Handle(this, eventInstance, listeners, index, ex.InnerException);
                return;
            }
            catch (Exception ex)
            {
                _exceptionHandler.Handle(this, eventInstance, listeners, index, ex);
                return;
            }
        }
    }

    private void RegisterRegistrations(IReadOnlyList<EventListener> registrations)
    {
        foreach (var group in registrations.GroupBy(registration => registration.EventType))
        {
            var listenerList = GetListenerList(group.Key);
            foreach (var registration in group)
                listenerList.AddListener(registration);
        }
    }

    private void UnregisterRegistrations(IReadOnlyList<EventListener> registrations)
    {
        foreach (var group in registrations.GroupBy(registration => registration.EventType))
        {
            if (!_listenerLists.TryGetValue(group.Key, out var listenerList))
                continue;

            foreach (var registration in group)
                listenerList.RemoveListener(registration);
        }
    }

    private static List<EventListener> CreateMethodRegistrations(Type listenerType, object? target, bool staticOnly)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    (staticOnly ? BindingFlags.Static : BindingFlags.Instance);
        var methods = listenerType.GetMethods(flags)
            .Select(method => new { Method = method, Attribute = method.GetCustomAttribute<ListenerAttribute>() })
            .Where(entry => entry.Attribute is not null)
            .ToArray();

        if (methods.Length == 0)
            throw new InvalidOperationException($"Listener '{listenerType}' does not declare any listener methods.");

        var registrations = new List<EventListener>();
        foreach (var entry in methods)
        {
            var method = entry.Method;
            var attribute = entry.Attribute!;
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
                throw new InvalidOperationException(
                    $"Listener method '{listenerType}.{method.Name}' must have exactly one parameter.");

            var eventType = parameters[0].ParameterType;
            if (eventType.IsValueType || !typeof(IEvent).IsAssignableFrom(eventType))
                throw new InvalidOperationException(
                    $"Listener method '{listenerType}.{method.Name}' parameter must be an event reference type.");

            registrations.Add(new EventListener(
                eventType,
                listenerType,
                method,
                eventInstance => method.Invoke(target, [eventInstance]),
                attribute.Order,
                attribute.ReceiveCanceled));
        }

        return registrations;
    }

    private EventListenerList GetListenerList(Type eventType)
    {
        if (_listenerLists.TryGetValue(eventType, out var listenerList))
            return listenerList;

        var existingLists = _listenerLists.ToArray();
        listenerList = new EventListenerList();
        _listenerLists.Add(eventType, listenerList);

        foreach (var (existingType, existingListenerList) in existingLists)
        {
            if (existingType.IsAssignableFrom(eventType))
            {
                existingListenerList.AddChild(listenerList);
                continue;
            }

            if (eventType.IsAssignableFrom(existingType))
                listenerList.AddChild(existingListenerList);
        }

        return listenerList;
    }
}