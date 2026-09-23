using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Input;
using PanguEngine.Registries;

namespace PanguEngine.Client.UI;

/// <summary>
/// Manages the persistent HUD and the current regular UI screen.
/// </summary>
/// <remarks>The engine owns this manager and destroys it after UI dispatch has stopped.</remarks>
public sealed class UiManager
{
    private readonly int _ownerThreadId;
    private readonly Queue<UiScreen?> _pendingScreens = [];
    private bool _isTransitioning;
    private bool _isDestroying;
    private bool _isInitializingHud;
    private bool _isUpdating;
    private bool _destroyed;

    internal UiManager()
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
        Hud = new HudScreen();
        Hud.Open();
    }

    /// <summary>
    /// Gets the persistent client HUD.
    /// </summary>
    public HudScreen Hud { get; }

    /// <summary>
    /// Gets the current screen, or null when no screen is open.
    /// </summary>
    public UiScreen? CurrentScreen { get; private set; }

    internal event Action<UiScreen?, UiScreen?>? CurrentScreenChanged;

    internal void InitializeHud(IRegistry<HudDefinition> definitions)
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        if (!definitions.IsFrozen)
            throw new InvalidOperationException("HUD initialization requires frozen definitions.");

        _isTransitioning = true;
        _isInitializingHud = true;
        try
        {
            Hud.Initialize(definitions);
        }
        finally
        {
            _isInitializingHud = false;
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// Opens a screen, replacing the current screen when necessary.
    /// </summary>
    /// <remarks>
    /// Reentrant requests execute in order after the current screen change and its notifications complete.
    /// A lifecycle failure propagates immediately and prevents subsequent screen change notifications.
    /// Deferred failures are reported by the outermost Open or Close call, and remaining requests are discarded.
    /// Screen changes from update callbacks must be scheduled through the engine dispatcher.
    /// </remarks>
    /// <param name="screen">The screen to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="screen"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the screen cannot be opened or the manager is initializing the HUD, shutting down,
    /// updating, updating layout, or generating drawing commands.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Open(UiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        RequestScreenChange(screen);
    }

    /// <summary>
    /// Closes the current screen.
    /// </summary>
    /// <remarks>
    /// Reentrant requests execute after the current screen change and its notifications complete.
    /// A deferred close applies to the screen current when the request executes.
    /// A lifecycle failure propagates immediately and prevents subsequent screen change notifications.
    /// Deferred failures are reported by the outermost Open or Close call, and remaining requests are discarded.
    /// Screen changes from update callbacks must be scheduled through the engine dispatcher.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager is initializing the HUD, shutting down, updating, updating layout,
    /// or generating drawing commands.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Close() => RequestScreenChange(null);

    private void RequestScreenChange(UiScreen? screen)
    {
        VerifyAccess();
        VerifyLifecycleOperation(allowQueuedChange: true);
        VerifyNotUpdating();
        _pendingScreens.Enqueue(screen);
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        try
        {
            while (_pendingScreens.TryDequeue(out var nextScreen))
            {
                if (ReferenceEquals(nextScreen, CurrentScreen))
                    continue;

                nextScreen?.VerifyCanOpen();
                var oldScreen = CurrentScreen;
                oldScreen?.VerifyCanClose();
                if (oldScreen is not null)
                {
                    CurrentScreen = null;
                    oldScreen.Close();
                }

                nextScreen?.Open();
                CurrentScreen = nextScreen;
                NotifyCurrentScreenChanged(oldScreen);
            }
        }
        finally
        {
            _pendingScreens.Clear();
            _isTransitioning = false;
        }
    }

    internal void Update()
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();

        _isUpdating = true;
        try
        {
            Hud.Update();
            CurrentScreen?.Update();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    internal void PrepareFrame(Size viewportSize, double alpha)
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();

        _isUpdating = true;
        try
        {
            Hud.PrepareFrame(viewportSize, alpha);
            CurrentScreen?.PrepareFrame(viewportSize, alpha);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    internal void AppendDrawCommands(UiDrawCommandList commands)
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        commands.Append(Hud.Screen);
        if (CurrentScreen is { } screen)
            commands.Append(screen);
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;

        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        var screen = CurrentScreen;
        CurrentScreen = null;
        _isTransitioning = true;
        _isDestroying = true;
        try
        {
            screen?.Close();
            Hud.Close();
            _destroyed = true;
            NotifyCurrentScreenChanged(screen);
        }
        finally
        {
            _isDestroying = false;
            _isTransitioning = false;
        }
    }

    internal bool ProcessPointerMoved(Point position)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessPointerMoved(position) ?? false;
    }

    internal bool ProcessPointerPressed(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessPointerPressed(position, button, modifiers) ?? false;
    }

    internal bool ProcessPointerReleased(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessPointerReleased(position, button, modifiers) ?? false;
    }

    internal bool ProcessPointerWheel(Point position, double deltaX, double deltaY)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessPointerWheel(position, deltaX, deltaY) ?? false;
    }

    internal bool ProcessKeyDown(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessKeyDown(key, modifiers, isRepeat) ?? false;
    }

    internal bool ProcessKeyUp(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        return CurrentScreen?.ProcessKeyUp(key, modifiers, isRepeat) ?? false;
    }

    internal void ProcessTextInput(string text)
    {
        VerifyAccess();
        CurrentScreen?.ProcessTextInput(text);
    }

    internal void ProcessFocusChanged(bool focused)
    {
        VerifyAccess();
        CurrentScreen?.ProcessFocusChanged(focused);
    }

    private void NotifyCurrentScreenChanged(UiScreen? oldScreen)
    {
        var newScreen = CurrentScreen;
        if (!ReferenceEquals(oldScreen, newScreen))
            CurrentScreenChanged?.Invoke(oldScreen, newScreen);
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI manager access requires its owner thread.");
    }

    private void VerifyLifecycleOperation(bool allowQueuedChange = false)
    {
        if (_isTransitioning && (!allowQueuedChange || _isDestroying || _isInitializingHud))
            throw new InvalidOperationException("The UI manager is already changing screens.");
        if (Hud.Screen.IsUpdatingLayout || Hud.Screen.IsDrawing)
        {
            throw new InvalidOperationException(
                "The UI manager cannot change screens while the HUD is updating or drawing.");
        }

        if (CurrentScreen?.IsUpdatingLayout == true)
            throw new InvalidOperationException("The UI manager cannot change screens during layout.");
        if (CurrentScreen?.IsDrawing == true)
        {
            throw new InvalidOperationException(
                "The UI manager cannot change screens while drawing commands are generated.");
        }
    }

    private void VerifyNotUpdating()
    {
        if (_isUpdating || Hud.Screen.IsUpdating || CurrentScreen?.IsUpdating == true)
            throw new InvalidOperationException("The UI manager cannot perform this operation during an update.");
    }
}