using Silk.NET.Maths;

namespace PanguEngine;

/// <summary>
/// Options used when launching the engine.
/// </summary>
public sealed class LaunchOptions
{
    /// <summary>
    /// Empty launch options.
    /// </summary>
    public static LaunchOptions Empty { get; } = new();

    /// <summary>
    /// Whether to enable graphics API validation during startup.
    /// </summary>
    public bool GpuValidation { get; init; }

    /// <summary>
    /// Additional mod paths to load during startup.
    /// </summary>
    public IReadOnlyList<string> ModPaths { get; init; } = [];

    /// <summary>
    /// The requested client window title.
    /// </summary>
    public string? WindowTitle { get; init; }

    /// <summary>
    /// The requested client window size in screen coordinates.
    /// </summary>
    public Vector2D<int>? WindowSize { get; init; }

    /// <summary>
    /// The requested client window mode.
    /// </summary>
    public WindowMode? WindowMode { get; init; }
}