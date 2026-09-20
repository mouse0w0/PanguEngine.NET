using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the persistent, non-interactive client HUD.
/// </summary>
public sealed class HudScreen
{
    private readonly UiScreen _screen;
    private readonly Panel _root;
    private readonly Crosshair _crosshair;

    internal HudScreen()
    {
        _crosshair = new Crosshair();
        _root = new Panel();
        _root.Children.Add(_crosshair);
        _screen = new UiScreen(_root);
    }

    /// <summary>
    /// Gets the mutable direct children of the HUD root in drawing order.
    /// </summary>
    public UiNodeCollection Children => _root.Children;

    /// <summary>
    /// Gets the built-in configurable crosshair node.
    /// </summary>
    public Crosshair Crosshair => _crosshair;

    /// <summary>
    /// Posts a HUD tree operation to the client UI owner thread.
    /// </summary>
    /// <param name="action">The operation to execute during the next HUD frame preparation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HUD is not accepting posted actions.
    /// </exception>
    public void Post(Action action) => _screen.Post(action);

    internal UiScreen Screen => _screen;

    internal void Open() => _screen.Open();

    internal void Update() => _screen.Update();

    internal void PrepareFrame(Size viewportSize, double alpha) => _screen.PrepareFrame(viewportSize, alpha);

    internal void Close() => _screen.Close();
}
