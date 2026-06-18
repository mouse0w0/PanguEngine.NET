namespace PanguEngine.Event;

/// <summary>
/// Represents an event that can be canceled.
/// </summary>
public interface ICancelableEvent : IEvent
{
    /// <summary>
    /// Whether the event has been canceled.
    /// </summary>
    bool IsCanceled { get; }

    /// <summary>
    /// Cancels the event.
    /// </summary>
    void Cancel();
}