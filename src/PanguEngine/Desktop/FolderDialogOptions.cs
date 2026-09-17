using PanguEngine.Windowing;

namespace PanguEngine.Desktop;

/// <summary>
/// Describes platform hints for a folder selection dialog.
/// </summary>
public sealed class FolderDialogOptions
{
    /// <summary>
    /// Creates folder dialog options with platform defaults.
    /// </summary>
    public FolderDialogOptions()
    {
    }

    /// <summary>
    /// Gets the window that should own the dialog, when supported by the platform.
    /// </summary>
    public Window? Owner { get; init; }

    /// <summary>
    /// Gets the requested dialog title, when supported by the platform.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the requested initial folder, when supported by the platform.
    /// </summary>
    public string? InitialLocation { get; init; }
}
