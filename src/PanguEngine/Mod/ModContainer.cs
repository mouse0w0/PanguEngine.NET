using Microsoft.Extensions.Logging;

namespace PanguEngine.Mod;

internal sealed class ModContainer(
    ModInfo info,
    ModSource source,
    ModAssemblyLoadContext loadContext,
    ModContext context,
    ILogger logger,
    object instance)
{
    public ModInfo Info { get; } = info;

    public ModSource Source { get; } = source;

    public ModAssemblyLoadContext LoadContext { get; } = loadContext;

    public ModContext Context { get; } = context;

    public ILogger Logger { get; } = logger;

    public object Instance { get; } = instance;

    public void Destroy()
    {
        DestroyInstance(Instance);
        Source.Dispose();
    }

    internal static void DestroyInstance(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else if (instance is IDisposable disposable)
            disposable.Dispose();
    }
}