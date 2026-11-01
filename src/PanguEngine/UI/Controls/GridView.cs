using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Displays fixed-size selectable tiles with virtualized horizontal or vertical filling.</summary>
/// <typeparam name="T">The source item type.</typeparam>
public sealed class GridView<T> : ItemsView<T>
{
    /// <summary>Identifies the direction filled before wrapping.</summary>
    public static readonly Property<Orientation> FlowProperty =
        Property.Register<GridView<T>, Orientation>(nameof(Flow), Orientation.Horizontal,
            onChanged: static (view, _, _) => view.LayoutChanged(true));

    /// <summary>Identifies the fixed tile width.</summary>
    public static readonly Property<double> ItemWidthProperty = RegisterSize(nameof(ItemWidth), 96);

    /// <summary>Identifies the fixed tile height.</summary>
    public static readonly Property<double> ItemHeightProperty = RegisterSize(nameof(ItemHeight), 96);

    /// <summary>Identifies the spacing between rows.</summary>
    public static readonly Property<double> RowSpacingProperty = RegisterSpacing(nameof(RowSpacing));

    /// <summary>Identifies the spacing between columns.</summary>
    public static readonly Property<double> ColumnSpacingProperty = RegisterSpacing(nameof(ColumnSpacing));

    static GridView()
    {
        CssRegistry.RegisterElement<GridView<T>>("GridView");
        CssRegistry.RegisterProperty<GridView<T>, Orientation>("flow", FlowProperty, CssValueConverters.ParseOrientation);
        CssRegistry.RegisterProperty<GridView<T>, double>("item-width", ItemWidthProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<GridView<T>, double>("item-height", ItemHeightProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<GridView<T>, double>("row-spacing", RowSpacingProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<GridView<T>, double>("column-spacing", ColumnSpacingProperty, CssValueConverters.ParseLength);
    }

    /// <summary>Gets or sets the direction filled before wrapping; scrolling uses the other axis.</summary>
    public Orientation Flow
    {
        get => GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    /// <summary>Gets or sets the fixed tile width in logical pixels.</summary>
    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>Gets or sets the fixed tile height in logical pixels.</summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>Gets or sets the spacing between rows in logical pixels.</summary>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Gets or sets the spacing between columns in logical pixels.</summary>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    private static Property<double> RegisterSize(string name, double defaultValue) =>
        Property.Register<GridView<T>, double>(name, defaultValue,
            onChanged: static (view, _, _) => view.LayoutChanged(false),
            validate: static value => double.IsFinite(value) && value > 0,
            validationMessage: "Item dimensions must be finite and positive.");

    private static Property<double> RegisterSpacing(string name) =>
        Property.Register<GridView<T>, double>(name,
            onChanged: static (view, _, _) => view.LayoutChanged(false),
            validate: static value => double.IsFinite(value) && value >= 0,
            validationMessage: "Item spacing must be finite and non-negative.");

    internal override ItemsViewLayout CreateLayout(Size viewport, int count) =>
        ItemsViewLayout.CreateGrid(count, viewport, Flow, ItemWidth, ItemHeight, RowSpacing, ColumnSpacing, Scene);
}
