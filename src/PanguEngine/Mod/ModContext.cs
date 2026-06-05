using Microsoft.Extensions.Logging;

namespace PanguEngine.Mod;

public sealed class ModContext
{
    internal ModContext(ModInfo info, ILogger logger, ModAssetProvider assets)
    {
        Info = info;
        Logger = logger;
        Assets = assets;
    }

    public ModInfo Info { get; }

    public ILogger Logger { get; }

    public ModAssetProvider Assets { get; }
}