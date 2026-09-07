using PanguEngine.Windowing;
using EngineWindow = PanguEngine.Windowing.Window;

namespace PanguEngine.Tests.Windowing;

internal sealed class TestWindowManager : WindowManager
{
    private readonly Func<WindowOptions, EngineWindow> _createWindow;
    private readonly Action? _pumpEvents;

    internal TestWindowManager(
        EngineWindow primaryWindow,
        Func<WindowOptions, EngineWindow> createWindow)
        : this(primaryWindow, createWindow, static () => 0, null)
    {
    }

    internal TestWindowManager(
        EngineWindow primaryWindow,
        Func<WindowOptions, EngineWindow> createWindow,
        Func<double> getTime)
        : this(primaryWindow, createWindow, getTime, null)
    {
    }

    internal TestWindowManager(
        EngineWindow primaryWindow,
        Func<WindowOptions, EngineWindow> createWindow,
        Func<double> getTime,
        Action? pumpEvents)
        : base(getTime)
    {
        if (!primaryWindow.IsPrimary)
            throw new InvalidOperationException("Window is not a primary window.");

        _createWindow = createWindow;
        _pumpEvents = pumpEvents;
        AddWindow(primaryWindow);
    }

    protected override EngineWindow CreateWindowCore(WindowOptions options) => _createWindow(options);

    protected override void PumpEventsCore() => _pumpEvents?.Invoke();

    protected override void DestroyCore()
    {
    }
}
