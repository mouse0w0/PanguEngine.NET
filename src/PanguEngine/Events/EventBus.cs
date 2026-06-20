using System.Reflection;

namespace PanguEngine.Events;

/// <summary>
/// Default event bus implementation.
/// </summary>
public sealed class EventBus(IEventExceptionHandler exceptionHandler) : IEventBus
{
    private readonly Lock _lock = new();

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

        var registrations = CreateMethodRegistrations(listener.GetType(), listener, false);
        lock (_lock)
        {
            if (!_registrations.TryAdd(listener, registrations))
                throw new InvalidOperationException($"Listener instance '{listener.GetType()}' is already registered.");

            RegisterRegistrations(registrations);
        }
    }

    /// <inheritdoc />
    public void Register(Type listenerType)
    {
        ArgumentNullException.ThrowIfNull(listenerType);
        var registrations = CreateMethodRegistrations(listenerType, null, true);
        lock (_lock)
        {
            if (!_registrations.TryAdd(listenerType, registrations))
                throw new InvalidOperationException($"Listener type '{listenerType}' is already registered.");

            RegisterRegistrations(registrations);
        }
    }

    /// <inheritdoc />
    public void Register<TEvent>(Action<TEvent> listener, Order order = Order.Default, bool receiveCanceled = false)
        where TEvent : Event
    {
        ArgumentNullException.ThrowIfNull(listener);
        var registration = new EventListener(
            typeof(TEvent),
            listener.Method.DeclaringType ?? listener.GetType(),
            listener.Method,
            eventInstance => listener((TEvent)eventInstance),
            order,
            receiveCanceled);

        lock (_lock)
        {
            if (!_registrations.TryAdd(listener, [registration]))
                throw new InvalidOperationException($"Listener delegate '{listener.Method}' is already registered.");

            RegisterRegistrations([registration]);
        }
    }

    /// <inheritdoc />
    public void Unregister(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        lock (_lock)
        {
            if (!_registrations.Remove(listener, out var registrations))
                return;

            UnregisterRegistrations(registrations);
        }
    }

    /// <inheritdoc />
    public void Unregister(Type listenerType)
    {
        ArgumentNullException.ThrowIfNull(listenerType);
        lock (_lock)
        {
            if (!_registrations.Remove(listenerType, out var registrations))
                return;

            UnregisterRegistrations(registrations);
        }
    }

    /// <inheritdoc />
    public void Unregister<TEvent>(Action<TEvent> listener) where TEvent : Event
    {
        Unregister((object)listener);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventInstance) where TEvent : Event
    {
        ArgumentNullException.ThrowIfNull(eventInstance);

        IReadOnlyList<EventListener> listeners;
        lock (_lock)
        {
            listeners = GetListenerList(eventInstance.GetType()).Listeners;
        }

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
            if (!typeof(Event).IsAssignableFrom(eventType))
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

        var baseType = eventType.BaseType;
        var parent = baseType is not null && typeof(Event).IsAssignableFrom(baseType)
            ? GetListenerList(baseType)
            : null;

        listenerList = new EventListenerList(parent);
        _listenerLists.Add(eventType, listenerList);

        return listenerList;
    }
}