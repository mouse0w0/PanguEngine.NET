using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Client.Input;

/// <summary>
/// Routes client window input through UI and logical input contexts.
/// </summary>
public sealed class InputManager
{
    private readonly Window _window;
    private readonly UiManager _uiManager;
    private readonly InputRouter _router;
    private IDisposable? _screenActivation;
    private Vector2D<float> _pointerPosition;
    private bool _hasPointerPosition;
    private Vector2D<float> _mouseBaseline;
    private bool _hasMouseBaseline;
    private bool _restorePointerCapture;
    private int _suspendCount;
    private int _uiTopologyVersion;
    private bool _started;
    private bool _destroying;
    private bool _destroyed;

    /// <summary>
    /// Creates a client input manager without subscribing to window events.
    /// </summary>
    /// <param name="window">The client window.</param>
    /// <param name="uiManager">The client UI manager.</param>
    /// <param name="bindings">The runtime input bindings.</param>
    public InputManager(
        Window window,
        UiManager uiManager,
        InputBindingMap bindings)
    {
        _window = window;
        _uiManager = uiManager;
        Bindings = bindings;
        _router = new InputRouter(bindings);
    }

    /// <summary>The mutable runtime input bindings.</summary>
    public InputBindingMap Bindings { get; }

    /// <summary>Whether the pointer is captured for relative movement.</summary>
    public bool IsPointerCaptured { get; private set; }

    internal event Action? StateInvalidated
    {
        add => _router.StateInvalidated += value;
        remove => _router.StateInvalidated -= value;
    }

    /// <summary>
    /// Starts routing window and UI events.
    /// </summary>
    public void Start()
    {
        VerifyNotDestroyed();
        if (_started)
            throw new InvalidOperationException("The client input manager has already started.");

        _router.FreezeHandlers();
        _window.KeyDown += OnKeyDown;
        _window.KeyUp += OnKeyUp;
        _window.TextInput += OnTextInput;
        _window.MouseMove += OnMouseMove;
        _window.MouseDown += OnMouseDown;
        _window.MouseUp += OnMouseUp;
        _window.Scroll += OnScroll;
        _window.FocusChanged += OnFocusChanged;
        _uiManager.CurrentScreenChanged += OnCurrentScreenChanged;
        _started = true;
        OnCurrentScreenChanged(null, _uiManager.CurrentScreen);
    }

    /// <summary>
    /// Activates an input context until the returned token is disposed.
    /// </summary>
    public IDisposable ActivateContext(InputContext context)
    {
        VerifyNotDestroyed();
        var routerActivation = _router.ActivateContext(context);
        if (context.PointerCapturePolicy == PointerCapturePolicy.Suspend)
            BeginPointerSuspend();

        return new CallbackToken(() =>
        {
            try
            {
                routerActivation.Dispose();
            }
            finally
            {
                if (context.PointerCapturePolicy == PointerCapturePolicy.Suspend)
                    EndPointerSuspend();
            }
        });
    }

    /// <summary>
    /// Registers an input action handler before input routing starts.
    /// </summary>
    public void RegisterHandler(
        InputAction action,
        Func<InputActionEvent, InputHandling> handler,
        int priority = 0)
    {
        VerifyNotDestroyed();
        _router.RegisterHandler(action, handler, priority);
    }

    /// <summary>Gets the current value of a logical input action.</summary>
    public InputActionValue GetValue(InputAction action)
    {
        VerifyNotDestroyed();
        return _router.GetValue(action);
    }

    /// <summary>
    /// Attempts to capture the pointer for relative movement.
    /// </summary>
    /// <returns>Whether the pointer is captured.</returns>
    public bool TryCapturePointer()
    {
        VerifyNotDestroyed();
        if (_destroying || _suspendCount > 0 || !_window.IsFocused)
            return false;
        if (IsPointerCaptured)
            return true;

        IsPointerCaptured = true;
        _window.CursorState = CursorState.Disabled;
        if (_hasPointerPosition)
        {
            _mouseBaseline = _pointerPosition;
            _hasMouseBaseline = true;
        }
        else
        {
            _hasMouseBaseline = false;
        }

        return true;
    }

    /// <summary>
    /// Releases the captured pointer and cancels pending suspend restoration.
    /// </summary>
    public void ReleasePointer()
    {
        VerifyNotDestroyed();
        _restorePointerCapture = false;
        ReleasePointerCore();
    }

    /// <summary>
    /// Stops input routing and restores the normal pointer state.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed || _destroying)
            return;

        _destroying = true;
        try
        {
            _restorePointerCapture = false;
            _suspendCount = 0;
            _router.Destroy();
        }
        finally
        {
            _screenActivation?.Dispose();
            _screenActivation = null;
            if (_started)
            {
                _window.KeyDown -= OnKeyDown;
                _window.KeyUp -= OnKeyUp;
                _window.TextInput -= OnTextInput;
                _window.MouseMove -= OnMouseMove;
                _window.MouseDown -= OnMouseDown;
                _window.MouseUp -= OnMouseUp;
                _window.Scroll -= OnScroll;
                _window.FocusChanged -= OnFocusChanged;
                _uiManager.CurrentScreenChanged -= OnCurrentScreenChanged;
            }
            ReleasePointerCore();
            _started = false;
            _destroying = false;
            _destroyed = true;
        }
    }

    private void OnKeyDown(Window window, KeyEventArgs args)
    {
        var source = InputSource.FromKey(args.Key);
        _router.BeginInputEvent();
        _router.RecordPress(source, args.Modifiers);
        var topologyVersion = _uiTopologyVersion;
        var handled = _uiManager.CurrentScreen is not null
                      && _uiManager.ProcessKeyDown(args.Key, args.Modifiers, args.IsRepeat);
        _router.RoutePress(source, args.IsRepeat || handled || topologyVersion != _uiTopologyVersion);
        _router.EndInputEvent();
    }

    private void OnKeyUp(Window window, KeyEventArgs args)
    {
        var source = InputSource.FromKey(args.Key);
        _router.BeginInputEvent();
        _router.RecordRelease(source, args.Modifiers);
        if (_uiManager.CurrentScreen is not null)
            _ = _uiManager.ProcessKeyUp(args.Key, args.Modifiers, args.IsRepeat);
        _router.RouteRelease(source);
        _router.EndInputEvent();
    }

    private void OnTextInput(Window window, string text)
    {
        if (_uiManager.CurrentScreen is not null)
            _uiManager.ProcessTextInput(text);
    }

    private void OnMouseMove(Window window, MouseMoveEventArgs args)
    {
        _router.BeginInputEvent();
        var position = new Vector2D<float>(args.X, args.Y);
        _pointerPosition = position;
        _hasPointerPosition = true;
        var delta = _hasMouseBaseline ? position - _mouseBaseline : Vector2D<float>.Zero;
        _mouseBaseline = position;
        _hasMouseBaseline = true;
        var topologyVersion = _uiTopologyVersion;
        var handled = TryRoutePointer(
            args.X,
            args.Y,
            static (manager, point) => manager.ProcessPointerMoved(point));
        _router.RouteSample(
            InputSource.MouseMove,
            new Vector2D<double>(delta.X, delta.Y),
            _window.KeyModifiers,
            handled || topologyVersion != _uiTopologyVersion);
        _router.EndInputEvent();
    }

    private void OnMouseDown(Window window, MouseClickEventArgs args)
    {
        var source = InputSource.FromMouseButton(args.Button);
        var modifiers = _window.KeyModifiers;
        _router.BeginInputEvent();
        _pointerPosition = new Vector2D<float>(args.X, args.Y);
        _hasPointerPosition = true;
        _router.RecordPress(source, modifiers);
        var topologyVersion = _uiTopologyVersion;
        var handled = TryRoutePointer(
            args.X,
            args.Y,
            (manager, point) => manager.ProcessPointerPressed(point, args.Button, modifiers));
        _router.RoutePress(source, handled || topologyVersion != _uiTopologyVersion);
        _router.EndInputEvent();
    }

    private void OnMouseUp(Window window, MouseClickEventArgs args)
    {
        var source = InputSource.FromMouseButton(args.Button);
        var modifiers = _window.KeyModifiers;
        _router.BeginInputEvent();
        _pointerPosition = new Vector2D<float>(args.X, args.Y);
        _hasPointerPosition = true;
        _router.RecordRelease(source, modifiers);
        _ = TryRoutePointer(
            args.X,
            args.Y,
            (manager, point) => manager.ProcessPointerReleased(point, args.Button, modifiers));
        _router.RouteRelease(source);
        _router.EndInputEvent();
    }

    private void OnScroll(Window window, ScrollEventArgs args)
    {
        _router.BeginInputEvent();
        var topologyVersion = _uiTopologyVersion;
        var mousePosition = _window.MousePosition;
        var handled = TryRoutePointer(
            mousePosition.X,
            mousePosition.Y,
            (manager, point) => manager.ProcessPointerWheel(point, args.X, args.Y));
        _router.RouteSample(
            InputSource.MouseWheel,
            new Vector2D<double>(args.X, args.Y),
            _window.KeyModifiers,
            handled || topologyVersion != _uiTopologyVersion);
        _router.EndInputEvent();
    }

    private void OnFocusChanged(Window window, bool focused)
    {
        if (!focused)
        {
            _router.Reset();
            _restorePointerCapture = false;
            ReleasePointerCore();
        }

        if (_uiManager.CurrentScreen is not null)
            _uiManager.ProcessFocusChanged(focused);
    }

    private void OnCurrentScreenChanged(UiScreen? oldScreen, UiScreen? newScreen)
    {
        _router.BeginInputEvent();
        _router.NotifyUiTopologyChanged(oldScreen?.InputContext);
        var topologyVersion = ++_uiTopologyVersion;
        var oldActivation = _screenActivation;
        _screenActivation = null;
        oldActivation?.Dispose();

        if (topologyVersion == _uiTopologyVersion && newScreen is not null)
        {
            var activation = ActivateContext(newScreen.InputContext);
            if (topologyVersion == _uiTopologyVersion)
                _screenActivation = activation;
            else
                activation.Dispose();
        }
        _router.EndInputEvent();
    }

    private bool TryRoutePointer(
        float x,
        float y,
        Func<UiManager, Point, bool> route)
    {
        if (_uiManager.CurrentScreen is null
            || !TryGetFramebufferPosition(x, y, out var position))
        {
            return false;
        }

        return route(_uiManager, position);
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

    private void BeginPointerSuspend()
    {
        if (_suspendCount++ != 0)
            return;
        _restorePointerCapture = IsPointerCaptured;
        ReleasePointerCore();
    }

    private void EndPointerSuspend()
    {
        if (_destroyed || _destroying || _suspendCount == 0)
            return;
        if (--_suspendCount != 0)
            return;

        var restore = _restorePointerCapture;
        _restorePointerCapture = false;
        if (restore && _window.IsFocused)
            _ = TryCapturePointer();
    }

    private void ReleasePointerCore()
    {
        IsPointerCaptured = false;
        _hasMouseBaseline = false;
        _window.CursorState = CursorState.Normal;
    }

    private void VerifyNotDestroyed()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
    }

    private sealed class CallbackToken(Action callback) : IDisposable
    {
        private Action? _callback = callback;

        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

}
