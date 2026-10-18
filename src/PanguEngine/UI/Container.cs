using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

/// <summary>
/// Provides the only UI element branch that can own child elements.
/// </summary>
/// <remarks>
/// Real child structure changes are rejected while the owning screen is generating drawing commands.
/// Existing operations that would not change the child list remain no-ops outside a mutation.
/// </remarks>
public abstract class Container : UIElement
{
    /// <summary>
    /// Identifies the <see cref="ClipToBounds"/> property.
    /// </summary>
    public static readonly Property<bool> ClipToBoundsProperty =
        Property.Register<Container, bool>(
            nameof(ClipToBounds));

    static Container()
    {
        CssRegistry.RegisterElement<Container>("Container");
    }

    /// <summary>
    /// Initializes a UI container.
    /// </summary>
    protected Container()
    {
        Children = new UIElementList(this);
        ReadOnlyChildren = Children.AsReadOnly();
    }

    /// <summary>
    /// Gets a stable read-only view of the direct children in drawing order.
    /// </summary>
    public IReadOnlyObservableList<UIElement> ReadOnlyChildren { get; }

    /// <summary>
    /// Gets the mutable collection of direct children for derived controls.
    /// </summary>
    protected internal UIElementList Children { get; }

    /// <inheritdoc />
    protected override IReadOnlyList<StyledObject> InheritanceChildren => ReadOnlyChildren;

    /// <summary>
    /// Gets or sets whether descendants are clipped to this container's local layout bounds.
    /// </summary>
    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    internal virtual DrawCommand CreateDescendantClip() =>
        new PushClipCommand(new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height));

    internal virtual bool ContainsDescendantClip(Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X < LayoutBounds.Width && point.Y < LayoutBounds.Height;

    internal void MoveChildToFront(UIElement child) => Children.Move(GetChildIndex(child), Children.Count - 1);
    internal void MoveChildToBack(UIElement child) => Children.Move(GetChildIndex(child), 0);

    private int GetChildIndex(UIElement child)
    {
        var index = Children.IndexOf(child);
        if (index < 0)
            throw new InvalidOperationException("The UI node is not a child of this parent.");
        return index;
    }
}