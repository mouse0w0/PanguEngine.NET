using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Initializes the engine.
    /// </summary>
    internal static void Initialize()
    {
        Log.Initialize();
        Logger = Log.CreateLogger("Engine");
    }

    /// <summary>
    /// Shuts down the engine.
    /// </summary>
    internal static void Shutdown()
    {
        Log.Shutdown();
    }
}