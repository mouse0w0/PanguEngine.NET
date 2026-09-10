using PanguEngine.Input;

namespace PanguEngine.Client.UI.Input;

/// <summary>
/// Stores and dispatches key bindings for a specific UI node type.
/// </summary>
/// <typeparam name="TNode">The type of UI node handled by the bindings.</typeparam>
/// <remarks>
/// Configure bindings before input dispatch. Instances do not provide thread synchronization.
/// </remarks>
public sealed class UiKeyBindings<TNode>
    where TNode : UiNode
{
    private readonly Dictionary<
        (Key Key, KeyModifiers Modifiers, KeyAction Action),
        Action<TNode, UiKeyEventArgs>> _bindings = [];

    /// <summary>
    /// Adds a key press binding without modifier keys.
    /// </summary>
    /// <param name="key">The key to bind.</param>
    /// <param name="handler">The callback invoked when the binding matches.</param>
    /// <returns>This binding collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An equivalent binding already exists.</exception>
    public UiKeyBindings<TNode> AddBinding(
        Key key,
        Action<TNode, UiKeyEventArgs> handler) =>
        AddBinding(key, KeyModifiers.None, KeyAction.Press, handler);

    /// <summary>
    /// Adds a key press binding with the specified modifier keys.
    /// </summary>
    /// <param name="key">The key to bind.</param>
    /// <param name="modifiers">The exact modifier keys required by the binding.</param>
    /// <param name="handler">The callback invoked when the binding matches.</param>
    /// <returns>This binding collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An equivalent binding already exists.</exception>
    public UiKeyBindings<TNode> AddBinding(
        Key key,
        KeyModifiers modifiers,
        Action<TNode, UiKeyEventArgs> handler) =>
        AddBinding(key, modifiers, KeyAction.Press, handler);

    /// <summary>
    /// Adds a key binding with the specified modifier keys and key action.
    /// </summary>
    /// <param name="key">The key to bind.</param>
    /// <param name="modifiers">The exact modifier keys required by the binding.</param>
    /// <param name="action">The key action required by the binding.</param>
    /// <param name="handler">The callback invoked when the binding matches.</param>
    /// <returns>This binding collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An equivalent binding already exists.</exception>
    public UiKeyBindings<TNode> AddBinding(
        Key key,
        KeyModifiers modifiers,
        KeyAction action,
        Action<TNode, UiKeyEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_bindings.TryAdd((key, modifiers, action), handler))
        {
            throw new InvalidOperationException(
                $"A key binding for {key}, {modifiers}, and {action} already exists.");
        }

        return this;
    }

    /// <summary>
    /// Invokes the binding that matches a keyboard event and key action.
    /// </summary>
    /// <param name="node">The node passed to the matched binding callback.</param>
    /// <param name="eventArgs">The keyboard event to match.</param>
    /// <param name="action">The key action to match.</param>
    /// <returns><see langword="true"/> if a binding was found and invoked; otherwise, <see langword="false"/>.</returns>
    public bool TryHandle(
        TNode node,
        UiKeyEventArgs eventArgs,
        KeyAction action)
    {
        if (!_bindings.TryGetValue(
                (eventArgs.Key, eventArgs.Modifiers, action),
                out var handler))
        {
            return false;
        }

        handler(node, eventArgs);
        return true;
    }
}