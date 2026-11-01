using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Displays a fixed-size, virtualized row or column of selectable data items.</summary>
/// <typeparam name="T">The source item type.</typeparam>
public sealed class ListView<T> : ItemsView<T>
{
    /// <summary>Identifies the arrangement and scrolling direction.</summary>
    public static readonly Property<Orientation> OrientationProperty =
        Property.Register<ListView<T>, Orientation>(nameof(Orientation), Orientation.Vertical,
            onChanged: static (view, _, _) => view.LayoutChanged(true));

    /// <summary>Identifies the fixed item size along the scrolling axis.</summary>
    public static readonly Property<double> ItemExtentProperty =
        Property.Register<ListView<T>, double>(nameof(ItemExtent), 32,
            onChanged: static (view, _, _) => view.LayoutChanged(false),
            validate: static value => double.IsFinite(value) && value > 0,
            validationMessage: "ItemExtent must be finite and positive.");

    /// <summary>Identifies the space between adjacent items.</summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<ListView<T>, double>(nameof(Spacing),
            onChanged: static (view, _, _) => view.LayoutChanged(false),
            validate: static value => double.IsFinite(value) && value >= 0,
            validationMessage: "Spacing must be finite and non-negative.");

    static ListView()
    {
        CssRegistry.RegisterElement<ListView<T>>("ListView");
        CssRegistry.RegisterProperty<ListView<T>, Orientation>("orientation", OrientationProperty,
            CssValueConverters.ParseOrientation);
        CssRegistry.RegisterProperty<ListView<T>, double>("item-extent", ItemExtentProperty,
            CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<ListView<T>, double>("spacing", SpacingProperty,
            CssValueConverters.ParseLength);
    }

    /// <summary>Gets or sets the arrangement and scrolling direction.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets the fixed item size along the scrolling axis in logical pixels.</summary>
    public double ItemExtent
    {
        get => GetValue(ItemExtentProperty);
        set => SetValue(ItemExtentProperty, value);
    }

    /// <summary>Gets or sets the space between adjacent items in logical pixels.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    internal override ItemsViewLayout CreateLayout(Size viewport, int count) =>
        ItemsViewLayout.CreateList(count, viewport, Orientation, ItemExtent, Spacing, Scene);
}
