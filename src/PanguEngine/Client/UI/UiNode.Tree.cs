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
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
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
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
    /// </exception>
    public void MoveToBack() =>
        Parent?.MoveChildToBack(this);

    /// <summary>
    /// Gets the UI screen that owns this node.
    /// </summary>
    public UiScreen? Screen { get; private set; }

    internal void SetParent(Parent? parent) =>
        Parent = parent;

    internal void SetScreenRecursive(UiScreen? screen)
    {
        Screen = screen;
        if (this is not Parent parent)
            return;

        foreach (var child in parent.Children)
            child.SetScreenRecursive(screen);
    }

    internal void InvalidateTreeStructure()
    {
        for (var node = this; node is not null; node = node.Parent)
            node.OnTreeStructureInvalidated();
    }

    partial void OnTreeStructureInvalidated();
}
