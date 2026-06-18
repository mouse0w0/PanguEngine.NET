namespace PanguEngine.Event;

/// <summary>
/// Marks a method as an event listener.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ListenerAttribute : Attribute
{
    /// <summary>
    /// The listener execution order.
    /// </summary>
    public Order Order { get; set; } = Order.Default;

    /// <summary>
    /// Whether the listener receives events that have already been canceled.
    /// </summary>
    public bool ReceiveCanceled { get; set; }
}