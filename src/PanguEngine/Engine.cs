namespace PanguEngine;

public abstract class Engine
{
    public static Engine Instance { get; private set; } = null!;

    public void Run()
    {
        Instance = this;

        OnInit();
        OnRunning();
        OnShutdown();
    }

    protected virtual void OnInit() { }
    protected virtual void OnRunning() { }
    protected virtual void OnShutdown() { }
}
