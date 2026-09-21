namespace PanguEngine.Client.UI;

/// <summary>
/// Describes a HUD component that the client host can create.
/// </summary>
/// <remarks>
/// The definition stores a factory only. Registering a definition never executes the factory or
/// creates UI; the host invokes the factory at most once per successfully initialized HUD.
/// </remarks>
public sealed class HudDefinition
{
    private readonly Func<Hud> _factory;

    /// <summary>
    /// Initializes a definition with the factory that creates the HUD component.
    /// </summary>
    /// <param name="factory">The factory that creates a new component instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public HudDefinition(Func<Hud> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    internal Hud Create() => _factory();
}
