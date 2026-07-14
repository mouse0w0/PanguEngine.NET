using PanguEngine.Input;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Collects input state for a local client game.
/// </summary>
internal sealed class ClientInputState
{
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly Window? _window;
    private readonly Action<CursorState> _setCursorState;
    private Vector2D<float> _mouseBaseline;
    private bool _hasMouseBaseline;
    private bool _leftClickRequested;
    private bool _rightClickRequested;
    private bool _isDestroyed;

    internal ClientInputState(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _setCursorState = state => window.CursorState = state;
        window.KeyDown += OnKeyDown;
        window.KeyUp += OnKeyUp;
        window.MouseMove += OnMouseMove;
        window.MouseDown += OnMouseDown;
        window.FocusChanged += OnFocusChanged;
    }

    internal ClientInputState(Action<CursorState> setCursorState)
    {
        _setCursorState = setCursorState ?? throw new ArgumentNullException(nameof(setCursorState));
    }

    /// <summary>Raised when captured mouse movement produces a relative delta.</summary>
    internal event Action<Vector2D<float>>? MouseDelta;

    /// <summary>Whether the mouse is currently captured for camera movement.</summary>
    internal bool IsMouseCaptured { get; private set; }

    /// <summary>
    /// Gets whether a key is currently pressed.
    /// </summary>
    /// <param name="key">The key to query.</param>
    /// <returns><see langword="true" /> when the key is pressed.</returns>
    internal bool IsKeyDown(Key key) => _pressedKeys.Contains(key);

    /// <summary>
    /// Consumes the pending left-click request.
    /// </summary>
    /// <returns><see langword="true" /> once for each observed left click while captured.</returns>
    internal bool ConsumeLeftClickRequest()
    {
        var requested = _leftClickRequested;
        _leftClickRequested = false;
        return requested;
    }

    /// <summary>
    /// Consumes the pending right-click request.
    /// </summary>
    /// <returns><see langword="true" /> once for each observed right click.</returns>
    internal bool ConsumeRightClickRequest()
    {
        var requested = _rightClickRequested;
        _rightClickRequested = false;
        return requested;
    }

    /// <summary>
    /// Captures the mouse without an initial position baseline.
    /// </summary>
    internal void CaptureMouse()
    {
        if (IsMouseCaptured)
            return;

        IsMouseCaptured = true;
        _hasMouseBaseline = false;
        _setCursorState(CursorState.Disabled);
    }

    internal void HandleKeyDown(KeyEventArgs args)
    {
        _pressedKeys.Add(args.Key);
        if (args.Key == Key.Escape)
        {
            ReleaseMouse();
            return;
        }
    }

    internal void HandleKeyUp(KeyEventArgs args)
    {
        _pressedKeys.Remove(args.Key);
    }

    internal void HandleMouseMove(MouseMoveEventArgs args)
    {
        if (!IsMouseCaptured)
            return;

        var position = new Vector2D<float>(args.X, args.Y);
        if (!_hasMouseBaseline)
        {
            _mouseBaseline = position;
            _hasMouseBaseline = true;
            return;
        }

        var delta = position - _mouseBaseline;
        _mouseBaseline = position;
        if (delta.X != 0 || delta.Y != 0)
            MouseDelta?.Invoke(delta);
    }

    internal void HandleMouseDown(MouseClickEventArgs args)
    {
        if (args.Button == MouseButton.Right)
        {
            _rightClickRequested = true;
            return;
        }

        if (args.Button != MouseButton.Left)
            return;

        if (IsMouseCaptured)
        {
            _leftClickRequested = true;
            return;
        }

        CaptureMouse();
        _mouseBaseline = new Vector2D<float>(args.X, args.Y);
        _hasMouseBaseline = true;
    }

    internal void HandleFocusChanged(bool focused)
    {
        if (focused)
            return;

        _pressedKeys.Clear();
        _leftClickRequested = false;
        _rightClickRequested = false;
        ReleaseMouse();
    }

    /// <summary>
    /// Stops collecting input and restores the normal cursor state.
    /// </summary>
    internal void Destroy()
    {
        if (_isDestroyed)
            return;

        _isDestroyed = true;
        if (_window is not null)
        {
            _window.KeyDown -= OnKeyDown;
            _window.KeyUp -= OnKeyUp;
            _window.MouseMove -= OnMouseMove;
            _window.MouseDown -= OnMouseDown;
            _window.FocusChanged -= OnFocusChanged;
        }

        _pressedKeys.Clear();
        _leftClickRequested = false;
        _rightClickRequested = false;
        var wasMouseCaptured = IsMouseCaptured;
        ReleaseMouse();
        if (!wasMouseCaptured)
            _setCursorState(CursorState.Normal);
        MouseDelta = null;
    }

    private void ReleaseMouse()
    {
        if (!IsMouseCaptured)
        {
            _hasMouseBaseline = false;
            return;
        }

        IsMouseCaptured = false;
        _hasMouseBaseline = false;
        _setCursorState(CursorState.Normal);
    }

    private void OnKeyDown(Window window, KeyEventArgs args) => HandleKeyDown(args);

    private void OnKeyUp(Window window, KeyEventArgs args) => HandleKeyUp(args);

    private void OnMouseMove(Window window, MouseMoveEventArgs args) => HandleMouseMove(args);

    private void OnMouseDown(Window window, MouseClickEventArgs args) => HandleMouseDown(args);

    private void OnFocusChanged(Window window, bool focused) => HandleFocusChanged(focused);
}