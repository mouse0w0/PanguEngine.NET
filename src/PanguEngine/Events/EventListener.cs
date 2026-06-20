using System.Reflection;

namespace PanguEngine.Events;

internal sealed class EventListener(
    Type eventType,
    Type ownerType,
    MethodInfo method,
    Action<Event> invoke,
    Order order,
    bool receiveCanceled) : IEventListener
{
    public Type OwnerType { get; } = ownerType;

    public MethodInfo Method { get; } = method;

    public Type EventType { get; } = eventType;

    public Action<Event> Invoke { get; } = invoke;

    public Order Order { get; } = order;

    public bool ReceiveCanceled { get; } = receiveCanceled;
}