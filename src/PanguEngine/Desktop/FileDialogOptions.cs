using PanguEngine.Windowing;

namespace PanguEngine.Desktop;

/// <summary>
/// Describes platform hints for an open or save file dialog.
/// </summary>
public sealed class FileDialogOptions
{
    /// <summary>
    /// Creates file dialog options with platform defaults.
    /// </summary>
    public FileDialogOptions()
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
    /// Gets the requested initial directory or file path, when supported by the platform.
    /// </summary>
    public string? InitialLocation { get; init; }

    /// <summary>
    /// Gets the requested file type filters.
    /// </summary>
    public IReadOnlyList<FileDialogFilter> Filters { get; init; } = [];
}
