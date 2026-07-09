namespace PanguEngine.Windowing;

/// <summary>
/// Provides file drop event data.
/// </summary>
/// <param name="Paths">The platform file paths dropped onto the window.</param>
public record FileDropEventArgs(string[] Paths);