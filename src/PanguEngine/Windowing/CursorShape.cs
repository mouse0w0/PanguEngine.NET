namespace PanguEngine.Windowing;

/// <summary>
/// Standard cursor shapes for visual feedback.
/// </summary>
/// <remarks>
/// Not every backend supports every cursor shape.
/// </remarks>
public enum CursorShape
{
    /// <summary>A regular arrow cursor.</summary>
    Arrow,

    /// <summary>A text input I-beam cursor.</summary>
    IBeam,

    /// <summary>A crosshair cursor.</summary>
    Crosshair,

    /// <summary>A pointing hand cursor.</summary>
    Hand,

    /// <summary>A horizontal resize arrow cursor.</summary>
    HResize,

    /// <summary>A vertical resize arrow cursor.</summary>
    VResize,

    /// <summary>A top-left to bottom-right diagonal resize cursor.</summary>
    NwseResize,

    /// <summary>A top-right to bottom-left diagonal resize cursor.</summary>
    NeswResize,

    /// <summary>An omni-directional resize/move cursor.</summary>
    ResizeAll,

    /// <summary>An operation-not-allowed cursor.</summary>
    NotAllowed,

    /// <summary>An hourglass/waiting cursor.</summary>
    Wait,

    /// <summary>A regular arrow with an hourglass/waiting cursor.</summary>
    WaitArrow
}