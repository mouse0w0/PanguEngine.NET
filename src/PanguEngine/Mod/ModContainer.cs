using Microsoft.Extensions.Logging;
using PanguEngine.Resources;

namespace PanguEngine.Mod;

/// <summary>
/// Contains a loaded mod and its runtime services.
/// </summary>
public sealed class ModContainer
{
    internal ModContainer(ModInfo info, ModSource source, ModAssemblyLoadContext loadContext, ILogger logger,
        IResourceSource resources, IMod instance, string sourcePath)
    {
        Info = info;
        Source = source;
        LoadContext = loadContext;
        Logger = logger;
        Resources = resources;
        Instance = instance;
        SourcePath = sourcePath;
    }

    /// <summary>
    /// Gets the mod metadata.
    /// </summary>
    public ModInfo Info { get; }

    /// <summary>
    /// Gets the logger for the mod.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Gets the resources provided by the mod.
    /// </summary>
    public IResourceSource Resources { get; }

    internal ModSource Source { get; }

    internal ModAssemblyLoadContext LoadContext { get; }

    internal IMod Instance { get; }

    internal string SourcePath { get; }

    internal void Destroy()
    {
        Source.Dispose();
    }
}