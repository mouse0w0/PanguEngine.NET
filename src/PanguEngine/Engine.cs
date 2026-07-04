using Microsoft.Extensions.Logging;
using PanguEngine.Events;
using PanguEngine.Modding;
using PanguEngine.Registries;
using PanguEngine.Resources;

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
    /// The runtime event bus.
    /// </summary>
    public static IEventBus EventBus { get; private set; } = null!;

    public static RegistryManager RegistryManager { get; private set; } = null!;

    /// <summary>
    /// The loaded mod manager.
    /// </summary>
    public static ModManager ModManager { get; private set; } = null!;

    /// <summary>
    /// The runtime resource manager.
    /// </summary>
    public static ResourceManager ResourceManager { get; private set; } = null!;

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
        EventBus = new EventBus(ThrowingEventExceptionHandler.Instance);
        RegistryManager = new RegistryManager();
        ModManager = new ModManager(Path.Combine(AppContext.BaseDirectory, "mods"), Log.CreateLogger("Mods"),
            options.ModPaths);
        ModManager.Load();
        ModManager.RunConfigure();
        RegistryManager.FreezeAll();
        ResourceManager = CreateResourceManager();
        ModManager.RunCommonSetup();
    }

    /// <summary>
    /// Shuts down the engine.
    /// </summary>
    internal static void Shutdown()
    {
        ResourceManager.Dispose();
        ModManager.Shutdown();
        Log.Shutdown();
    }

    private static ResourceManager CreateResourceManager()
    {
        var sources = new List<IResourceSource>(ModManager.LoadedMods.Select(mod => mod.Resources))
        {
            new DirectoryResourceSource(AppContext.BaseDirectory)
        };
        return new ResourceManager(sources);
    }
}