using System.Diagnostics;
using PanguEngine.Windowing;

namespace PanguEngine.Client;

/// <summary>
/// Runs the client update and render loop.
/// </summary>
public sealed class ClientLoop(WindowManager windowManager)
{
    private readonly Dictionary<Window, double> _lastRenderTimes = [];
    private bool _stopRequested;

    /// <summary>The global update events per second.</summary>
    public double UpdatesPerSecond { get; set; } = 20;

    /// <summary>Whether the loop is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Raised when the global client update is due.</summary>
    public event Action<double>? Update;

    /// <summary>Runs the client loop until stopped or all windows close.</summary>
    public void Run()
    {
        if (IsRunning)
            throw new InvalidOperationException("Client loop is already running.");

        IsRunning = true;
        _stopRequested = false;
        var stopwatch = Stopwatch.StartNew();
        var lastUpdateTime = stopwatch.Elapsed.TotalSeconds;

        try
        {
            while (!_stopRequested && windowManager.Windows.Count > 0)
            {
                windowManager.DoEvents();
                var now = stopwatch.Elapsed.TotalSeconds;

                if (UpdatesPerSecond > 0)
                {
                    var updateInterval = 1d / UpdatesPerSecond;
                    if (now - lastUpdateTime >= updateInterval)
                    {
                        Update?.Invoke(now - lastUpdateTime);
                        lastUpdateTime = now;
                    }
                }

                foreach (var window in windowManager.Windows)
                {
                    RenderWindow(window, now);
                }
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Requests the client loop to stop.</summary>
    public void Stop()
    {
        _stopRequested = true;
    }

    private void RenderWindow(Window window, double now)
    {
        if (window.IsDestroyed || window.IsClosing || !window.IsVisible || window.IsMinimized)
            return;

        _lastRenderTimes.TryGetValue(window, out var lastRenderTime);
        var interval = window.FramesPerSecond <= 0 ? 0 : 1d / window.FramesPerSecond;
        if (interval > 0 && now - lastRenderTime < interval)
            return;

        window.DoRender(now - lastRenderTime);
        _lastRenderTimes[window] = now;
    }
}