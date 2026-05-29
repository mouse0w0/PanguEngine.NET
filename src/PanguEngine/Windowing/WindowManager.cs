namespace PanguEngine.Windowing;

/// <summary>
/// Creates and manages engine windows.
/// </summary>
public sealed class WindowManager
{
    private readonly Func<WindowOptions, Window> _createWindow;
    private readonly List<Window> _windows = [];
    private readonly List<Window> _pendingDestroy = [];
    private bool _destroyed;

    /// <summary>
    /// Creates a window manager for a primary window.
    /// </summary>
    /// <param name="primaryWindow">The primary window created by the client startup path.</param>
    /// <param name="createWindow">The non-primary window factory.</param>
    public WindowManager(Window primaryWindow, Func<WindowOptions, Window> createWindow)
    {
        ArgumentNullException.ThrowIfNull(primaryWindow);
        if (!primaryWindow.IsPrimary)
            throw new InvalidOperationException("Window is not a primary window.");

        _createWindow = createWindow ?? throw new ArgumentNullException(nameof(createWindow));
        PrimaryWindow = primaryWindow;
        AddWindow(primaryWindow);
    }

    /// <summary>The active windows.</summary>
    public IReadOnlyList<Window> Windows => _windows;

    /// <summary>The current primary window.</summary>
    public Window? PrimaryWindow { get; private set; }

    /// <summary>Creates a non-primary window.</summary>
    public Window CreateWindow(WindowOptions options)
    {
        ThrowIfDestroyed();
        var window = _createWindow(options);
        if (window.IsPrimary)
            throw new InvalidOperationException("Window factory created a primary window.");

        AddWindow(window);
        return window;
    }

    /// <summary>Processes platform events for all windows.</summary>
    public void DoEvents()
    {
        foreach (var window in _windows)
        {
            if (!window.IsDestroyed)
                window.DoEvents();
        }

        DestroyClosedWindows();
    }

    /// <summary>Requests all windows to close.</summary>
    public void CloseAll()
    {
        foreach (var window in _windows)
        {
            if (!window.IsDestroyed)
                window.CloseWindow();
        }
    }

    /// <summary>Destroys all managed window resources.</summary>
    internal void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        foreach (var window in _windows)
        {
            DestroyWindow(window);
        }

        _windows.Clear();
        _pendingDestroy.Clear();
        PrimaryWindow = null;
    }

    private void AddWindow(Window window)
    {
        _windows.Add(window);
        window.Close += OnWindowClosed;
    }

    private void OnWindowClosed(Window window)
    {
        var managedWindow = _windows.FirstOrDefault(candidate => ReferenceEquals(candidate, window));
        if (managedWindow is null) return;

        if (!_pendingDestroy.Contains(managedWindow))
            _pendingDestroy.Add(managedWindow);
    }

    private void DestroyClosedWindows()
    {
        foreach (var window in _pendingDestroy)
        {
            DestroyWindow(window);
            _windows.Remove(window);
            if (ReferenceEquals(PrimaryWindow, window))
                PrimaryWindow = null;
        }

        _pendingDestroy.Clear();
    }

    private static void DestroyWindow(Window window)
    {
        if (!window.IsDestroyed)
            window.Destroy();
    }

    private void ThrowIfDestroyed()
    {
        if (_destroyed)
            throw new InvalidOperationException("Window manager is destroyed.");
    }
}