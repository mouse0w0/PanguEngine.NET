namespace PanguEngine.Event;

/// <summary>
/// Rethrows exceptions thrown by event listeners.
/// </summary>
public sealed class ThrowingEventExceptionHandler : IEventExceptionHandler
{
    /// <summary>
    /// The shared throwing exception handler instance.
    /// </summary>
    public static ThrowingEventExceptionHandler Instance { get; } = new();

    private ThrowingEventExceptionHandler()
    {
    }

    /// <inheritdoc />
    public void Handle(IEventBus bus, IEvent eventInstance, IReadOnlyList<IEventListener> listeners, int index,
        Exception exception)
    {
        throw exception;
    }
}