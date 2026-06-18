using System.Reflection;

namespace PanguEngine.Event;

internal sealed class EventListener(
    Type eventType,
    Type ownerType,
    MethodInfo method,
    Action<IEvent> invoke,
    Order order,
    bool receiveCanceled) : IEventListener
{
    public Type OwnerType { get; } = ownerType;

    public MethodInfo Method { get; } = method;

    public Type EventType { get; } = eventType;

    public Action<IEvent> Invoke { get; } = invoke;

    public Order Order { get; } = order;

    public bool ReceiveCanceled { get; } = receiveCanceled;
}