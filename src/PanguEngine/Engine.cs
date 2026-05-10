using Microsoft.Extensions.Logging;

namespace PanguEngine;

public static class Engine
{
    public static ILogger Logger { get; private set; } = null!;

    internal static void Initialize()
    {
        Log.Initialize();
        Logger = Log.CreateLogger("Engine");
    }

    internal static void Shutdown()
    {
        Log.Shutdown();
    }
}