namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a UI screen with a retained root node.
/// </summary>
public partial class Screen
{
    private UiManager? _owner;

    /// <summary>
    /// Initializes a UI screen with its root node.
    /// </summary>
    /// <param name="root">The root node of the screen.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    public Screen(UiNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    /// <summary>
    /// Gets the root node of this screen.
    /// </summary>
    public UiNode Root { get; }

    /// <summary>
    /// Invoked before this screen is attached and becomes current.
    /// </summary>
    /// <param name="manager">The manager opening this screen.</param>
    protected virtual void OnOpening(UiManager manager)
    {
    }

    /// <summary>
    /// Invoked after this screen is attached and becomes current.
    /// </summary>
    /// <param name="manager">The manager that opened this screen.</param>
    protected virtual void OnOpened(UiManager manager)
    {
    }

    /// <summary>
    /// Invoked before this screen stops being current and is detached.
    /// </summary>
    /// <param name="manager">The manager closing this screen.</param>
    protected virtual void OnClosing(UiManager manager)
    {
    }

    /// <summary>
    /// Invoked after this screen is detached and stops being current.
    /// </summary>
    /// <param name="manager">The manager that closed this screen.</param>
    protected virtual void OnClosed(UiManager manager)
    {
    }

    internal bool TryClaim(UiManager manager) =>
        Interlocked.CompareExchange(ref _owner, manager, null) is null;

    internal void Release(UiManager manager)
    {
        if (!ReferenceEquals(
                Interlocked.CompareExchange(ref _owner, null, manager),
                manager))
        {
            throw new InvalidOperationException("The UI screen is not owned by this manager.");
        }
    }

    internal void InvokeOpening(UiManager manager) =>
        OnOpening(manager);

    internal void InvokeOpened(UiManager manager) =>
        OnOpened(manager);

    internal void InvokeClosing(UiManager manager) =>
        OnClosing(manager);

    internal void InvokeClosed(UiManager manager) =>
        OnClosed(manager);
}
