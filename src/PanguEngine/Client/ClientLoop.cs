using System.Diagnostics;

namespace PanguEngine.Client;

/// <summary>
/// Runs the client update and render loop.
/// </summary>
public sealed class ClientLoop(Func<bool> shouldContinue, Action pumpEvents, Action update, Action<double> render)
{
    private bool _stopRequested;

    private double _updatesPerSecond = 20;
    private double _updateInterval = 1d / 20;

    /// <summary>The global update events per second.</summary>
    public double UpdatesPerSecond
    {
        get => _updatesPerSecond;
        set
        {
            _updatesPerSecond = value;
            _updateInterval = value > 0 ? 1d / value : 0d;
        }
    }

    /// <summary>Whether the loop is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Runs the client loop until stopped or all windows close.</summary>
    public void Run()
    {
        if (IsRunning)
            throw new InvalidOperationException("Client loop is already running.");

        IsRunning = true;
        _stopRequested = false;
        var stopwatch = Stopwatch.StartNew();
        var previous = stopwatch.Elapsed.TotalSeconds;
        var lag = 0d;

        try
        {
            while (!_stopRequested && shouldContinue())
            {
                pumpEvents();

                var current = stopwatch.Elapsed.TotalSeconds;
                var elapsed = current - previous;
                previous = current;

                var alpha = 0d;
                if (_updateInterval > 0)
                {
                    lag += elapsed;

                    if (lag >= _updateInterval)
                    {
                        update();
                        lag -= _updateInterval;
                    }

                    alpha = Math.Min(lag * _updatesPerSecond, 1d);
                }

                render(alpha);
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
}