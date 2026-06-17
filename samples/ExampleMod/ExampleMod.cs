using Microsoft.Extensions.Logging;
using PanguEngine.Mod;

namespace ExampleMod;

public sealed class ExampleModEntry : IMod
{
    public void Configure(ModContainer container)
    {
        container.Logger.LogInformation("ExampleMod {Id} {Version} loaded successfully!", container.Info.Id, container.Info.Version);
    }
}
