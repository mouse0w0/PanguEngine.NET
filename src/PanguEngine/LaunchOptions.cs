using System.Net;
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

    /// <summary>Whether to start a dedicated server instead of the client.</summary>
    public bool Server { get; init; }

    /// <summary>The remote server address, or null for an integrated game.</summary>
    public IPAddress? ConnectAddress { get; init; }

    /// <summary>The dedicated server listening address.</summary>
    public IPAddress? ListenAddress { get; init; }

    /// <summary>The remote or listening TCP port.</summary>
    public int Port { get; init; } = 20202;

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
