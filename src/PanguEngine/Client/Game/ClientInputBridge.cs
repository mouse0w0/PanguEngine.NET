using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.Windowing;

namespace PanguEngine.Client.Game;

internal sealed class ClientInputBridge
{
    private readonly Window _window;
    private readonly UiManager _uiManager;
    private readonly ClientInputState _input;
    private readonly Func<bool> _tryHandleEscape;
    private bool _restoreMouseCapture;
    private bool _destroyed;

    internal ClientInputBridge(
        Window window,
        UiManager uiManager,
        ClientInputState input,
        Func<bool> tryHandleEscape)
    {
        _window = window;
        _uiManager = uiManager;
        _input = input;
        _tryHandleEscape = tryHandleEscape;
        window.KeyDown += OnKeyDown;
        window.KeyUp += OnKeyUp;
        window.MouseMove += OnMouseMove;
        window.MouseDown += OnMouseDown;
        window.MouseUp += OnMouseUp;
        window.Scroll += OnScroll;
        window.FocusChanged += OnFocusChanged;
        uiManager.CurrentScreenChanged += OnCurrentScreenChanged;
        OnCurrentScreenChanged(null, uiManager.CurrentScreen);
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;

        _window.KeyDown -= OnKeyDown;
        _window.KeyUp -= OnKeyUp;
        _window.MouseMove -= OnMouseMove;
        _window.MouseDown -= OnMouseDown;
        _window.MouseUp -= OnMouseUp;
        _window.Scroll -= OnScroll;
        _window.FocusChanged -= OnFocusChanged;
        _uiManager.CurrentScreenChanged -= OnCurrentScreenChanged;
        _restoreMouseCapture = false;
    }

    private void OnKeyDown(Window window, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && _tryHandleEscape())
            return;

        var screen = _uiManager.CurrentScreen;
        if (screen is not null)
        {
            _uiManager.ProcessKeyDown(args.Key, args.Modifiers);
            return;
        }

        _input.HandleKeyDown(args);
    }

    private void OnKeyUp(Window window, KeyEventArgs args)
    {
        var screen = _uiManager.CurrentScreen;
        if (screen is not null)
        {
            _uiManager.ProcessKeyUp(args.Key, args.Modifiers);
            return;
        }

        _input.HandleKeyUp(args);
    }

    private void OnMouseMove(Window window, MouseMoveEventArgs args)
    {
        var screen = _uiManager.CurrentScreen;
        if (screen is null)
        {
            _input.HandleMouseMove(args);
            return;
        }

        if (TryGetFramebufferPosition(args.X, args.Y, out var position))
            _uiManager.ProcessPointerMoved(position);
    }

    private void OnMouseDown(Window window, MouseClickEventArgs args)
    {
        var screen = _uiManager.CurrentScreen;
        if (screen is null)
        {
            _input.HandleMouseDown(args);
            return;
        }

        if (!TryGetFramebufferPosition(args.X, args.Y, out var position))
            return;
        var modifiers = _window.KeyModifiers;
        _uiManager.ProcessPointerPressed(position, args.Button, modifiers);
    }

    private void OnMouseUp(Window window, MouseClickEventArgs args)
    {
        var screen = _uiManager.CurrentScreen;
        if (screen is null || !TryGetFramebufferPosition(args.X, args.Y, out var position))
            return;

        var modifiers = _window.KeyModifiers;
        _uiManager.ProcessPointerReleased(position, args.Button, modifiers);
    }

    private void OnScroll(Window window, ScrollEventArgs args)
    {
        var screen = _uiManager.CurrentScreen;
        if (screen is null)
            return;

        var mousePosition = _window.MousePosition;
        if (TryGetFramebufferPosition(mousePosition.X, mousePosition.Y, out var position))
            _uiManager.ProcessPointerWheel(position, args.X, args.Y);
    }

    private void OnFocusChanged(Window window, bool focused)
    {
        var screen = _uiManager.CurrentScreen;
        _input.HandleFocusChanged(focused);
        if (screen is not null)
            _uiManager.ProcessFocusChanged(focused);
    }

    private void OnCurrentScreenChanged(UiScreen? oldScreen, UiScreen? newScreen)
    {
        if (oldScreen is null && newScreen is not null)
        {
            _restoreMouseCapture = _input.SuspendForUi();
            return;
        }

        if (oldScreen is null || newScreen is not null)
            return;

        var restoreMouseCapture = _restoreMouseCapture;
        _restoreMouseCapture = false;
        if (restoreMouseCapture && _window.IsFocused)
            _input.CaptureMouse();
    }

    private bool TryGetFramebufferPosition(float x, float y, out Point position)
    {
        var windowSize = _window.Size;
        var framebufferSize = _window.FramebufferSize;
        if (windowSize.X <= 0 || windowSize.Y <= 0
                              || framebufferSize.X <= 0 || framebufferSize.Y <= 0)
        {
            position = default;
            return false;
        }

        position = new Point(
            (double)x * framebufferSize.X / windowSize.X,
            (double)y * framebufferSize.Y / windowSize.Y);
        return true;
    }
}
