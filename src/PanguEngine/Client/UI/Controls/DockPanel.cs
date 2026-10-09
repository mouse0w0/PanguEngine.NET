using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Arranges children in collection order along the edges of the remaining content area.
/// </summary>
public sealed class DockPanel : Panel
{
    /// <summary>
    /// Identifies the attached dock direction property, whose default value is <see cref="Dock.Left"/>.
    /// </summary>
    public static readonly Property<Dock> DockProperty =
        Property.RegisterAttached<DockPanel, UiNode, Dock>(
            "Dock",
            Dock.Left,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="LastChildFill"/> property.
    /// </summary>
    public static readonly Property<bool> LastChildFillProperty =
        Property.Register<DockPanel, bool>(
            nameof(LastChildFill),
            true,
            onChanged: static (node, _, _) => node.InvalidateArrange());

    static DockPanel()
    {
        UiCssRegistry.RegisterElement<DockPanel>("DockPanel");
        UiCssRegistry.RegisterProperty<UiNode, Dock>("dock", DockProperty, UiCssValueConverters.ParseDock);
        UiCssRegistry.RegisterProperty<DockPanel, bool>(
            "last-child-fill", LastChildFillProperty, UiCssValueConverters.ParseBool);
    }

    /// <summary>
    /// Gets or sets whether the last child in the collection receives the entire remaining content area.
    /// The default value is true.
    /// </summary>
    /// <remarks>
    /// The last child's dock direction is ignored when this property is true. Its size constraints,
    /// margin, and alignment still apply within the allocated area. A collapsed last child does not
    /// transfer the remaining area to another child.
    /// </remarks>
    public bool LastChildFill
    {
        get => GetValue(LastChildFillProperty);
        set => SetValue(LastChildFillProperty, value);
    }

    /// <summary>
    /// Gets the edge to which a child is docked, defaulting to <see cref="Dock.Left"/>.
    /// </summary>
    /// <param name="node">The node whose dock direction to read.</param>
    /// <returns>The effective dock direction.</returns>
    public static Dock GetDock(UiNode node) => node.GetValue(DockProperty);

    /// <summary>
    /// Sets the edge to which a child is docked within a DockPanel.
    /// </summary>
    /// <param name="node">The node whose dock direction to set.</param>
    /// <param name="value">The dock direction.</param>
    public static void SetDock(UiNode node, Dock value) => node.SetValue(DockProperty, value);

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var consumedWidth = 0d;
        var consumedHeight = 0d;
        var desiredWidth = 0d;
        var desiredHeight = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(
                Math.Max(0, availableSize.Width - consumedWidth),
                Math.Max(0, availableSize.Height - consumedHeight)));
            if (child.Visibility == Visibility.Collapsed)
                continue;

            var childSize = child.DesiredSize;
            switch (GetDock(child))
            {
                case Dock.Left:
                case Dock.Right:
                    desiredHeight = Math.Max(desiredHeight, AddFinite(consumedHeight, childSize.Height));
                    consumedWidth = AddFinite(consumedWidth, childSize.Width);
                    break;

                case Dock.Top:
                case Dock.Bottom:
                    desiredWidth = Math.Max(desiredWidth, AddFinite(consumedWidth, childSize.Width));
                    consumedHeight = AddFinite(consumedHeight, childSize.Height);
                    break;
            }
        }

        return new Size(
            Math.Max(desiredWidth, consumedWidth),
            Math.Max(desiredHeight, consumedHeight));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var x = contentBounds.X;
        var y = contentBounds.Y;
        var width = contentBounds.Width;
        var height = contentBounds.Height;
        var fillIndex = LastChildFill ? Children.Count - 1 : -1;
        var index = 0;

        foreach (var child in Children)
        {
            var fillsRemainingSpace = index++ == fillIndex;
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            var slot = new Rect(x, y, width, height);
            if (!fillsRemainingSpace)
            {
                var childSize = child.DesiredSize;
                switch (GetDock(child))
                {
                    case Dock.Left:
                    {
                        var childWidth = Math.Min(childSize.Width, width);
                        slot = new Rect(x, y, childWidth, height);
                        x += childWidth;
                        width -= childWidth;
                        break;
                    }

                    case Dock.Top:
                    {
                        var childHeight = Math.Min(childSize.Height, height);
                        slot = new Rect(x, y, width, childHeight);
                        y += childHeight;
                        height -= childHeight;
                        break;
                    }

                    case Dock.Right:
                    {
                        var childWidth = Math.Min(childSize.Width, width);
                        width -= childWidth;
                        slot = new Rect(x + width, y, childWidth, height);
                        break;
                    }

                    case Dock.Bottom:
                    {
                        var childHeight = Math.Min(childSize.Height, height);
                        height -= childHeight;
                        slot = new Rect(x, y + height, width, childHeight);
                        break;
                    }
                }
            }

            child.Arrange(slot);
        }
    }

    private static double AddFinite(double value, double addition)
    {
        var result = value + addition;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("DockPanel measurement produced a non-finite desired size.");

        return result;
    }
}
