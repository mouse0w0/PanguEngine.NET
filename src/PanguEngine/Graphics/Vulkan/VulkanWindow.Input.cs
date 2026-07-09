using System.Numerics;
using PanguEngine.Windowing;
using Silk.NET.Input;
using Silk.NET.Maths;
using InputKey = PanguEngine.Input.Key;
using InputKeyAction = PanguEngine.Input.KeyAction;
using InputKeyModifiers = PanguEngine.Input.KeyModifiers;
using InputMouseButton = PanguEngine.Input.MouseButton;
using SilkKey = Silk.NET.Input.Key;
using SilkMouseButton = Silk.NET.Input.MouseButton;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed partial class VulkanWindow
{
    private IInputContext? _inputContext;
    private ICursor? _cursor;

    /// <inheritdoc/>
    public override CursorState CursorState
    {
        get => _cursor?.CursorMode switch
        {
            CursorMode.Hidden => CursorState.Hidden,
            CursorMode.Disabled => CursorState.Disabled,
            CursorMode.Raw => CursorState.Raw,
            _ => CursorState.Normal
        };
        set
        {
            _cursor?.CursorMode = value switch
            {
                CursorState.Hidden => CursorMode.Hidden,
                CursorState.Disabled => CursorMode.Disabled,
                CursorState.Raw => CursorMode.Raw,
                _ => CursorMode.Normal
            };
        }
    }

    /// <inheritdoc/>
    public override CursorShape CursorShape
    {
        get => _cursor?.StandardCursor switch
        {
            StandardCursor.Arrow => CursorShape.Arrow,
            StandardCursor.IBeam => CursorShape.IBeam,
            StandardCursor.Crosshair => CursorShape.Crosshair,
            StandardCursor.Hand => CursorShape.Hand,
            StandardCursor.HResize => CursorShape.HResize,
            StandardCursor.VResize => CursorShape.VResize,
            StandardCursor.NwseResize => CursorShape.NwseResize,
            StandardCursor.NeswResize => CursorShape.NeswResize,
            StandardCursor.ResizeAll => CursorShape.ResizeAll,
            StandardCursor.NotAllowed => CursorShape.NotAllowed,
            StandardCursor.Wait => CursorShape.Wait,
            StandardCursor.WaitArrow => CursorShape.WaitArrow,
            _ => CursorShape.Arrow
        };
        set
        {
            _cursor?.Type = CursorType.Standard;
            _cursor?.StandardCursor = value switch
            {
                CursorShape.Arrow => StandardCursor.Arrow,
                CursorShape.IBeam => StandardCursor.IBeam,
                CursorShape.Crosshair => StandardCursor.Crosshair,
                CursorShape.Hand => StandardCursor.Hand,
                CursorShape.HResize => StandardCursor.HResize,
                CursorShape.VResize => StandardCursor.VResize,
                CursorShape.NwseResize => StandardCursor.NwseResize,
                CursorShape.NeswResize => StandardCursor.NeswResize,
                CursorShape.ResizeAll => StandardCursor.ResizeAll,
                CursorShape.NotAllowed => StandardCursor.NotAllowed,
                CursorShape.Wait => StandardCursor.Wait,
                CursorShape.WaitArrow => StandardCursor.WaitArrow,
                _ => StandardCursor.Arrow
            };
        }
    }

    /// <inheritdoc/>
    public override Vector2D<float> MousePosition
    {
        get
        {
            var mice = _inputContext?.Mice;
            var position = mice is not null && mice.Count > 0 ? mice[0].Position : Vector2.Zero;
            return new Vector2D<float>(position.X, position.Y);
        }
    }

    /// <inheritdoc/>
    public override InputKeyModifiers KeyModifiers => GetCurrentKeyModifiers();

    /// <inheritdoc/>
    public override string ClipboardText
    {
        get
        {
            var keyboards = _inputContext?.Keyboards;
            return keyboards is not null && keyboards.Count > 0 ? keyboards[0].ClipboardText : "";
        }
        set
        {
            if (_inputContext is null) return;
            foreach (var keyboard in _inputContext.Keyboards)
                keyboard.ClipboardText = value;
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<InputKey> SupportedKeys
    {
        get
        {
            var keyboards = _inputContext?.Keyboards;
            if (keyboards is null || keyboards.Count == 0)
                return [];

            var keys = keyboards[0].SupportedKeys;
            var result = new InputKey[keys.Count];
            for (var i = 0; i < keys.Count; i++)
                result[i] = (InputKey)(int)keys[i];

            return result;
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<InputMouseButton> SupportedMouseButtons
    {
        get
        {
            var mice = _inputContext?.Mice;
            if (mice is null || mice.Count == 0)
                return [];

            var buttons = mice[0].SupportedButtons;
            var result = new InputMouseButton[buttons.Count];
            for (var i = 0; i < buttons.Count; i++)
                result[i] = (InputMouseButton)(int)buttons[i];

            return result;
        }
    }

    /// <inheritdoc/>
    public override bool IsKeyPressed(InputKey key)
    {
        var keyboards = _inputContext?.Keyboards;
        if (keyboards is null)
            return false;

        foreach (var keyboard in keyboards)
        {
            if (keyboard.IsKeyPressed((SilkKey)(int)key))
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override bool IsMouseButtonPressed(InputMouseButton button)
    {
        var mice = _inputContext?.Mice;
        if (mice is null)
            return false;

        foreach (var mouse in mice)
        {
            if (mouse.IsButtonPressed((SilkMouseButton)(int)button))
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override void BeginTextInput()
    {
        if (_inputContext is null) return;
        foreach (var keyboard in _inputContext.Keyboards)
            keyboard.BeginInput();
    }

    /// <inheritdoc/>
    public override void EndTextInput()
    {
        if (_inputContext is null) return;
        foreach (var keyboard in _inputContext.Keyboards)
            keyboard.EndInput();
    }

    /// <summary>Gets the keyboard modifiers currently pressed by any keyboard device.</summary>
    /// <returns>The active engine key modifiers.</returns>
    private InputKeyModifiers GetCurrentKeyModifiers()
    {
        var mods = InputKeyModifiers.None;
        if (_inputContext is null)
            return mods;

        foreach (var keyboard in _inputContext.Keyboards)
        {
            if (keyboard.IsKeyPressed(SilkKey.ShiftLeft) || keyboard.IsKeyPressed(SilkKey.ShiftRight))
                mods |= InputKeyModifiers.Shift;
            if (keyboard.IsKeyPressed(SilkKey.ControlLeft) || keyboard.IsKeyPressed(SilkKey.ControlRight))
                mods |= InputKeyModifiers.Control;
            if (keyboard.IsKeyPressed(SilkKey.AltLeft) || keyboard.IsKeyPressed(SilkKey.AltRight))
                mods |= InputKeyModifiers.Alt;
            if (keyboard.IsKeyPressed(SilkKey.SuperLeft) || keyboard.IsKeyPressed(SilkKey.SuperRight))
                mods |= InputKeyModifiers.Super;
        }

        return mods;
    }

    /// <summary>Creates the input context and subscribes to input events.</summary>
    private void InitializeInput()
    {
        _inputContext = _silkWindow.CreateInput();
        foreach (var keyboard in _inputContext.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }

        foreach (var mouse in _inputContext.Mice)
        {
            _cursor = mouse.Cursor;
            mouse.MouseMove += OnMouseMove;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.Scroll += OnMouseScroll;
        }
    }

    /// <summary>Handles a platform key press event.</summary>
    /// <param name="keyboard">The keyboard that raised the event.</param>
    /// <param name="key">The pressed key.</param>
    /// <param name="scancode">The platform scan code.</param>
    private void OnKeyDown(IKeyboard keyboard, SilkKey key, int scancode)
    {
        KeyDown?.Invoke(this, new KeyEventArgs((InputKey)(int)key, InputKeyAction.Press, GetCurrentKeyModifiers()));
    }

    /// <summary>Handles a platform key release event.</summary>
    /// <param name="keyboard">The keyboard that raised the event.</param>
    /// <param name="key">The released key.</param>
    /// <param name="scancode">The platform scan code.</param>
    private void OnKeyUp(IKeyboard keyboard, SilkKey key, int scancode)
    {
        KeyUp?.Invoke(this, new KeyEventArgs((InputKey)(int)key, InputKeyAction.Release, GetCurrentKeyModifiers()));
    }

    /// <summary>Handles a platform text input event.</summary>
    /// <param name="keyboard">The keyboard that raised the event.</param>
    /// <param name="c">The input character.</param>
    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        CharInput?.Invoke(this, c);
    }

    /// <summary>Handles a platform mouse move event.</summary>
    /// <param name="mouse">The mouse that raised the event.</param>
    /// <param name="position">The new mouse position.</param>
    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseMove?.Invoke(this, new MouseMoveEventArgs(position.X, position.Y));
    }

    /// <summary>Handles a platform mouse button press event.</summary>
    /// <param name="mouse">The mouse that raised the event.</param>
    /// <param name="button">The pressed mouse button.</param>
    private void OnMouseDown(IMouse mouse, SilkMouseButton button)
    {
        MouseDown?.Invoke(this,
            new MouseClickEventArgs((InputMouseButton)(int)button, mouse.Position.X, mouse.Position.Y));
    }

    /// <summary>Handles a platform mouse button release event.</summary>
    /// <param name="mouse">The mouse that raised the event.</param>
    /// <param name="button">The released mouse button.</param>
    private void OnMouseUp(IMouse mouse, SilkMouseButton button)
    {
        MouseUp?.Invoke(this,
            new MouseClickEventArgs((InputMouseButton)(int)button, mouse.Position.X, mouse.Position.Y));
    }

    /// <summary>Handles a platform mouse scroll event.</summary>
    /// <param name="mouse">The mouse that raised the event.</param>
    /// <param name="scrollWheel">The scroll wheel delta.</param>
    private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        Scroll?.Invoke(this, new ScrollEventArgs(scrollWheel.X, scrollWheel.Y));
    }
}