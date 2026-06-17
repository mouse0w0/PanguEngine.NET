using Microsoft.Extensions.Logging;

namespace PanguEngine.Mod;

public sealed class ModContainer
{
    internal ModContainer(ModInfo info, ModSource source, ModAssemblyLoadContext loadContext, ILogger logger,
        ModAssetProvider assets, IMod instance, string sourcePath)
    {
        Info = info;
        Source = source;
        LoadContext = loadContext;
        Logger = logger;
        Assets = assets;
        Instance = instance;
        SourcePath = sourcePath;
    }

    public ModInfo Info { get; }

    public ILogger Logger { get; }

    public ModAssetProvider Assets { get; }

    internal ModSource Source { get; }

    internal ModAssemblyLoadContext LoadContext { get; }

    internal IMod Instance { get; }

    internal string SourcePath { get; }

    internal void Destroy()
    {
        Source.Dispose();
    }
}