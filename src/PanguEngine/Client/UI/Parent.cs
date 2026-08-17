using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the only UI node branch that can own child nodes.
/// </summary>
/// <remarks>
/// Real child structure changes are rejected while the owning screen is generating drawing commands.
/// Existing operations that would not change the child list remain no-ops.
/// </remarks>
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
    /// Thrown when the operation would duplicate a child, create a cycle, adopt a UI screen root,
    /// or modify a tree owned by an open screen from the wrong thread.
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
    /// Thrown when the operation would duplicate a child, create a cycle, adopt a UI screen root,
    /// or modify a tree owned by an open screen from the wrong thread.
    /// </exception>
    protected void InsertChild(int index, UiNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if ((uint)index > (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        ValidateInsertion(child);
        var oldParent = child.Parent;
        var oldScreen = child.Screen;
        var newScreen = Screen;
        VerifyScreens(oldScreen, newScreen);
        var affectedScreens = new List<UiScreen>();
        if (oldScreen is not null && !ReferenceEquals(oldScreen, newScreen))
            AddAffectedScreen(affectedScreens, oldScreen);

        var activeScreens = BeginRuntimeOperations(affectedScreens);
        try
        {
            oldParent?._children.RemoveAt(oldParent._children.IndexOf(child));

            child.SetParent(this);
            _children.Insert(index, child);
            if (!ReferenceEquals(oldScreen, newScreen))
                child.SetScreenRecursive(newScreen);

            if (!ReferenceEquals(oldScreen, child.Screen))
                child.InvalidateMeasureSubtree();
            oldParent?.InvalidateTreeStructure();
            InvalidateTreeStructure();
            CommitAndNotifyInputState(activeScreens);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
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
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
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

    internal void RemoveChildForRootTransfer(UiNode child)
    {
        var index = GetChildIndex(child);
        _children.RemoveAt(index);
        child.SetParent(null);
        InvalidateTreeStructure();
    }

    /// <summary>
    /// Removes all direct children from this parent.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
    /// </exception>
    protected void ClearChildren()
    {
        if (_children.Count == 0)
            return;

        var screen = Screen;
        screen?.VerifyTreeMutationAccess();
        var oldScreens = _children
            .Select(child => (Child: child, Screen: child.Screen))
            .ToArray();
        var affectedScreens = new List<UiScreen>();
        AddAffectedScreen(affectedScreens, screen);
        var activeScreens = BeginRuntimeOperations(affectedScreens);

        try
        {
            foreach (var child in _children)
            {
                child.SetParent(null);
                if (screen is not null)
                    child.SetScreenRecursive(null);
            }

            _children.Clear();
            foreach (var (child, oldScreen) in oldScreens)
            {
                if (!ReferenceEquals(oldScreen, child.Screen))
                    child.InvalidateMeasureSubtree();
            }
            InvalidateTreeStructure();
            CommitAndNotifyInputState(activeScreens);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
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
        var oldScreen = child.Screen;
        var newScreen = Screen;
        VerifyScreens(oldScreen, newScreen);
        var replacedScreen = replacedChild.Screen;
        var affectedScreens = new List<UiScreen>();
        if (oldScreen is not null && !ReferenceEquals(oldScreen, newScreen))
            AddAffectedScreen(affectedScreens, oldScreen);
        if (replacedChild.Screen is not null)
            AddAffectedScreen(affectedScreens, replacedChild.Screen);

        var activeScreens = BeginRuntimeOperations(affectedScreens);
        try
        {
            oldParent?._children.RemoveAt(oldParent._children.IndexOf(child));
            child.SetParent(this);
            _children[index] = child;
            if (!ReferenceEquals(oldScreen, newScreen))
                child.SetScreenRecursive(newScreen);

            replacedChild.SetParent(null);
            if (newScreen is not null)
                replacedChild.SetScreenRecursive(null);

            if (!ReferenceEquals(oldScreen, child.Screen))
                child.InvalidateMeasureSubtree();
            if (!ReferenceEquals(replacedScreen, replacedChild.Screen))
                replacedChild.InvalidateMeasureSubtree();
            oldParent?.InvalidateTreeStructure();
            InvalidateTreeStructure();
            CommitAndNotifyInputState(activeScreens);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
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
        var screen = child.Screen;
        Screen?.VerifyTreeMutationAccess();
        var affectedScreens = new List<UiScreen>();
        AddAffectedScreen(affectedScreens, screen);
        var activeScreens = BeginRuntimeOperations(affectedScreens);
        try
        {
            _children.RemoveAt(index);
            child.SetParent(null);
            if (screen is not null)
                child.SetScreenRecursive(null);
            if (!ReferenceEquals(screen, child.Screen))
                child.InvalidateMeasureSubtree();
            InvalidateTreeStructure();
            CommitAndNotifyInputState(activeScreens);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
    }

    private void MoveChild(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        Screen?.VerifyTreeMutationAccess();
        var preserveHitTestLayout = CanPreserveHitTestLayoutAfterChildOrderChange();
        var child = _children[oldIndex];
        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, child);
        InvalidateTreeStructure();
        if (preserveHitTestLayout)
            RestoreHitTestLayoutAfterChildOrderChange();
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

        if (child.Parent is null && child.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can become a child.");
    }

    private static void VerifyScreens(UiScreen? oldScreen, UiScreen? newScreen)
    {
        oldScreen?.VerifyTreeMutationAccess();
        if (!ReferenceEquals(oldScreen, newScreen))
            newScreen?.VerifyTreeMutationAccess();
    }

    private static void AddAffectedScreen(List<UiScreen> screens, UiScreen? screen)
    {
        if (screen is not null && !screens.Contains(screen))
            screens.Add(screen);
    }

    private static List<UiScreen> BeginRuntimeOperations(List<UiScreen> screens)
    {
        var activeScreens = new List<UiScreen>(screens.Count);
        try
        {
            foreach (var screen in screens)
            {
                if (screen.BeginRuntimeOperationIfOpen())
                    activeScreens.Add(screen);
            }
        }
        catch
        {
            EndRuntimeOperations(activeScreens);
            throw;
        }

        return activeScreens;
    }

    private static void EndRuntimeOperations(List<UiScreen> screens)
    {
        for (var index = screens.Count - 1; index >= 0; index--)
            screens[index].EndRuntimeOperation();
    }

    private static void CommitAndNotifyInputState(List<UiScreen> screens)
    {
        var snapshots = new List<(UiScreen Screen, UiScreen.InputStateCleanupSnapshot Snapshot)>();
        foreach (var screen in screens)
        {
            if (screen.CommitInputStateAfterTreeChange() is { } snapshot)
                snapshots.Add((screen, snapshot));
        }

        var errors = new List<Exception>();
        foreach (var (screen, snapshot) in snapshots)
        {
            try
            {
                screen.NotifyInputStateLoss(snapshot);
            }
            catch (Exception exception)
            {
                errors.AddRange(exception switch
                {
                    AggregateException aggregate => aggregate.InnerExceptions,
                    _ => [exception]
                });
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
