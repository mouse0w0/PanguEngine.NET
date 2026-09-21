namespace PanguEngine.Client.UI.Huds;

/// <summary>
/// Represents a business component hosted by the persistent client HUD.
/// </summary>
/// <remarks>
/// Factories must create a new component instance for each host. The host mounts its root node,
/// drives the update callbacks, and invokes
/// <see cref="OnDestroy"/> at most once after accepting the component.
/// </remarks>
public abstract class Hud
{
    private bool _destroyed;

    /// <summary>
    /// Initializes a HUD component with its UI root node.
    /// </summary>
    /// <param name="root">The non-null root node owned by this component.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    protected Hud(UiNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    /// <summary>
    /// Gets the UI root node owned by this component.
    /// </summary>
    public UiNode Root { get; }

    /// <summary>
    /// Invoked on each client fixed update.
    /// </summary>
    protected virtual void OnFixedUpdate()
    {
    }

    /// <summary>
    /// Invoked before layout on each prepared window frame.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    protected virtual void OnFrameUpdate(double alpha)
    {
    }

    /// <summary>
    /// Invoked once when the hosting HUD closes and cleans up this accepted component.
    /// </summary>
    protected virtual void OnDestroy()
    {
    }

    internal void UpdateFixed() => OnFixedUpdate();

    internal void UpdateFrame(double alpha) => OnFrameUpdate(alpha);

    internal void DestroyHud()
    {
        if (_destroyed)
            return;

        _destroyed = true;
        OnDestroy();
    }
}
