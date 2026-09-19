namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Specifies the arm layout used by a crosshair.
/// </summary>
public enum CrosshairShape
{
    /// <summary>Draws four arms around the center.</summary>
    Cross,

    /// <summary>Draws left, right, and lower arms.</summary>
    T,

    /// <summary>Draws left, right, and upper arms.</summary>
    InvertedT
}
