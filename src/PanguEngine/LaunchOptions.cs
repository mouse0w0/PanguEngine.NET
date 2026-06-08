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
    /// Additional mod paths to load during startup.
    /// </summary>
    public IReadOnlyList<string> ModPaths { get; init; } = [];
}