namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    /// <summary>
    /// Gets the framework-maintained direct parent of this node.
    /// </summary>
    public Parent? Parent { get; private set; }

    /// <summary>
    /// Moves this node to the visual front of its siblings.
    /// </summary>
    /// <remarks>
    /// The node becomes the last child in drawing order. Calling this method without a parent or
    /// while already at the front has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active tree is modified from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the active tree dispatcher is shut down.
    /// </exception>
    public void MoveToFront() =>
        Parent?.MoveChildToFront(this);

    /// <summary>
    /// Moves this node to the visual back of its siblings.
    /// </summary>
    /// <remarks>
    /// The node becomes the first child in drawing order. Calling this method without a parent or
    /// while already at the back has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active tree is modified from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the active tree dispatcher is shut down.
    /// </exception>
    public void MoveToBack() =>
        Parent?.MoveChildToBack(this);

    internal UiDispatcher? ActiveDispatcher { get; private set; }

    internal void AttachToTree(UiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (Parent is not null)
            throw new InvalidOperationException("Only a root node can be attached to an active UI tree.");
        if (ActiveDispatcher is not null)
            throw new InvalidOperationException("The UI node is already attached to an active UI tree.");

        dispatcher.VerifyAccess();
        SetActiveDispatcherRecursive(dispatcher);
    }

    internal void DetachFromTree()
    {
        if (Parent is not null)
            throw new InvalidOperationException("Only a root node can be detached from an active UI tree.");
        var dispatcher = ActiveDispatcher;
        if (dispatcher is null)
            return;

        dispatcher.VerifyAccess();
        SetActiveDispatcherRecursive(null);
    }

    internal void SetParent(Parent? parent) =>
        Parent = parent;

    internal void SetActiveDispatcherRecursive(UiDispatcher? dispatcher)
    {
        ActiveDispatcher = dispatcher;
        if (this is not Parent parent)
            return;

        foreach (var child in parent.Children)
            child.SetActiveDispatcherRecursive(dispatcher);
    }

    internal void InvalidateTreeStructure(UiNode source)
    {
        for (UiNode? node = this; node is not null; node = node.Parent)
            node.OnTreeStructureInvalidated(source);
    }

    partial void OnTreeStructureInvalidated(UiNode source);
}
