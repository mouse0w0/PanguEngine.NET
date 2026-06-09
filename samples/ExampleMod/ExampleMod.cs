using Microsoft.Extensions.Logging;
using PanguEngine.Mod;

namespace ExampleMod;

public sealed class ExampleModEntry : IMod
{
    public void Configure(ModContext context)
    {
        context.Logger.LogInformation("ExampleMod {Id} {Version} loaded successfully!", context.Info.Id, context.Info.Version);
    }
}
