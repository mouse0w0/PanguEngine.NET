using System.Diagnostics;

namespace PanguEngine.Windowing;

/// <summary>
/// Creates and manages engine windows.
/// </summary>
public abstract class WindowManager
{
    private sealed class WindowEntry(Window window)
    {
        internal WeakReference<Window> Reference { get; } = new(window);
        internal double LastRenderTime { get; set; }
    }

    private readonly Func<double> _getTime;
    private readonly List<WindowEntry> _windows = [];
    private readonly List<Window> _visibleWindows = [];
    private readonly List<Window> _dueWindows = [];
    private double _renderTime;
    private bool _destroyed;

    protected WindowManager()
        : this(GetCurrentTime)
    {
    }

    protected WindowManager(Func<double> getTime)
    {
        _getTime = getTime ?? throw new ArgumentNullException(nameof(getTime));
        VisibleWindows = _visibleWindows.AsReadOnly();
    }

    /// <summary>The currently visible windows held by the manager.</summary>
    public IReadOnlyList<Window> VisibleWindows { get; }

    /// <summary>Creates a non-primary window.</summary>
    public Window CreateWindow(WindowOptions options)
    {
        ThrowIfDestroyed();
        var window = CreateWindowCore(options);
        if (window.IsPrimary)
            throw new InvalidOperationException("Window factory created a primary window.");

        AddWindow(window);
        return window;
    }

    protected abstract Window CreateWindowCore(WindowOptions options);

    /// <summary>Processes platform events for all windows.</summary>
    public void DoEvents()
    {
        PumpEventsCore();
        PruneDeadWindows();
    }

    protected abstract void PumpEventsCore();

    /// <summary>Captures the windows due for a frame and performs their pre-render events.</summary>
    /// <param name="alpha">The interpolation factor since the last fixed update.</param>
    internal void PreRenderWindows(double alpha)
    {
        PruneDeadWindows();
        _dueWindows.Clear();
        _renderTime = _getTime();
        foreach (var entry in _windows)
        {
            if (!entry.Reference.TryGetTarget(out var window) ||
                window.IsDestroyed || !window.IsVisible ||
                window.WindowState == WindowState.Minimized)
                continue;

            var interval = window.FramesPerSecond <= 0 ? 0 : 1d / window.FramesPerSecond;
            if (interval > 0 && _renderTime - entry.LastRenderTime < interval)
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
            if (window.IsDestroyed || !window.IsVisible ||
                window.WindowState == WindowState.Minimized)
                continue;

            window.DoRender(alpha);
            FindEntry(window)!.LastRenderTime = _renderTime;
        }
    }

    /// <summary>Hides all windows.</summary>
    public void HideAll()
    {
        foreach (var window in GetWindowsSnapshot())
        {
            if (!window.IsDestroyed)
                window.Hide();
        }
    }

    /// <summary>Stops managing all windows.</summary>
    internal void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        foreach (var window in GetWindowsSnapshot())
            window.VisibilityChanged -= OnWindowVisibilityChanged;

        _windows.Clear();
        _visibleWindows.Clear();
        _dueWindows.Clear();
        DestroyCore();
    }

    protected abstract void DestroyCore();

    protected void AddWindow(Window window)
    {
        _windows.Add(new WindowEntry(window));
        window.VisibilityChanged += OnWindowVisibilityChanged;
        if (window.IsVisible)
            AddVisibleWindow(window);
    }

    private void OnWindowVisibilityChanged(Window window, bool isVisible)
    {
        if (isVisible)
            AddVisibleWindow(window);
        else
            RemoveVisibleWindow(window);
    }

    private void PruneDeadWindows()
    {
        foreach (var entry in _windows.ToArray())
        {
            if (!entry.Reference.TryGetTarget(out var window))
            {
                _windows.Remove(entry);
                continue;
            }

            if (!window.IsDestroyed)
                continue;

            RemoveWindow(window);
        }
    }

    private void RemoveWindow(Window window)
    {
        RemoveVisibleWindow(window);
        window.VisibilityChanged -= OnWindowVisibilityChanged;
        for (var i = _windows.Count - 1; i >= 0; i--)
        {
            if (_windows[i].Reference.TryGetTarget(out var candidate) && ReferenceEquals(candidate, window))
                _windows.RemoveAt(i);
        }
    }

    private void AddVisibleWindow(Window window)
    {
        if (!_visibleWindows.Contains(window))
            _visibleWindows.Add(window);
    }

    private void RemoveVisibleWindow(Window window) => _visibleWindows.Remove(window);

    private List<Window> GetWindowsSnapshot()
    {
        var windows = new List<Window>(_windows.Count);
        foreach (var entry in _windows)
        {
            if (entry.Reference.TryGetTarget(out var window) && !window.IsDestroyed)
                windows.Add(window);
        }

        return windows;
    }

    private WindowEntry? FindEntry(Window window)
    {
        foreach (var entry in _windows)
        {
            if (entry.Reference.TryGetTarget(out var candidate) && ReferenceEquals(candidate, window))
                return entry;
        }

        return null;
    }

    private void ThrowIfDestroyed()
    {
        if (_destroyed)
            throw new InvalidOperationException("Window manager is destroyed.");
    }

    private static double GetCurrentTime() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}