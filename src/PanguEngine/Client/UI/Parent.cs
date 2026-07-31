using System.Collections.ObjectModel;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the only UI node branch that can own child nodes.
/// </summary>
public abstract class Parent : UiNode
{
    private readonly List<UiNode> _children = [];
    private readonly ReadOnlyCollection<UiNode> _readOnlyChildren;

    /// <summary>
    /// Initializes a UI parent node.
    /// </summary>
    protected Parent()
    {
        _readOnlyChildren = _children.AsReadOnly();
    }

    /// <summary>
    /// Gets a stable read-only view of the direct children in drawing order.
    /// </summary>
    public IReadOnlyList<UiNode> Children => _readOnlyChildren;

    /// <summary>
    /// Adds a child after all existing children, moving it from another parent when necessary.
    /// </summary>
    /// <param name="child">The child to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation would duplicate a child, create a cycle, adopt an active root,
    /// or modify an active tree from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when either active tree dispatcher is shut down.
    /// </exception>
    protected void AddChild(UiNode child) =>
        InsertChild(_children.Count, child);

    /// <summary>
    /// Inserts a child at an index, moving it from another parent when necessary.
    /// </summary>
    /// <param name="index">The zero-based insertion index.</param>
    /// <param name="child">The child to insert.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside the range from zero through the child count.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation would duplicate a child, create a cycle, adopt an active root,
    /// or modify an active tree from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when either active tree dispatcher is shut down.
    /// </exception>
    protected void InsertChild(int index, UiNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if ((uint)index > (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        ValidateInsertion(child);
        var oldParent = child.Parent;
        var oldDispatcher = child.ActiveDispatcher;
        var newDispatcher = ActiveDispatcher;
        VerifyDispatchers(oldDispatcher, newDispatcher);

        if (oldParent is not null)
            oldParent._children.RemoveAt(oldParent._children.IndexOf(child));

        child.SetParent(this);
        _children.Insert(index, child);
        if (!ReferenceEquals(oldDispatcher, newDispatcher))
            child.SetActiveDispatcherRecursive(newDispatcher);

        oldParent?.InvalidateTreeStructure(child);
        InvalidateTreeStructure(child);
    }

    /// <summary>
    /// Removes a direct child from this parent.
    /// </summary>
    /// <param name="child">The child to remove.</param>
    /// <returns>Whether the node was a direct child and was removed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active tree is modified from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the active tree dispatcher is shut down.
    /// </exception>
    protected bool RemoveChild(UiNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = _children.IndexOf(child);
        if (index < 0)
            return false;

        var dispatcher = ActiveDispatcher;
        dispatcher?.VerifyAccess();
        _children.RemoveAt(index);
        child.SetParent(null);
        if (dispatcher is not null)
            child.SetActiveDispatcherRecursive(null);
        InvalidateTreeStructure(child);
        return true;
    }

    /// <summary>
    /// Removes all direct children from this parent.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active tree is modified from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the active tree dispatcher is shut down.
    /// </exception>
    protected void ClearChildren()
    {
        if (_children.Count == 0)
            return;

        var dispatcher = ActiveDispatcher;
        dispatcher?.VerifyAccess();
        foreach (var child in _children)
        {
            child.SetParent(null);
            if (dispatcher is not null)
                child.SetActiveDispatcherRecursive(null);
        }

        _children.Clear();
        InvalidateTreeStructure(this);
    }

    internal void MoveChildToFront(UiNode child)
    {
        var index = GetChildIndex(child);
        if (index == _children.Count - 1)
            return;

        ActiveDispatcher?.VerifyAccess();
        _children.RemoveAt(index);
        _children.Add(child);
        InvalidateTreeStructure(child);
    }

    internal void MoveChildToBack(UiNode child)
    {
        var index = GetChildIndex(child);
        if (index == 0)
            return;

        ActiveDispatcher?.VerifyAccess();
        _children.RemoveAt(index);
        _children.Insert(0, child);
        InvalidateTreeStructure(child);
    }

    private void ValidateInsertion(UiNode child)
    {
        if (ReferenceEquals(child.Parent, this))
            throw new InvalidOperationException("The UI node is already a child of this parent.");

        for (UiNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("Adding the UI node would create a parent cycle.");
        }

        if (child.Parent is null && child.ActiveDispatcher is not null)
            throw new InvalidOperationException("An active root must be detached before it can become a child.");
    }

    private static void VerifyDispatchers(
        UiDispatcher? oldDispatcher,
        UiDispatcher? newDispatcher)
    {
        oldDispatcher?.VerifyAccess();
        if (!ReferenceEquals(oldDispatcher, newDispatcher))
            newDispatcher?.VerifyAccess();
    }

    private int GetChildIndex(UiNode child)
    {
        var index = _children.IndexOf(child);
        if (index < 0)
            throw new InvalidOperationException("The UI node is not a child of this parent.");

        return index;
    }
}
