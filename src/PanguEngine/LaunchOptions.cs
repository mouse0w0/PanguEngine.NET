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
}