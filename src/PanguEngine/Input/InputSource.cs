namespace PanguEngine.Input;

/// <summary>
/// Identifies a physical input source category.
/// </summary>
public enum InputSourceType
{
    /// <summary>A keyboard key.</summary>
    Key,

    /// <summary>A mouse button.</summary>
    MouseButton,

    /// <summary>Mouse movement.</summary>
    MouseMove,

    /// <summary>Mouse wheel movement.</summary>
    MouseWheel
}

/// <summary>
/// Identifies a physical keyboard or mouse input source.
/// </summary>
public readonly record struct InputSource
{
    /// <summary>The mouse movement source.</summary>
    public static InputSource MouseMove { get; } = new(InputSourceType.MouseMove, Key.Unknown, MouseButton.Unknown);

    /// <summary>The mouse wheel source.</summary>
    public static InputSource MouseWheel { get; } = new(InputSourceType.MouseWheel, Key.Unknown, MouseButton.Unknown);

    /// <summary>Creates a keyboard key source.</summary>
    /// <param name="key">The keyboard key.</param>
    /// <returns>The created source.</returns>
    public static InputSource FromKey(Key key) => new(InputSourceType.Key, key, MouseButton.Unknown);

    /// <summary>Creates a mouse button source.</summary>
    /// <param name="button">The mouse button.</param>
    /// <returns>The created source.</returns>
    public static InputSource FromMouseButton(MouseButton button) => new(InputSourceType.MouseButton, Key.Unknown, button);

    private InputSource(InputSourceType type, Key key, MouseButton mouseButton)
    {
        Type = type;
        Key = key;
        MouseButton = mouseButton;
    }

    /// <summary>The physical source category.</summary>
    public InputSourceType Type { get; }

    /// <summary>The keyboard key for a key source.</summary>
    public Key Key { get; }

    /// <summary>The mouse button for a mouse button source.</summary>
    public MouseButton MouseButton { get; }
}
