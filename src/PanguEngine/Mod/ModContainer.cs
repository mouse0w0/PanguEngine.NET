namespace PanguEngine.Mod;

internal sealed class ModContainer(
    ModInfo info,
    ModSource source,
    ModAssemblyLoadContext loadContext,
    ModContext context,
    object instance)
{
    public ModInfo Info { get; } = info;

    public ModSource Source { get; } = source;

    public ModAssemblyLoadContext LoadContext { get; } = loadContext;

    public ModContext Context { get; } = context;

    public object Instance { get; } = instance;

    public void Destroy()
    {
        Source.Dispose();
    }
}