using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the only UI node branch that can own child nodes.
/// </summary>
public abstract class Parent : UiNode
{
    /// <summary>
    /// Identifies the <see cref="ClipToBounds"/> property.
    /// </summary>
    public static readonly UiProperty<bool> ClipToBoundsProperty =
        UiProperty.Register<Parent, bool>(
            nameof(ClipToBounds),
            invalidation: UiPropertyInvalidation.Input | UiPropertyInvalidation.Render);

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
    /// Gets or sets whether descendants are clipped to this parent's local layout bounds.
    /// </summary>
    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

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

    internal void AddChildFromCollection(UiNode child) =>
        AddChild(child);

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
        var oldScreen = child.ActiveScreen;
        var newDispatcher = ActiveDispatcher;
        var newScreen = ActiveScreen;
        VerifyDispatchers(oldDispatcher, newDispatcher);
        var detachedGroups = new Dictionary<Screen, List<UiNode>>(ReferenceEqualityComparer.Instance);
        if (oldScreen is not null && !ReferenceEquals(oldScreen, newScreen))
            AddDetachedNodes(detachedGroups, oldScreen, child);

        if (oldParent is not null)
            oldParent._children.RemoveAt(oldParent._children.IndexOf(child));

        child.SetParent(this);
        _children.Insert(index, child);
        if (!ReferenceEquals(oldDispatcher, newDispatcher) || !ReferenceEquals(oldScreen, newScreen))
            child.SetActiveTreeRecursive(newDispatcher, newScreen);

        oldParent?.InvalidateTreeStructure(child);
        InvalidateTreeStructure(child);
        NotifyDetached(detachedGroups);
    }

    internal void InsertChildFromCollection(int index, UiNode child) =>
        InsertChild(index, child);

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

        RemoveChildAt(index);
        return true;
    }

    internal bool RemoveChildFromCollection(UiNode child) =>
        RemoveChild(child);

    internal void RemoveChildAtFromCollection(int index)
    {
        VerifyChildIndex(index);
        RemoveChildAt(index);
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
        var detachedGroups = new Dictionary<Screen, List<UiNode>>(ReferenceEqualityComparer.Instance);
        foreach (var child in _children)
        {
            if (child.ActiveScreen is not null)
                AddDetachedNodes(detachedGroups, child.ActiveScreen, child);
        }

        foreach (var child in _children)
        {
            child.SetParent(null);
            if (dispatcher is not null)
                child.SetActiveTreeRecursive(null, null);
        }

        _children.Clear();
        InvalidateTreeStructure(this);
        NotifyDetached(detachedGroups);
    }

    internal void ClearChildrenFromCollection() =>
        ClearChildren();

    internal void ReplaceChildFromCollection(int index, UiNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        VerifyChildIndex(index);
        var replacedChild = _children[index];
        if (ReferenceEquals(replacedChild, child))
            return;

        ValidateInsertion(child);
        var oldParent = child.Parent;
        var oldDispatcher = child.ActiveDispatcher;
        var oldScreen = child.ActiveScreen;
        var newDispatcher = ActiveDispatcher;
        var newScreen = ActiveScreen;
        VerifyDispatchers(oldDispatcher, newDispatcher);
        var detachedGroups = new Dictionary<Screen, List<UiNode>>(ReferenceEqualityComparer.Instance);
        if (oldScreen is not null && !ReferenceEquals(oldScreen, newScreen))
            AddDetachedNodes(detachedGroups, oldScreen, child);
        if (replacedChild.ActiveScreen is not null)
            AddDetachedNodes(detachedGroups, replacedChild.ActiveScreen, replacedChild);
        if (oldParent is not null)
            oldParent._children.RemoveAt(oldParent._children.IndexOf(child));

        child.SetParent(this);
        _children[index] = child;
        if (!ReferenceEquals(oldDispatcher, newDispatcher) || !ReferenceEquals(oldScreen, newScreen))
            child.SetActiveTreeRecursive(newDispatcher, newScreen);

        replacedChild.SetParent(null);
        if (newDispatcher is not null)
            replacedChild.SetActiveTreeRecursive(null, null);

        oldParent?.InvalidateTreeStructure(child);
        InvalidateTreeStructure(child);
        NotifyDetached(detachedGroups);
    }

    internal void MoveChildFromCollection(int oldIndex, int newIndex)
    {
        VerifyChildIndex(oldIndex);
        VerifyChildIndex(newIndex);
        MoveChild(oldIndex, newIndex);
    }

    internal void MoveChildToFront(UiNode child)
    {
        var index = GetChildIndex(child);
        MoveChild(index, _children.Count - 1);
    }

    internal void MoveChildToBack(UiNode child)
    {
        var index = GetChildIndex(child);
        MoveChild(index, 0);
    }

    private void RemoveChildAt(int index)
    {
        var child = _children[index];
        var dispatcher = ActiveDispatcher;
        dispatcher?.VerifyAccess();
        var screen = child.ActiveScreen;
        var detachedNodes = screen is null ? null : SnapshotSubtree(child);
        _children.RemoveAt(index);
        child.SetParent(null);
        if (dispatcher is not null)
            child.SetActiveTreeRecursive(null, null);
        InvalidateTreeStructure(child);
        screen?.HandleSubtreesDetached(detachedNodes!);
    }

    private void MoveChild(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        ActiveDispatcher?.VerifyAccess();
        var child = _children[oldIndex];
        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, child);
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

    private static List<UiNode> SnapshotSubtree(UiNode root)
    {
        var nodes = new List<UiNode>();
        root.CollectSubtree(nodes);
        return nodes;
    }

    private static void AddDetachedNodes(
        Dictionary<Screen, List<UiNode>> groups,
        Screen screen,
        UiNode root)
    {
        if (!groups.TryGetValue(screen, out var nodes))
        {
            nodes = [];
            groups.Add(screen, nodes);
        }

        root.CollectSubtree(nodes);
    }

    private static void NotifyDetached(Dictionary<Screen, List<UiNode>> groups)
    {
        if (groups.Count == 0)
            return;

        var errors = new List<Exception>();
        foreach (var (screen, nodes) in groups)
        {
            try
            {
                screen.HandleSubtreesDetached(nodes);
            }
            catch (Exception exception)
            {
                if (exception is AggregateException aggregate)
                    errors.AddRange(aggregate.InnerExceptions);
                else
                    errors.Add(exception);
            }
        }

        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private int GetChildIndex(UiNode child)
    {
        var index = _children.IndexOf(child);
        if (index < 0)
            throw new InvalidOperationException("The UI node is not a child of this parent.");

        return index;
    }

    private void VerifyChildIndex(int index)
    {
        if ((uint)index >= (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
