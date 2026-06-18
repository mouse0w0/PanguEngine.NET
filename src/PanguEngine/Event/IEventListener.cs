using System.Reflection;

namespace PanguEngine.Event;

/// <summary>
/// Describes an event listener.
/// </summary>
public interface IEventListener
{
    /// <summary>
    /// The type that owns the listener.
    /// </summary>
    Type OwnerType { get; }

    /// <summary>
    /// The listener method.
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// The event type accepted by the listener.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// The listener order.
    /// </summary>
    Order Order { get; }

    /// <summary>
    /// Whether the listener receives canceled events.
    /// </summary>
    bool ReceiveCanceled { get; }
}