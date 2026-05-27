namespace PanguEngine.Input;

/// <summary>
/// Keyboard modifier key flags.
/// </summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier keys are active.</summary>
    None = 0,

    /// <summary>A shift key is held.</summary>
    Shift = 1,

    /// <summary>A control key is held.</summary>
    Control = 2,

    /// <summary>An alt key is held.</summary>
    Alt = 4,

    /// <summary>A super (command/windows) key is held.</summary>
    Super = 8,
}