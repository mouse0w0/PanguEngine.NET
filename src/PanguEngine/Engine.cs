using Microsoft.Extensions.Logging;

namespace PanguEngine;

public abstract class Engine
{
    public static Engine Instance { get; private set; } = null!;
    public static ILogger Logger { get; private set; } = null!;

    public void Run()
    {
        Instance = this;

        Log.Initialize();
        Logger = Log.CreateLogger("Engine");

        OnInit();
        OnRunning();
        OnShutdown();

        Log.Shutdown();
    }

    protected virtual void OnInit()
    {
    }

    protected virtual void OnRunning()
    {
    }

    protected virtual void OnShutdown()
    {
    }
}