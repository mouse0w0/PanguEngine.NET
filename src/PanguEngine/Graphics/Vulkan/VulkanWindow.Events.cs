using PanguEngine.Input;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Maths;
using Window = PanguEngine.Windowing.Window;
using WindowState = PanguEngine.Windowing.WindowState;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed partial class VulkanWindow
{
    /// <inheritdoc/>
    public override event Action<Window, ResizeEventArgs>? Resize;

    /// <inheritdoc/>
    public override event Action<Window, FramebufferResizeEventArgs>? FramebufferResize;

    /// <inheritdoc/>
    public override event Action<Window, Vector2D<int>>? Move;

    /// <inheritdoc/>
    public override event Action<Window, WindowState>? StateChanged;

    /// <inheritdoc/>
    public override event Action<Window, FileDropEventArgs>? FileDrop;

    /// <inheritdoc/>
    public override event Action<Window>? Close;

    /// <inheritdoc/>
    public override event Action<Window, bool>? FocusChanged;

    /// <inheritdoc/>
    public override event Action<Window, KeyEventArgs>? KeyDown;

    /// <inheritdoc/>
    public override event Action<Window, KeyEventArgs>? KeyUp;

    /// <inheritdoc/>
    public override event Action<Window, MouseMoveEventArgs>? MouseMove;

    /// <inheritdoc/>
    public override event Action<Window, MouseClickEventArgs>? MouseDown;

    /// <inheritdoc/>
    public override event Action<Window, MouseClickEventArgs>? MouseUp;

    /// <inheritdoc/>
    public override event Action<Window, ScrollEventArgs>? Scroll;

    /// <inheritdoc/>
    public override event Action<Window, char>? CharInput;

    /// <inheritdoc/>
    public override event Action<Window, double>? PreRender;

    /// <inheritdoc/>
    public override event Action<Window, double>? Render;

    internal void HandleEvent(in SDL_Event @event)
    {
        if (IsDestroyed)
            return;
        switch (@event.Type)
        {
            case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                _framebufferResized = true;
                var framebufferSize = FramebufferSize;
                Resize?.Invoke(this, new ResizeEventArgs(
                    @event.window.data1,
                    @event.window.data2,
                    framebufferSize.X,
                    framebufferSize.Y));
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                _framebufferResized = true;
                FramebufferResize?.Invoke(this, new FramebufferResizeEventArgs(
                    @event.window.data1,
                    @event.window.data2));
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MOVED:
                Move?.Invoke(this, new Vector2D<int>(@event.window.data1, @event.window.data2));
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED:
                StateChanged?.Invoke(this, WindowState.Minimized);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED:
                StateChanged?.Invoke(this, WindowState.Maximized);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_RESTORED:
            case SDL_EventType.SDL_EVENT_WINDOW_LEAVE_FULLSCREEN:
                StateChanged?.Invoke(this, WindowState.Normal);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_ENTER_FULLSCREEN:
                StateChanged?.Invoke(this, WindowState.Fullscreen);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                RequestClose();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                SetFocus(true);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                SetFocus(false);
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_ENTER:
                ApplyCursorShape();
                ApplyCursorState();
                break;
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
                HandleKeyEvent(@event.key);
                break;
            case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                var textEvent = @event.text;
                HandleTextInput(textEvent.GetText());
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                HandleMouseMotion(@event.motion);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                HandleMouseButton(@event.button, true);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                HandleMouseButton(@event.button, false);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                var scrollX = @event.wheel.x;
                var scrollY = @event.wheel.y;
                if (@event.wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED)
                {
                    scrollX = -scrollX;
                    scrollY = -scrollY;
                }
                Scroll?.Invoke(this, new ScrollEventArgs(scrollX, scrollY));
                break;
            default:
                return;
        }
    }

    internal void HandleDropEvent(SDL_EventType type, string? path)
    {
        if (IsDestroyed)
            return;
        switch (type)
        {
            case SDL_EventType.SDL_EVENT_DROP_BEGIN:
                _eventState.BeginDrop();
                break;
            case SDL_EventType.SDL_EVENT_DROP_FILE:
                if (path is not null)
                    _eventState.AddDropFile(path);
                break;
            case SDL_EventType.SDL_EVENT_DROP_COMPLETE:
                FileDrop?.Invoke(this, new FileDropEventArgs(_eventState.CompleteDrop()));
                break;
            default:
                return;
        }
    }

    internal void RequestClose()
    {
        if (IsDestroyed)
            return;
        if (IsClosing)
            return;

        IsClosing = true;
        Close?.Invoke(this);
    }

    private void SetFocus(bool focused)
    {
        if (IsFocused == focused)
            return;

        _isFocused = focused;
        FocusChanged?.Invoke(this, focused);
    }

    private void HandleKeyEvent(SDL_KeyboardEvent keyEvent)
    {
        if (keyEvent.repeat && keyEvent.down)
            return;
        if (!SdlKeyMapping.TryGetKey(keyEvent.scancode, out var key))
            return;

        var args = new KeyEventArgs(
            key,
            keyEvent.down ? KeyAction.Press : KeyAction.Release,
            ToKeyModifiers(keyEvent.mod));
        if (keyEvent.down)
            KeyDown?.Invoke(this, args);
        else
            KeyUp?.Invoke(this, args);
    }

    private void HandleTextInput(string? text)
    {
        if (text is null)
            return;
        foreach (var character in text)
            CharInput?.Invoke(this, character);
    }

    private void HandleMouseMotion(SDL_MouseMotionEvent motion)
    {
        var position = _eventState.ApplyMouseMotion(motion.x, motion.y, motion.xrel, motion.yrel);
        MouseMove?.Invoke(this, new MouseMoveEventArgs(position.X, position.Y));
    }

    private void HandleMouseButton(SDL_MouseButtonEvent button, bool down)
    {
        if (!SdlMouseMapping.TryGetButton(button.button, out var mouseButton))
            return;

        var position = _cursorState is CursorState.Disabled or CursorState.Raw
            ? _eventState.MousePosition
            : new Vector2D<float>(button.x, button.y);
        var args = new MouseClickEventArgs(mouseButton, position.X, position.Y);
        if (down)
            MouseDown?.Invoke(this, args);
        else
            MouseUp?.Invoke(this, args);
    }

    private static KeyModifiers ToKeyModifiers(SDL_Keymod modifiers)
    {
        var modifierValue = (uint)modifiers;
        var result = KeyModifiers.None;
        if ((modifierValue & SDL3.SDL_KMOD_SHIFT) != 0)
            result |= KeyModifiers.Shift;
        if ((modifierValue & SDL3.SDL_KMOD_CTRL) != 0)
            result |= KeyModifiers.Control;
        if ((modifierValue & SDL3.SDL_KMOD_ALT) != 0)
            result |= KeyModifiers.Alt;
        if ((modifierValue & SDL3.SDL_KMOD_GUI) != 0)
            result |= KeyModifiers.Super;
        return result;
    }
}
