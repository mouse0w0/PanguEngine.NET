using PanguEngine.Client.UI.Drawing;
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
    private bool _isTransitioning;
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
        try
        {
            Hud.Initialize(definitions);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// Opens a screen, replacing the current screen when necessary.
    /// </summary>
    /// <remarks>Screen changes from update callbacks must be scheduled through the engine dispatcher.</remarks>
    /// <param name="screen">The screen to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="screen"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the screen cannot be opened or the manager is performing another lifecycle,
    /// update, layout, or drawing operation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Open(UiScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        if (ReferenceEquals(screen, CurrentScreen))
            return;

        screen.VerifyCanOpen();
        var oldScreen = CurrentScreen;
        _isTransitioning = true;
        try
        {
            if (oldScreen is not null)
            {
                oldScreen.VerifyCanClose();
                CurrentScreen = null;
                oldScreen.Close();
            }

            screen.Open();
            CurrentScreen = screen;
        }
        finally
        {
            _isTransitioning = false;
            NotifyCurrentScreenChanged(oldScreen);
        }
    }

    /// <summary>
    /// Closes the current screen.
    /// </summary>
    /// <remarks>Screen changes from update callbacks must be scheduled through the engine dispatcher.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager is performing another lifecycle, update, layout, or drawing operation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Close()
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
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
            NotifyCurrentScreenChanged(screen);
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
        try
        {
            screen?.Close();
            Hud.Close();
            _destroyed = true;
        }
        finally
        {
            _isTransitioning = false;
        }

        NotifyCurrentScreenChanged(screen);
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

    internal void ProcessKeyDown(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        CurrentScreen?.ProcessKeyDown(key, modifiers, isRepeat);
    }

    internal void ProcessKeyUp(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        CurrentScreen?.ProcessKeyUp(key, modifiers, isRepeat);
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

    private void VerifyLifecycleOperation()
    {
        if (_isTransitioning)
            throw new InvalidOperationException("The UI manager is already performing a lifecycle operation.");
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
