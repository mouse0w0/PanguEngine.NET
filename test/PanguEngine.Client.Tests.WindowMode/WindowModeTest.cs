using PanguEngine.Graphics;
using PanguEngine.Input;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Client.Tests.WindowMode;

internal enum DisplayMode
{
    Windowed,
    Fullscreen,
    BorderlessFullscreen
}

internal sealed class WindowModeScene : IClientTestScene
{
    private DisplayMode _mode = DisplayMode.Windowed;
    private Vector2D<int> _savedSize = new(800, 600);
    private Vector2D<int> _savedPosition = new(50, 50);
    private WindowBorder _savedBorder = WindowBorder.Resizable;
    private Presenter _presenter = null!;

    /// <inheritdoc/>
    public string Name => "WindowMode Test (F11 to cycle)";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        window.KeyDown += OnKeyDown;
        window.Render += (_, _) => DrawFrame();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
    }

    private void DrawFrame()
    {
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription(
                frame.Width,
                frame.Height,
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.02f, 0.04f, 0.08f, 1)),
                ]));
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private void OnKeyDown(Window window, KeyEventArgs e)
    {
        if (e.Key != Key.F11 || e.Action != KeyAction.Press)
            return;

        var nextMode = (DisplayMode)(((int)_mode + 1) % 3);

        switch (_mode)
        {
            case DisplayMode.Windowed:
                _savedSize = window.Size;
                _savedPosition = window.Position;
                _savedBorder = window.WindowBorder;
                break;
            case DisplayMode.Fullscreen:
                break;
            case DisplayMode.BorderlessFullscreen:
                break;
        }

        switch (nextMode)
        {
            case DisplayMode.Windowed:
                window.IsFullscreen = false;
                window.WindowBorder = _savedBorder;
                window.Size = _savedSize;
                window.Position = _savedPosition;
                break;
            case DisplayMode.Fullscreen:
                window.IsFullscreen = true;
                break;
            case DisplayMode.BorderlessFullscreen:
                var monitorSize = window.MonitorSize;
                window.IsFullscreen = false;
                window.WindowBorder = WindowBorder.Hidden;
                window.Size = monitorSize;
                window.Position = new Vector2D<int>(0, 0);
                break;
        }

        _mode = nextMode;
    }
}

internal static class WindowModeTest
{
    private static void Main()
    {
        ClientTestApp.Run(new WindowModeScene());
    }
}