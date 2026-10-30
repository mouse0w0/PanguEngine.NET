using System.Diagnostics;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.UI;
using PanguEngine.UI.Drawing;
using CursorShape = PanguEngine.Windowing.CursorShape;

namespace PanguEngine.Client.UI;

/// <summary>
/// Manages the persistent HUD and the current regular UI scene.
/// </summary>
/// <remarks>The engine owns this manager and destroys it after UI dispatch has stopped.</remarks>
public sealed class UIManager
{
    private readonly int _ownerThreadId;
    private readonly Queue<GameUIScene?> _pendingScenes = [];
    private bool _isTransitioning;
    private bool _isDestroying;
    private bool _isInitializingHud;
    private bool _isUpdating;
    private bool _destroyed;

    internal UIManager()
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
        Hud = new HudManager();
        Hud.Open();
    }

    /// <summary>
    /// Gets the persistent client HUD.
    /// </summary>
    public HudManager Hud { get; }

    /// <summary>
    /// Gets the current UI scene, or null when no scene is open.
    /// </summary>
    public GameUIScene? CurrentScene { get; private set; }

    internal event Action<GameUIScene?, GameUIScene?>? CurrentSceneChanged;

    /// <summary>Gets the current scene's pointer shape, or an arrow when no scene is available.</summary>
    internal CursorShape PointerCursor => CurrentScene?.PointerCursor ?? CursorShape.Arrow;

    /// <summary>Forwards pointer shape changes from the current scene.</summary>
    internal event EventHandler? PointerCursorChanged;

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
    /// Opens a scene, replacing the current scene when necessary.
    /// </summary>
    /// <remarks>
    /// Reentrant requests execute in order after the current scene change and its notifications complete.
    /// A lifecycle failure propagates immediately and prevents subsequent scene change notifications.
    /// Deferred failures are reported by the outermost Open or Close call, and remaining requests are discarded.
    /// Scene changes from update callbacks must be scheduled through the engine dispatcher.
    /// </remarks>
    /// <param name="scene">The scene to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene cannot be opened or the manager is initializing the HUD, shutting down,
    /// updating, updating layout, or generating drawing commands.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Open(GameUIScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        RequestSceneChange(scene);
    }

    /// <summary>
    /// Closes the current scene.
    /// </summary>
    /// <remarks>
    /// Reentrant requests execute after the current scene change and its notifications complete.
    /// A deferred close applies to the scene current when the request executes.
    /// A lifecycle failure propagates immediately and prevents subsequent scene change notifications.
    /// Deferred failures are reported by the outermost Open or Close call, and remaining requests are discarded.
    /// Scene changes from update callbacks must be scheduled through the engine dispatcher.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager is initializing the HUD, shutting down, updating, updating layout,
    /// or generating drawing commands.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager is shut down.</exception>
    public void Close() => RequestSceneChange(null);

    private void RequestSceneChange(GameUIScene? scene)
    {
        VerifyAccess();
        VerifyLifecycleOperation(allowQueuedChange: true);
        VerifyNotUpdating();
        _pendingScenes.Enqueue(scene);
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        try
        {
            while (_pendingScenes.TryDequeue(out var nextScene))
            {
                if (ReferenceEquals(nextScene, CurrentScene))
                    continue;

                nextScene?.VerifyCanOpen();
                var oldScene = CurrentScene;
                oldScene?.VerifyCanClose();
                if (oldScene is not null)
                {
                    oldScene.PointerCursorChanged -= OnScenePointerCursorChanged;
                    CurrentScene = null;
                    oldScene.Close();
                }

                nextScene?.Open();
                CurrentScene = nextScene;
                if (nextScene is not null)
                    nextScene.PointerCursorChanged += OnScenePointerCursorChanged;
                NotifyCurrentSceneChanged(oldScene);
            }
        }
        finally
        {
            _pendingScenes.Clear();
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
            CurrentScene?.Update();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>Updates the HUD and current scene and prepares their UI for drawing.</summary>
    /// <remarks>
    /// Runs on the UI owner thread before drawing, completing the HUD before the current scene.
    /// Each scene's game frame callback precedes UI preparation, which processes posted actions,
    /// advances tickers, and updates layout.
    /// </remarks>
    /// <param name="viewportSize">The output viewport size.</param>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    internal void UpdateFrame(Size viewportSize, double alpha)
        => UpdateFrame(viewportSize, alpha, Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()));

    /// <summary>Updates the HUD and current scene and prepares their UI for drawing.</summary>
    /// <remarks>
    /// Runs on the UI owner thread before drawing, completing the HUD before the current scene.
    /// Each scene's game frame callback precedes UI preparation, which processes posted actions,
    /// advances tickers, and updates layout.
    /// </remarks>
    /// <param name="viewportSize">The output viewport size.</param>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    /// <param name="frameTime">The absolute monotonic time shared by tickers in this frame.</param>
    internal void UpdateFrame(Size viewportSize, double alpha, TimeSpan frameTime)
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();

        _isUpdating = true;
        try
        {
            Hud.UpdateFrame(alpha);
            if (Hud.Scene.IsOpen())
                Hud.PrepareFrame(viewportSize, frameTime);
            if (CurrentScene is { } scene)
            {
                scene.UpdateFrame(alpha);
                if (scene.IsOpen())
                    scene.PrepareFrame(viewportSize, frameTime);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    internal void AppendDrawCommands(DrawCommandList commands)
    {
        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        commands.Append(Hud.Scene);
        if (CurrentScene is { } scene)
            commands.Append(scene);
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;

        VerifyAccess();
        VerifyLifecycleOperation();
        VerifyNotUpdating();
        var scene = CurrentScene;
        if (scene is not null)
            scene.PointerCursorChanged -= OnScenePointerCursorChanged;
        CurrentScene = null;
        _isTransitioning = true;
        _isDestroying = true;
        try
        {
            scene?.Close();
            Hud.Close();
            _destroyed = true;
            NotifyCurrentSceneChanged(scene);
        }
        finally
        {
            _isDestroying = false;
            _isTransitioning = false;
        }
    }

    /// <summary>Synchronizes the current scene's pointer position without routing a movement event.</summary>
    /// <param name="position">The pointer position in framebuffer coordinates.</param>
    internal void SynchronizePointerPosition(Point position)
    {
        var scene = CurrentScene;
        if (scene is null)
            return;

        VerifyAccess();
        if (scene.IsOpen())
            scene.SynchronizePointerPosition(position);
    }

    private void OnScenePointerCursorChanged(object? sender, EventArgs args)
    {
        if (ReferenceEquals(sender, CurrentScene))
            PointerCursorChanged?.Invoke(this, EventArgs.Empty);
    }

    internal bool ProcessPointerMoved(Point position)
    {
        VerifyAccess();
        return CurrentScene?.ProcessPointerMoved(position) ?? false;
    }

    internal bool ProcessPointerPressed(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        return CurrentScene?.ProcessPointerPressed(position, button, modifiers) ?? false;
    }

    internal bool ProcessPointerReleased(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        VerifyAccess();
        return CurrentScene?.ProcessPointerReleased(position, button, modifiers) ?? false;
    }

    internal bool ProcessPointerWheel(
        Point position,
        double deltaX,
        double deltaY,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        VerifyAccess();
        return CurrentScene?.ProcessPointerWheel(position, deltaX, deltaY, modifiers) ?? false;
    }

    internal bool ProcessKeyDown(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        return CurrentScene?.ProcessKeyDown(key, modifiers, isRepeat) ?? false;
    }

    internal bool ProcessKeyUp(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        VerifyAccess();
        return CurrentScene?.ProcessKeyUp(key, modifiers, isRepeat) ?? false;
    }

    internal void ProcessTextInput(string text)
    {
        VerifyAccess();
        CurrentScene?.ProcessTextInput(text);
    }

    internal void ProcessFocusChanged(bool focused)
    {
        VerifyAccess();
        CurrentScene?.ProcessFocusChanged(focused);
    }

    private void NotifyCurrentSceneChanged(GameUIScene? oldScene)
    {
        var newScene = CurrentScene;
        if (!ReferenceEquals(oldScene, newScene))
            CurrentSceneChanged?.Invoke(oldScene, newScene);
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
            throw new InvalidOperationException("The UI manager is already changing scenes.");
        if (Hud.Scene.IsUpdatingLayout || Hud.Scene.IsDrawing)
        {
            throw new InvalidOperationException(
                "The UI manager cannot change scenes while the HUD is updating or drawing.");
        }

        if (CurrentScene?.IsUpdatingLayout == true)
            throw new InvalidOperationException("The UI manager cannot change scenes during layout.");
        if (CurrentScene?.IsDrawing == true)
        {
            throw new InvalidOperationException(
                "The UI manager cannot change scenes while drawing commands are generated.");
        }
    }

    private void VerifyNotUpdating()
    {
        if (_isUpdating || Hud.Scene.IsUpdating || CurrentScene?.IsUpdating == true)
            throw new InvalidOperationException("The UI manager cannot perform this operation during an update.");
    }
}
