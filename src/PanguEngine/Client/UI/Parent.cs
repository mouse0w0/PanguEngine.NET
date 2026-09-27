using PanguEngine.ComponentModel;
using System.Collections.ObjectModel;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the only UI node branch that can own child nodes.
/// </summary>
/// <remarks>
/// Real child structure changes are rejected while the owning screen is generating drawing commands.
/// Existing operations that would not change the child list remain no-ops outside a mutation.
/// </remarks>
public abstract class Parent : UiNode
{
    /// <summary>
    /// Identifies the <see cref="ClipToBounds"/> property.
    /// </summary>
    public static readonly Property<bool> ClipToBoundsProperty =
        Property.Register<Parent, bool>(
            nameof(ClipToBounds));

    static Parent()
    {
        UiCssRegistry.RegisterElement<Parent>("Parent");
    }

    /// <summary>
    /// Initializes a UI parent node.
    /// </summary>
    protected Parent()
    {
        Children = new UiNodeList(this);
        ReadOnlyChildren = new ReadOnlyCollection<UiNode>(Children);
    }

    /// <summary>
    /// Gets a stable read-only view of the direct children in drawing order.
    /// </summary>
    public IReadOnlyList<UiNode> ReadOnlyChildren { get; }

    /// <summary>
    /// Gets the mutable collection of direct children for derived controls.
    /// </summary>
    protected internal UiNodeList Children { get; }

    /// <summary>
    /// Gets or sets whether descendants are clipped to this parent's local layout bounds.
    /// </summary>
    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    internal void MoveChildToFront(UiNode child) => Children.Move(GetChildIndex(child), Children.Count - 1);
    internal void MoveChildToBack(UiNode child) => Children.Move(GetChildIndex(child), 0);

    private int GetChildIndex(UiNode child)
    {
        var index = Children.IndexOf(child);
        if (index < 0)
            throw new InvalidOperationException("The UI node is not a child of this parent.");
        return index;
    }
}
