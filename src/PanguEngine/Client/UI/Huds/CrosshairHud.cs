using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.UI.Huds;

/// <summary>
/// Provides the built-in crosshair HUD component.
/// </summary>
public sealed class CrosshairHud : Hud
{
    /// <summary>
    /// Initializes a crosshair component whose root is a new crosshair node.
    /// </summary>
    public CrosshairHud()
        : this(new Crosshair())
    {
    }

    private CrosshairHud(Crosshair crosshair)
        : base(crosshair)
    {
        Crosshair = crosshair;
    }

    /// <summary>
    /// Gets the crosshair node owned by this component.
    /// </summary>
    public Crosshair Crosshair { get; }
}
