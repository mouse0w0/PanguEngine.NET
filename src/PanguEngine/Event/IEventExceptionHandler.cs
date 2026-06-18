namespace PanguEngine.Event;

/// <summary>
/// Handles exceptions thrown by event listeners.
/// </summary>
public interface IEventExceptionHandler
{
    /// <summary>
    /// Handles a listener exception.
    /// </summary>
    /// <param name="bus">The event bus publishing the event.</param>
    /// <param name="eventInstance">The event being published.</param>
    /// <param name="listeners">The listener list used for the current dispatch.</param>
    /// <param name="index">The index of the listener that threw.</param>
    /// <param name="exception">The captured exception.</param>
    void Handle(IEventBus bus, IEvent eventInstance, IReadOnlyList<IEventListener> listeners, int index,
        Exception exception);
}