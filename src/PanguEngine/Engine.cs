using Microsoft.Extensions.Logging;
using PanguEngine.Mod;

namespace PanguEngine;

/// <summary>
/// Engine.
/// </summary>
public static class Engine
{
    /// <summary>
    /// The engine-level logger.
    /// </summary>
    public static ILogger Logger { get; private set; } = null!;

    public static ModManager ModManager { get; private set; } = null!;

    /// <summary>
    /// Initializes the engine.
    /// </summary>
    internal static void Initialize()
    {
        Initialize(LaunchOptions.Empty);
    }

    /// <summary>
    /// Initializes the engine with launch options.
    /// </summary>
    /// <param name="options">The launch options.</param>
    internal static void Initialize(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Log.Initialize();
        Logger = Log.CreateLogger("Engine");
        ModManager = new ModManager(Path.Combine(AppContext.BaseDirectory, "Mods"), Log.CreateLogger("Mods"),
            options.ModPaths);
        ModManager.Load();
    }

    /// <summary>
    /// Shuts down the engine.
    /// </summary>
    internal static void Shutdown()
    {
        ModManager.Shutdown();
        Log.Shutdown();
    }
}