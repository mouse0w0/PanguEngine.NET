using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Manages the lifecycle and layout of a single current UI screen.
/// </summary>
public sealed class UiManager
{
    private bool _isTransitioning;
    private bool _isUpdating;
    private bool _isLayingOut;
    private volatile bool _isShutdown;

    internal UiManager()
    {
        Dispatcher = new UiDispatcher();
    }

    /// <summary>
    /// Gets the dispatcher that owns active UI work.
    /// </summary>
    public UiDispatcher Dispatcher { get; }

    /// <summary>
    /// Gets the current screen, or null when no screen is open.
    /// </summary>
    public Screen? CurrentScreen { get; private set; }

    /// <summary>
    /// Opens a screen, replacing the current screen when necessary.
    /// </summary>
    /// <param name="screen">The screen to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="screen"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the screen cannot be opened or the manager is performing another lifecycle or
    /// layout operation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Open(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        Dispatcher.VerifyAccess();
        VerifyLifecycleOperation();
        if (ReferenceEquals(screen, CurrentScreen))
            return;

        VerifyRootCanAttach(screen);
        if (!screen.TryClaim(this))
            throw new InvalidOperationException("The UI screen is already owned by another manager.");

        var releaseCandidate = true;
        _isTransitioning = true;
        try
        {
            if (CurrentScreen is not null)
            {
                CloseCurrent();
                VerifyRootCanAttach(screen);
            }

            screen.ResetInputStateForOpening();
            screen.InvokeOpening(this);
            VerifyRootCanAttach(screen);
            screen.Root.AttachToTree(Dispatcher, screen);
            CurrentScreen = screen;
            releaseCandidate = false;

            try
            {
                screen.InvokeOpened(this);
            }
            catch (Exception exception)
            {
                var errors = new List<Exception> { exception };
                ForceCloseCurrent(errors);
                ThrowLifecycleErrors(errors);
            }
        }
        finally
        {
            try
            {
                if (releaseCandidate)
                    screen.Release(this);
            }
            finally
            {
                _isTransitioning = false;
            }
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
        Dispatcher.VerifyAccess();
        VerifyLifecycleOperation();
        if (CurrentScreen is null)
            return;

        _isTransitioning = true;
        try
        {
            CloseCurrent();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    internal void Update(Size viewportSize)
    {
        Dispatcher.VerifyAccess();
        if (_isUpdating || _isTransitioning || _isLayingOut)
            throw new InvalidOperationException("The UI manager cannot update in its current state.");

        var viewportBounds = new Rect(0, 0, viewportSize);
        _isUpdating = true;
        try
        {
            Dispatcher.DrainPending();
            if (_isShutdown)
                return;

            var screen = CurrentScreen;
            if (screen is null)
                return;

            _isLayingOut = true;
            try
            {
                var root = screen.Root;
                root.Measure(viewportSize);
                if (root.IsMeasureValid)
                    root.Arrange(viewportBounds);
            }
            finally
            {
                _isLayingOut = false;
            }

            if (ReferenceEquals(CurrentScreen, screen))
                screen.RefreshPointerAfterLayout();
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

        Dispatcher.VerifyAccess();
        VerifyLifecycleOperation();
        var errors = new List<Exception>();
        _isTransitioning = true;
        try
        {
            ForceCloseCurrent(errors);
            Dispatcher.Shutdown();
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
        Dispatcher.VerifyAccess();
        CurrentScreen?.ProcessPointerMoved(position);
    }

    internal void ProcessPointerPressed(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        Dispatcher.VerifyAccess();
        Screen.VerifyMouseButton(button);
        CurrentScreen?.ProcessPointerPressed(position, button, modifiers);
    }

    internal void ProcessPointerReleased(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        Dispatcher.VerifyAccess();
        Screen.VerifyMouseButton(button);
        CurrentScreen?.ProcessPointerReleased(position, button, modifiers);
    }

    internal void ProcessPointerWheel(Point position, double deltaX, double deltaY)
    {
        Dispatcher.VerifyAccess();
        Screen.VerifyWheelDelta(deltaX, nameof(deltaX));
        Screen.VerifyWheelDelta(deltaY, nameof(deltaY));
        CurrentScreen?.ProcessPointerWheel(position, deltaX, deltaY);
    }

    internal void ProcessKeyDown(Key key, KeyModifiers modifiers)
    {
        Dispatcher.VerifyAccess();
        CurrentScreen?.ProcessKeyDown(key, modifiers);
    }

    internal void ProcessKeyUp(Key key, KeyModifiers modifiers)
    {
        Dispatcher.VerifyAccess();
        CurrentScreen?.ProcessKeyUp(key, modifiers);
    }

    private void CloseCurrent()
    {
        var screen = CurrentScreen!;
        screen.InvokeClosing(this);
        var errors = new List<Exception>();
        try
        {
            screen.Root.DetachFromTree();
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }

        if (screen.Root.ActiveDispatcher is not null)
        {
            ThrowLifecycleErrors(errors);
            return;
        }

        screen.ClearInputStateAfterClose();
        CurrentScreen = null;
        try
        {
            screen.InvokeClosed(this);
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }
        finally
        {
            screen.Release(this);
        }

        ThrowLifecycleErrors(errors);
    }

    private void ForceCloseCurrent(List<Exception> errors)
    {
        var screen = CurrentScreen;
        if (screen is null)
            return;

        try
        {
            screen.InvokeClosing(this);
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }

        try
        {
            screen.Root.DetachFromTree();
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }

        if (screen.Root.ActiveDispatcher is not null)
            return;

        screen.ClearInputStateAfterClose();
        CurrentScreen = null;
        try
        {
            screen.InvokeClosed(this);
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }
        finally
        {
            screen.Release(this);
        }
    }

    private void VerifyLifecycleOperation()
    {
        if (_isTransitioning)
            throw new InvalidOperationException("The UI manager is already changing screens.");
        if (_isLayingOut)
            throw new InvalidOperationException("The UI manager cannot change screens during layout.");
    }

    private static void VerifyRootCanAttach(Screen screen)
    {
        if (screen.Root.Parent is not null || screen.Root.ActiveDispatcher is not null)
            throw new InvalidOperationException("The UI screen root must be an inactive root node.");
    }

    private static void ThrowLifecycleErrors(List<Exception> errors)
    {
        if (errors.Count == 1)
            throw errors[0];
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
