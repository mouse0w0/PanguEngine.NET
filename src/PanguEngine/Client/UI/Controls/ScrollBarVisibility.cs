namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Specifies when and whether one axis of a scroll view can scroll and shows its scroll bar.
/// </summary>
public enum ScrollBarVisibility
{
    /// <summary>The axis neither scrolls nor shows a scroll bar.</summary>
    Disabled,

    /// <summary>The axis scrolls but never shows a scroll bar.</summary>
    Hidden,

    /// <summary>The axis scrolls and shows a scroll bar only when the content overflows.</summary>
    Auto,

    /// <summary>The axis scrolls and always shows a scroll bar.</summary>
    Visible
}
