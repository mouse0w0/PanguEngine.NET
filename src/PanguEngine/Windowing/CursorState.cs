namespace PanguEngine.Windowing;

/// <summary>
/// Cursor visibility and behavior modes.
/// </summary>
public enum CursorState
{
    /// <summary>Cursor is visible and can move freely.</summary>
    Normal,

    /// <summary>Cursor is invisible but can move freely.</summary>
    Hidden,

    /// <summary>Cursor is invisible and locked to the window center.</summary>
    Disabled,

    /// <summary>Cursor is invisible, locked to center, with unscaled raw motion.</summary>
    Raw
}