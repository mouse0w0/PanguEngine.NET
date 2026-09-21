namespace PanguEngine.Input;

/// <summary>
/// Identifies the broad input domain of a context.
/// </summary>
public enum InputScope
{
    /// <summary>User-interface input.</summary>
    Ui,

    /// <summary>Game input.</summary>
    Game
}

/// <summary>
/// Identifies physical input categories captured by a context.
/// </summary>
[Flags]
public enum InputCaptureMask
{
    /// <summary>No input categories are captured.</summary>
    None = 0,

    /// <summary>Keyboard input.</summary>
    Keyboard = 1,

    /// <summary>Mouse button input.</summary>
    MouseButton = 2,

    /// <summary>Mouse movement input.</summary>
    MouseMove = 4,

    /// <summary>Mouse wheel input.</summary>
    MouseWheel = 8,

    /// <summary>All keyboard and mouse input.</summary>
    All = Keyboard | MouseButton | MouseMove | MouseWheel
}

/// <summary>
/// Describes how a context affects pointer capture.
/// </summary>
public enum PointerCapturePolicy
{
    /// <summary>Leaves the current pointer capture state unchanged.</summary>
    Preserve,

    /// <summary>Suspends pointer capture while the context is active.</summary>
    Suspend
}

/// <summary>
/// Defines a runtime input routing context.
/// </summary>
public sealed class InputContext
{
    /// <summary>
    /// Creates an input context.
    /// </summary>
    /// <param name="scope">The input scope.</param>
    /// <param name="captureMask">The physical categories captured from lower contexts.</param>
    /// <param name="pointerCapturePolicy">The pointer capture policy.</param>
    public InputContext(
        InputScope scope,
        InputCaptureMask captureMask = InputCaptureMask.None,
        PointerCapturePolicy pointerCapturePolicy = PointerCapturePolicy.Preserve)
    {
        Scope = scope;
        CaptureMask = captureMask;
        PointerCapturePolicy = pointerCapturePolicy;
    }

    /// <summary>The input scope.</summary>
    public InputScope Scope { get; }

    /// <summary>The categories captured from lower contexts.</summary>
    public InputCaptureMask CaptureMask { get; }

    /// <summary>The pointer capture policy.</summary>
    public PointerCapturePolicy PointerCapturePolicy { get; }
}
