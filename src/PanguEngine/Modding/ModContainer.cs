using Microsoft.Extensions.Logging;
using PanguEngine.Resources;

namespace PanguEngine.Modding;

/// <summary>
/// Contains a loaded mod and its runtime services.
/// </summary>
public sealed class ModContainer
{
    /// <summary>
    /// Initializes a loaded mod container.
    /// </summary>
    /// <param name="info">The mod metadata.</param>
    /// <param name="source">The mod source.</param>
    /// <param name="loadContext">The assembly load context for the mod.</param>
    /// <param name="logger">The logger for the mod.</param>
    /// <param name="resources">The resources provided by the mod.</param>
    /// <param name="instance">The mod entry point instance.</param>
    /// <param name="sourcePath">The original source path for the mod.</param>
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

    /// <summary>
    /// Gets the mod source.
    /// </summary>
    internal ModSource Source { get; }

    /// <summary>
    /// Gets the assembly load context for the mod.
    /// </summary>
    internal ModAssemblyLoadContext LoadContext { get; }

    /// <summary>
    /// Gets the mod entry point instance.
    /// </summary>
    internal IMod Instance { get; }

    /// <summary>
    /// Gets the original source path for the mod.
    /// </summary>
    internal string SourcePath { get; }

    /// <summary>
    /// Releases resources owned by the loaded mod.
    /// </summary>
    internal void Destroy()
    {
        Source.Dispose();
    }
}