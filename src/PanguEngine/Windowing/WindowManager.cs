using System.Diagnostics;

namespace PanguEngine.Windowing;

/// <summary>
/// Creates and manages engine windows.
/// </summary>
public sealed class WindowManager
{
    private readonly Func<WindowOptions, Window> _createWindow;
    private readonly Func<double> _getTime;
    private readonly List<Window> _windows = [];
    private readonly List<Window> _dueWindows = [];
    private readonly List<Window> _pendingDestroy = [];
    private readonly Dictionary<Window, double> _lastRenderTimes = [];
    private double _renderTime;
    private bool _destroyed;

    /// <summary>
    /// Creates a window manager for a primary window.
    /// </summary>
    /// <param name="primaryWindow">The primary window created by the client startup path.</param>
    /// <param name="createWindow">The non-primary window factory.</param>
    public WindowManager(Window primaryWindow, Func<WindowOptions, Window> createWindow)
        : this(primaryWindow, createWindow, GetCurrentTime)
    {
    }

    internal WindowManager(
        Window primaryWindow,
        Func<WindowOptions, Window> createWindow,
        Func<double> getTime)
    {
        ArgumentNullException.ThrowIfNull(primaryWindow);
        if (!primaryWindow.IsPrimary)
            throw new InvalidOperationException("Window is not a primary window.");

        _createWindow = createWindow ?? throw new ArgumentNullException(nameof(createWindow));
        _getTime = getTime ?? throw new ArgumentNullException(nameof(getTime));
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

    /// <summary>Captures the windows due for a frame and performs their pre-render events.</summary>
    /// <param name="alpha">The interpolation factor since the last fixed update.</param>
    internal void PreRenderWindows(double alpha)
    {
        _dueWindows.Clear();
        _renderTime = _getTime();
        foreach (var window in _windows)
        {
            if (window.IsDestroyed || window.IsClosing || !window.IsVisible ||
                window.WindowState == WindowState.Minimized)
                continue;

            _lastRenderTimes.TryGetValue(window, out var lastRenderTime);
            var interval = window.FramesPerSecond <= 0 ? 0 : 1d / window.FramesPerSecond;
            if (interval > 0 && _renderTime - lastRenderTime < interval)
                continue;

            _dueWindows.Add(window);
        }

        foreach (var window in _dueWindows)
            window.DoPreRender(alpha);
    }

    /// <summary>Renders windows from the most recent pre-render snapshot that remain renderable.</summary>
    /// <param name="alpha">The interpolation factor since the last fixed update.</param>
    internal void RenderWindows(double alpha)
    {
        foreach (var window in _dueWindows)
        {
            if (window.IsDestroyed || window.IsClosing || !window.IsVisible ||
                window.WindowState == WindowState.Minimized)
                continue;

            window.DoRender(alpha);
            _lastRenderTimes[window] = _renderTime;
        }
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
        _dueWindows.Clear();
        _pendingDestroy.Clear();
        _lastRenderTimes.Clear();
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
            _lastRenderTimes.Remove(window);
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

    private static double GetCurrentTime() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
