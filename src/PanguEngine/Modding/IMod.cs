namespace PanguEngine.Modding;

/// <summary>
/// Defines a mod entry point.
/// </summary>
public interface IMod
{
    /// <summary>
    /// Configures the mod after it is loaded.
    /// </summary>
    /// <param name="context">The configure lifecycle context.</param>
    void Configure(ModConfigureContext context)
    {
    }

    /// <summary>
    /// Runs shared setup for all runtime modes.
    /// </summary>
    /// <param name="context">The common setup lifecycle context.</param>
    void CommonSetup(ModCommonSetupContext context)
    {
    }

    /// <summary>
    /// Runs client-only setup.
    /// </summary>
    /// <param name="context">The client setup lifecycle context.</param>
    void ClientSetup(ModClientSetupContext context)
    {
    }

    /// <summary>
    /// Runs dedicated-server-only setup.
    /// </summary>
    /// <param name="context">The dedicated server setup lifecycle context.</param>
    void DedicatedServerSetup(ModDedicatedServerSetupContext context)
    {
    }

    /// <summary>
    /// Runs after setup has completed for the current runtime mode.
    /// </summary>
    /// <param name="context">The ready lifecycle context.</param>
    void Ready(ModReadyContext context)
    {
    }
}