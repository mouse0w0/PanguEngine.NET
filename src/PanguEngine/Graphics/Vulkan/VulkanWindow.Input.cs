using System.Runtime.InteropServices;
using System.Text;
using PanguEngine.Input;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Maths;
using InputKey = PanguEngine.Input.Key;
using InputMouseButton = PanguEngine.Input.MouseButton;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed unsafe partial class VulkanWindow
{
    private CursorState _cursorState;
    private CursorShape _cursorShape = CursorShape.Arrow;

    /// <inheritdoc/>
    public override CursorState CursorState
    {
        get => _cursorState;
        set
        {
            VulkanContext.EnsureRenderThread();
            if (_cursorState == value)
                return;
            var wasRelative = _cursorState is CursorState.Disabled or CursorState.Raw;
            var shouldBeRelative = value is CursorState.Disabled or CursorState.Raw;
            if (wasRelative && shouldBeRelative)
            {
                _cursorState = value;
                return;
            }

            if (shouldBeRelative)
            {
                var position = GetAbsoluteMousePosition();
                if (!SDL3.SDL_SetWindowRelativeMouseMode(NativeWindow, true))
                    throw CreateSdlException("SDL relative mouse mode update");
                _eventState.EnterRelativeMode(position);
                _cursorState = value;
                return;
            }

            var absolutePosition = _eventState.MousePosition;
            if (wasRelative)
            {
                if (!SDL3.SDL_SetWindowRelativeMouseMode(NativeWindow, false))
                    throw CreateSdlException("SDL relative mouse mode update");
                absolutePosition = GetAbsoluteMousePosition();
            }

            if (HasMouseFocus() && value == CursorState.Hidden)
            {
                if (!SDL3.SDL_HideCursor())
                {
                    if (wasRelative)
                        SDL3.SDL_SetWindowRelativeMouseMode(NativeWindow, true);
                    throw CreateSdlException("SDL cursor hiding");
                }
            }
            else if (HasMouseFocus() && !SDL3.SDL_ShowCursor())
            {
                if (wasRelative)
                    SDL3.SDL_SetWindowRelativeMouseMode(NativeWindow, true);
                throw CreateSdlException("SDL cursor showing");
            }

            if (wasRelative)
                _eventState.ExitRelativeMode(absolutePosition);
            _cursorState = value;
        }
    }

    /// <inheritdoc/>
    public override CursorShape CursorShape
    {
        get => _cursorShape;
        set
        {
            VulkanContext.EnsureRenderThread();
            if (_cursorShape == value)
                return;

            if (HasMouseFocus())
            {
                var cursor = _platform.GetCursor(value);
                if (!SDL3.SDL_SetCursor(cursor))
                    throw CreateSdlException("SDL cursor update");
            }
            _cursorShape = value;
        }
    }

    internal void ApplyCursorState()
    {
        VulkanContext.EnsureRenderThread();
        if (_cursorState is CursorState.Disabled or CursorState.Raw)
        {
            if (!SDL3.SDL_SetWindowRelativeMouseMode(NativeWindow, true))
                throw CreateSdlException("SDL relative mouse mode update");
        }
        else if (_cursorState == CursorState.Hidden)
        {
            if (!SDL3.SDL_HideCursor())
                throw CreateSdlException("SDL cursor hiding");
        }
        else if (!SDL3.SDL_ShowCursor())
        {
            throw CreateSdlException("SDL cursor showing");
        }
    }

    internal void ApplyCursorShape()
    {
        VulkanContext.EnsureRenderThread();
        if (!HasMouseFocus())
            return;
        var cursor = _platform.GetCursor(_cursorShape);
        if (!SDL3.SDL_SetCursor(cursor))
            throw CreateSdlException("SDL cursor update");
    }

    /// <inheritdoc/>
    public override Vector2D<float> MousePosition => _eventState.MousePosition;

    /// <inheritdoc/>
    public override KeyModifiers KeyModifiers
    {
        get
        {
            if (IsDestroyed)
                return KeyModifiers.None;
            VulkanContext.EnsureRenderThread();
            return ToKeyModifiers(SDL3.SDL_GetModState());
        }
    }

    /// <inheritdoc/>
    public override string ClipboardText
    {
        get
        {
            if (IsDestroyed)
                return "";
            VulkanContext.EnsureRenderThread();
            var text = SDL3.Unsafe_SDL_GetClipboardText();
            if (text is null)
                return "";

            try
            {
                return Marshal.PtrToStringUTF8((nint)text) ?? "";
            }
            finally
            {
                SDL3.SDL_free((nint)text);
            }
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            fixed (byte* text = bytes)
            {
                if (!SDL3.SDL_SetClipboardText(text))
                    throw CreateSdlException("SDL clipboard update");
            }
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<InputKey> SupportedKeys => SdlKeyMapping.SupportedKeys;

    /// <inheritdoc/>
    public override IReadOnlyList<InputMouseButton> SupportedMouseButtons => SdlMouseMapping.SupportedButtons;

    /// <inheritdoc/>
    public override bool IsKeyPressed(InputKey key)
    {
        if (IsDestroyed)
            return false;
        VulkanContext.EnsureRenderThread();
        if (!SdlKeyMapping.TryGetScancode(key, out var scancode))
            return false;
        if (SDL3.SDL_GetKeyboardFocus() != NativeWindow)
            return false;

        var keyCount = 0;
        var keyState = SDL3.SDL_GetKeyboardState(&keyCount);
        return keyState is not null && (int)scancode < keyCount && keyState[(int)scancode];
    }

    /// <inheritdoc/>
    public override bool IsMouseButtonPressed(InputMouseButton button)
    {
        if (IsDestroyed)
            return false;
        VulkanContext.EnsureRenderThread();
        var buttonNumber = button switch
        {
            InputMouseButton.Left => 1,
            InputMouseButton.Middle => 2,
            InputMouseButton.Right => 3,
            InputMouseButton.Button4 => 4,
            InputMouseButton.Button5 => 5,
            InputMouseButton.Button6 => 6,
            InputMouseButton.Button7 => 7,
            InputMouseButton.Button8 => 8,
            InputMouseButton.Button9 => 9,
            InputMouseButton.Button10 => 10,
            InputMouseButton.Button11 => 11,
            InputMouseButton.Button12 => 12,
            _ => 0
        };
        if (buttonNumber == 0)
            return false;
        if (!HasMouseFocus())
            return false;

        float x = 0;
        float y = 0;
        var state = SDL3.SDL_GetMouseState(&x, &y);
        return ((uint)state & (1u << (buttonNumber - 1))) != 0;
    }

    /// <inheritdoc/>
    public override void BeginTextInput()
    {
        if (IsDestroyed)
            return;
        VulkanContext.EnsureRenderThread();
        if (_textInputActive)
            return;
        if (!SDL3.SDL_StartTextInput(NativeWindow))
            throw CreateSdlException("SDL text input start");
        _textInputActive = true;
    }

    /// <inheritdoc/>
    public override void EndTextInput()
    {
        if (IsDestroyed)
            return;
        VulkanContext.EnsureRenderThread();
        if (!_textInputActive)
            return;
        if (!SDL3.SDL_StopTextInput(NativeWindow))
            throw CreateSdlException("SDL text input stop");
        _textInputActive = false;
    }

    private void InitializeInput()
    {
        _eventState.ExitRelativeMode(GetAbsoluteMousePosition());
        _cursorState = CursorState.Normal;
        var cursor = _platform.GetCursor(_cursorShape);
        if (HasMouseFocus() && !SDL3.SDL_SetCursor(cursor))
            throw CreateSdlException("SDL cursor initialization");
        BeginTextInput();
    }

    private Vector2D<float> GetAbsoluteMousePosition()
    {
        if (SDL3.SDL_GetMouseFocus() != NativeWindow)
            return _eventState.MousePosition;

        float x = 0;
        float y = 0;
        SDL3.SDL_GetMouseState(&x, &y);
        return new Vector2D<float>(x, y);
    }

    private bool HasMouseFocus() => SDL3.SDL_GetMouseFocus() == NativeWindow;

    private static InvalidOperationException CreateSdlException(string operation) =>
        new($"{operation} failed: {SDL3.SDL_GetError()}");
}
