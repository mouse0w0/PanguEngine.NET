using PanguEngine.Input;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Manages the lifecycle and layout of a single current UI screen.
/// </summary>
public sealed class UiManager
{
    private readonly int _ownerThreadId;
    private bool _isTransitioning;
    private bool _isUpdating;
    private bool _isShutdown;

    internal UiManager()
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Gets the current screen, or null when no screen is open.
    /// </summary>
    public UiScreen? CurrentScreen { get; private set; }

    /// <summary>
    /// Opens a screen, replacing the current screen when necessary.
    /// </summary>
    /// <param name="screen">The screen to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="screen"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the screen cannot be opened or the manager is performing another lifecycle or layout operation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Open(UiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        VerifyAccess();
        VerifyLifecycleOperation();
        if (ReferenceEquals(screen, CurrentScreen))
            return;

        screen.VerifyCanOpen();
        _isTransitioning = true;
        try
        {
            var old = CurrentScreen;
            if (old is not null)
            {
                old.VerifyCanClose();
                CurrentScreen = null;
                old.Close();
            }

            screen.Open();
            CurrentScreen = screen;
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// Closes the current screen.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager is performing another lifecycle or layout operation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Close()
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        var screen = CurrentScreen;
        if (screen is null)
            return;

        screen.VerifyCanClose();
        _isTransitioning = true;
        CurrentScreen = null;
        try
        {
            screen.Close();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    internal void Update(Size viewportSize)
    {
        VerifyAccess();
        if (_isUpdating || _isTransitioning)
            throw new InvalidOperationException("The UI manager cannot update in its current state.");

        UiScreen.CreateViewportBounds(viewportSize);
        _isUpdating = true;
        try
        {
            var screen = CurrentScreen;
            if (screen is null)
                return;

            screen.Update(viewportSize);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    internal void Shutdown()
    {
        if (_isShutdown)
            return;

        VerifyAccess();
        VerifyLifecycleOperation();
        var errors = new List<Exception>();
        var screen = CurrentScreen;
        CurrentScreen = null;
        _isTransitioning = true;
        try
        {
            if (screen is not null)
            {
                try
                {
                    screen.Close();
                }
                catch (Exception exception)
                {
                    AddLifecycleErrors(errors, exception);
                }
            }

            _isShutdown = true;
        }
        finally
        {
            _isTransitioning = false;
        }

        ThrowLifecycleErrors(errors);
    }

    internal void ProcessPointerMoved(Point position)
    {
        VerifyAccess();
        CurrentScreen?.ProcessPointerMoved(position);
    }

    internal void ProcessPointerPressed(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        CurrentScreen?.ProcessPointerPressed(position, button, modifiers);
    }

    internal void ProcessPointerReleased(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        CurrentScreen?.ProcessPointerReleased(position, button, modifiers);
    }

    internal void ProcessPointerWheel(Point position, double deltaX, double deltaY)
    {
        VerifyAccess();
        CurrentScreen?.ProcessPointerWheel(position, deltaX, deltaY);
    }

    internal void ProcessKeyDown(Key key, KeyModifiers modifiers)
    {
        VerifyAccess();
        CurrentScreen?.ProcessKeyDown(key, modifiers);
    }

    internal void ProcessKeyUp(Key key, KeyModifiers modifiers)
    {
        VerifyAccess();
        CurrentScreen?.ProcessKeyUp(key, modifiers);
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_isShutdown, this);
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI manager access requires its owner thread.");
    }

    private void VerifyLifecycleOperation()
    {
        if (_isTransitioning)
            throw new InvalidOperationException("The UI manager is already changing screens.");
        if (CurrentScreen?.IsUpdatingLayout == true)
            throw new InvalidOperationException("The UI manager cannot change screens during layout.");
    }

    private static void ThrowLifecycleErrors(List<Exception> errors)
    {
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private static void AddLifecycleErrors(List<Exception> errors, Exception exception)
    {
        if (exception is AggregateException aggregate)
            errors.AddRange(aggregate.InnerExceptions);
        else
            errors.Add(exception);
    }
}
