namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private static readonly UiPropertyKey<Parent?> ParentPropertyKey =
        UiProperty.RegisterReadOnly<UiNode, Parent?>(nameof(Parent));

    private static readonly UiPropertyKey<UiScreen?> ScreenPropertyKey =
        UiProperty.RegisterReadOnly<UiNode, UiScreen?>(nameof(Screen));

    /// <summary>
    /// Identifies the <see cref="Parent"/> property.
    /// </summary>
    public static readonly UiProperty<Parent?> ParentProperty = ParentPropertyKey.Property;

    /// <summary>
    /// Identifies the <see cref="Screen"/> property.
    /// </summary>
    public static readonly UiProperty<UiScreen?> ScreenProperty = ScreenPropertyKey.Property;

    /// <summary>
    /// Gets the framework-maintained direct parent of this node.
    /// </summary>
    /// <remarks>
    /// The value is null when this node has no parent. Property change notifications occur after
    /// this value changes, before any resulting screen ownership change.
    /// </remarks>
    public Parent? Parent => GetValue(ParentProperty);

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
    /// <remarks>
    /// The value is null when this node has no owning screen. Property change notifications occur
    /// after this node's value changes and before its descendants are updated.
    /// </remarks>
    public UiScreen? Screen => GetValue(ScreenProperty);

    internal void SetParent(Parent? parent) =>
        SetValue(ParentPropertyKey, parent);

    internal void SetScreenRecursive(UiScreen? screen)
    {
        SetValue(ScreenPropertyKey, screen);
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