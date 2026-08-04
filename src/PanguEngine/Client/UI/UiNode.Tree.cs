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

    /// <summary>
    /// Gets the active screen that contains this node.
    /// </summary>
    public Screen? ActiveScreen { get; private set; }

    internal void AttachToTree(UiDispatcher dispatcher, Screen? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (Parent is not null)
            throw new InvalidOperationException("Only a root node can be attached to an active UI tree.");
        if (ActiveDispatcher is not null)
            throw new InvalidOperationException("The UI node is already attached to an active UI tree.");

        dispatcher.VerifyAccess();
        SetActiveTreeRecursive(dispatcher, screen);
    }

    internal void DetachFromTree()
    {
        if (Parent is not null)
            throw new InvalidOperationException("Only a root node can be detached from an active UI tree.");
        var dispatcher = ActiveDispatcher;
        if (dispatcher is null)
            return;

        dispatcher.VerifyAccess();
        var detachedNodes = new List<UiNode>();
        CollectSubtree(detachedNodes);
        var screen = ActiveScreen;
        SetActiveTreeRecursive(null, null);
        screen?.HandleSubtreesDetached(detachedNodes);
    }

    internal void SetParent(Parent? parent) =>
        Parent = parent;

    internal void SetActiveTreeRecursive(UiDispatcher? dispatcher, Screen? screen)
    {
        ActiveDispatcher = dispatcher;
        ActiveScreen = screen;
        if (this is not Parent parent)
            return;

        foreach (var child in parent.Children)
            child.SetActiveTreeRecursive(dispatcher, screen);
    }

    internal void CollectSubtree(List<UiNode> nodes)
    {
        nodes.Add(this);
        if (this is not Parent parent)
            return;

        foreach (var child in parent.Children)
            child.CollectSubtree(nodes);
    }

    internal void InvalidateTreeStructure(UiNode source)
    {
        for (UiNode? node = this; node is not null; node = node.Parent)
            node.OnTreeStructureInvalidated(source);
    }

    partial void OnTreeStructureInvalidated(UiNode source);
}
