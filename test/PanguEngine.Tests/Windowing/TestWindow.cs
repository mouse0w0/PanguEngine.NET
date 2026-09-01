using PanguEngine.Graphics;
using PanguEngine.Input;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using EngineWindow = PanguEngine.Windowing.Window;

namespace PanguEngine.Tests.Windowing;

#pragma warning disable CS0067
internal sealed class TestWindow(bool isPrimary = false) : EngineWindow
{
    private bool _isDestroyed;
    private bool _isFocused = true;
    private Vector2D<int>? _framebufferSize;
    private Vector2D<float> _mousePosition;
    private KeyModifiers _keyModifiers;

    internal int EventCallCount { get; set; }

    public override bool IsDestroyed => _isDestroyed;
    public override string Title { get; set; } = "";
    public override Vector2D<int> Position { get; set; }
    public override Vector2D<int> Size { get; set; }
    public override Vector2D<int> FramebufferSize => _framebufferSize ?? Size;
    public override Vector2D<int> FullSize => Size;
    public override Rectangle<int> BorderSize => default;
    public override bool IsFocused => _isFocused;
    public override WindowState WindowState { get; set; } = WindowState.Normal;
    public override DisplayMonitor? Monitor => null;
    public override VideoMode VideoMode => VideoMode.Default;
    public override bool IsVisible { get; set; } = true;
    public override bool IsClosing { get; set; }
    public override WindowBorder WindowBorder { get; set; } = WindowBorder.Resizable;
    public override double FramesPerSecond { get; set; }
    public override bool VSync { get; set; }
    public override bool TopMost { get; set; }
    public override bool IsPrimary { get; } = isPrimary;
    public override Presenter Presenter => throw new NotSupportedException();
    public override CursorState CursorState { get; set; }
    public override CursorShape CursorShape { get; set; }
    public override Vector2D<float> MousePosition => _mousePosition;
    public override KeyModifiers KeyModifiers => _keyModifiers;
    public override string ClipboardText { get; set; } = "";
    public override IReadOnlyList<Key> SupportedKeys => [];
    public override IReadOnlyList<MouseButton> SupportedMouseButtons => [];

    public override event Action<EngineWindow, ResizeEventArgs>? Resize;
    public override event Action<EngineWindow, FramebufferResizeEventArgs>? FramebufferResize;
    public override event Action<EngineWindow, Vector2D<int>>? Move;
    public override event Action<EngineWindow, WindowState>? StateChanged;
    public override event Action<EngineWindow, FileDropEventArgs>? FileDrop;
    public override event Action<EngineWindow>? Close;
    public override event Action<EngineWindow, bool>? FocusChanged;
    public override event Action<EngineWindow, KeyEventArgs>? KeyDown;
    public override event Action<EngineWindow, KeyEventArgs>? KeyUp;
    public override event Action<EngineWindow, MouseMoveEventArgs>? MouseMove;
    public override event Action<EngineWindow, MouseClickEventArgs>? MouseDown;
    public override event Action<EngineWindow, MouseClickEventArgs>? MouseUp;
    public override event Action<EngineWindow, ScrollEventArgs>? Scroll;
    public override event Action<EngineWindow, char>? CharInput;
    public override event Action<EngineWindow, double>? PreRender;
    public override event Action<EngineWindow, double>? Render;

    public override void Show() => IsVisible = true;
    public override void Hide() => IsVisible = false;
    public override void CenterOnScreen() { }
    public override void Focus() { }
    public override Vector2D<int> PointToClient(Vector2D<int> point) => point;
    public override Vector2D<int> PointToScreen(Vector2D<int> point) => point;
    public override Vector2D<int> PointToFramebuffer(Vector2D<int> point) => point;
    public override bool IsKeyPressed(Key key) => false;
    public override bool IsMouseButtonPressed(MouseButton button) => false;
    public override void BeginTextInput() { }
    public override void EndTextInput() { }
    public override void SetWindowIcon(WindowIcon icon) { }
    public override void SetWindowIcons(WindowIcon[] icons) { }
    public override void SetDefaultIcon() { }

    public override void CloseWindow()
    {
        IsClosing = true;
        Close?.Invoke(this);
    }

    internal override void Destroy() => _isDestroyed = true;
    internal override void DoEvents() => EventCallCount++;
    internal override void DoPreRender(double alpha) => PreRender?.Invoke(this, alpha);
    internal override void DoRender(double alpha) => Render?.Invoke(this, alpha);

    internal void SetFramebufferSize(Vector2D<int> size) => _framebufferSize = size;

    internal void SetKeyModifiers(KeyModifiers modifiers) => _keyModifiers = modifiers;

    internal void SetMousePosition(Vector2D<float> position) => _mousePosition = position;

    internal void RaiseKeyDown(KeyEventArgs args) => KeyDown?.Invoke(this, args);

    internal void RaiseKeyUp(KeyEventArgs args) => KeyUp?.Invoke(this, args);

    internal void RaiseMouseMove(MouseMoveEventArgs args)
    {
        _mousePosition = new Vector2D<float>(args.X, args.Y);
        MouseMove?.Invoke(this, args);
    }

    internal void RaiseMouseDown(MouseClickEventArgs args)
    {
        _mousePosition = new Vector2D<float>(args.X, args.Y);
        MouseDown?.Invoke(this, args);
    }

    internal void RaiseMouseUp(MouseClickEventArgs args)
    {
        _mousePosition = new Vector2D<float>(args.X, args.Y);
        MouseUp?.Invoke(this, args);
    }

    internal void RaiseScroll(ScrollEventArgs args) => Scroll?.Invoke(this, args);

    internal void RaiseFocusChanged(bool focused)
    {
        _isFocused = focused;
        FocusChanged?.Invoke(this, focused);
    }
}
#pragma warning restore CS0067
