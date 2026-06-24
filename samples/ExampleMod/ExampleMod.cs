using Microsoft.Extensions.Logging;
using PanguEngine.Modding;

namespace ExampleMod;

public sealed class ExampleModEntry : IMod
{
    public void Configure(ModConfigureContext context)
    {
        var mod = context.Mod;
        mod.Logger.LogInformation("ExampleMod {Id} {Version} loaded successfully!", mod.Info.Id, mod.Info.Version);
    }
}
