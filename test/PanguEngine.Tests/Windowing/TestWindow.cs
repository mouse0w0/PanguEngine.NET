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

    public override bool IsDestroyed => _isDestroyed;
    public override string Title { get; set; } = "";
    public override Vector2D<int> Position { get; set; }
    public override Vector2D<int> Size { get; set; }
    public override Vector2D<int> FramebufferSize => Size;
    public override Vector2D<int> FullSize => Size;
    public override Rectangle<int> BorderSize => default;
    public override bool IsFocused => true;
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
    public override Vector2D<float> MousePosition => default;
    public override KeyModifiers KeyModifiers => default;
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
    internal override void DoEvents() { }
    internal override void DoPreRender(double alpha) => PreRender?.Invoke(this, alpha);
    internal override void DoRender(double alpha) => Render?.Invoke(this, alpha);
}
#pragma warning restore CS0067
