using PanguEngine.Client.UI.Styling;
using PanguEngine.Client.UI.Selection;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Presents a set of selectable tab items with a single active content page.
/// </summary>
/// <remarks>
/// The view owns a dedicated <see cref="TabItemCollection"/>, coordinates selection and content
/// visibility, lays out the strip in four directions with overflow scrolling, and optionally allows
/// in-view drag reordering. Only the selected content host is measured, arranged, drawn, and hit tested.
/// </remarks>
public sealed partial class TabView : Control
{
    internal const double DragThreshold = 6;
    internal const double WheelSmallChange = 16;
    internal const double AutoScrollEdge = 24;
    internal const double AutoScrollSpeed = 360;

    /// <summary>Identifies the <see cref="TabStripPlacement"/> property.</summary>
    public static readonly Property<TabStripPlacement> TabStripPlacementProperty =
        Property.Register<TabView, TabStripPlacement>(
            nameof(TabStripPlacement),
            TabStripPlacement.Top,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>Identifies the <see cref="CanReorderTabs"/> property.</summary>
    public static readonly Property<bool> CanReorderTabsProperty =
        Property.Register<TabView, bool>(
            nameof(CanReorderTabs),
            false);

    /// <summary>Identifies the <see cref="MinTabWidth"/> property.</summary>
    public static readonly Property<double> MinTabWidthProperty =
        Property.Register<TabView, double>(
            nameof(MinTabWidth),
            96d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "MinTabWidth must be a finite non-negative value.");

    /// <summary>Identifies the <see cref="MaxTabWidth"/> property.</summary>
    public static readonly Property<double> MaxTabWidthProperty =
        Property.Register<TabView, double>(
            nameof(MaxTabWidth),
            240d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "MaxTabWidth must be a finite non-negative value.");

    /// <summary>Identifies the <see cref="VerticalTabStripWidth"/> property.</summary>
    public static readonly Property<double> VerticalTabStripWidthProperty =
        Property.Register<TabView, double>(
            nameof(VerticalTabStripWidth),
            200d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "VerticalTabStripWidth must be a finite non-negative value.");

    static TabView()
    {
        UiCssRegistry.RegisterElement<TabView>("TabView");
        UiCssRegistry.RegisterProperty<TabView, double>(
            "min-tab-width", MinTabWidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<TabView, double>(
            "max-tab-width", MaxTabWidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<TabView, double>(
            "vertical-tab-strip-width", VerticalTabStripWidthProperty, UiCssValueConverters.ParseLength);
    }

    private readonly TabStripPanel _strip;
    private readonly TabContentArea _contentArea;
    private readonly UiTicker _dragTicker;
    private int _collectionMutationDepth;
    private int _selectionUpdateDepth;
    private bool _collectionNotificationPending;

    /// <summary>Initializes an empty tab view.</summary>
    public TabView()
    {
        _dragTicker = CreateTicker(OnDragTick);
        Items = new TabItemCollection(this);
        Selection = new SingleSelectionModel<TabItem>(Items);
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        Items.Changed += OnItemsChanged;
        _strip = new TabStripPanel(this);
        _contentArea = new TabContentArea(this);
        Children.Add(_strip);
        Children.Add(_contentArea);
        Subscribe(ScreenProperty, (_, change) =>
        {
            CancelDrag();
            if (change.NewValue is null)
                _strip.Viewport.ResetBringIntoView();
        });
    }

    /// <summary>Gets the mutable collection of tab items.</summary>
    public TabItemCollection Items { get; }

    /// <summary>Gets the single selection model for this view.</summary>
    public SingleSelectionModel<TabItem> Selection { get; }

    /// <summary>Gets or sets the side of the content area occupied by the tab strip.</summary>
    public TabStripPlacement TabStripPlacement
    {
        get => GetValue(TabStripPlacementProperty);
        set => SetValue(TabStripPlacementProperty, value);
    }

    /// <summary>Gets or sets whether tabs can be reordered by dragging within this view.</summary>
    public bool CanReorderTabs
    {
        get => GetValue(CanReorderTabsProperty);
        set => SetValue(CanReorderTabsProperty, value);
    }

    /// <summary>Gets or sets the minimum horizontal tab slot width in logical pixels.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double MinTabWidth
    {
        get => GetValue(MinTabWidthProperty);
        set => SetValue(MinTabWidthProperty, value);
    }

    /// <summary>Gets or sets the maximum horizontal tab slot width in logical pixels.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double MaxTabWidth
    {
        get => GetValue(MaxTabWidthProperty);
        set => SetValue(MaxTabWidthProperty, value);
    }

    /// <summary>Gets or sets the preferred width of a vertical tab strip in logical pixels.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double VerticalTabStripWidth
    {
        get => GetValue(VerticalTabStripWidthProperty);
        set => SetValue(VerticalTabStripWidthProperty, value);
    }

    /// <summary>Occurs after the selected item has changed.</summary>
    public event EventHandler<TabSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Occurs when a user requests that a closable tab be closed.</summary>
    /// <remarks>The event does not remove the item; the handler decides whether and when to remove it.</remarks>
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested;

    /// <summary>Occurs after a drag has committed a new tab order.</summary>
    public event EventHandler<TabReorderedEventArgs>? TabReordered;

    internal bool IsHorizontalPlacement =>
        TabStripPlacement is TabStripPlacement.Top or TabStripPlacement.Bottom;

    /// <summary>
    /// Determines whether a node is an internal layout part owned by a tab view.
    /// </summary>
    internal static bool IsInternalPartNode(UiNode node) =>
        node.Parent is TabView ||
        node is TabStripPanel or TabStripViewport or TabScrollButton or TabContentArea or TabContentHost or TabHeaderHost or TabCloseButton;

    internal (double Min, double Max) GetTabWidthBounds()
    {
        var min = MinTabWidth;
        var max = MaxTabWidth;
        if (max < min)
            throw new InvalidOperationException("MaxTabWidth must be greater than or equal to MinTabWidth.");
        return (min, max);
    }

    internal double ResolveVerticalStripWidth(double availableWidth)
    {
        var preferred = VerticalTabStripWidth;
        return double.IsPositiveInfinity(availableWidth)
            ? preferred
            : Math.Min(preferred, availableWidth);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, TabStripPlacementProperty))
        {
            CancelDrag();
            _strip.SynchronizeOrientation();
        }
        else if (ReferenceEquals(eventArgs.Property, CanReorderTabsProperty) && !CanReorderTabs)
        {
            CancelDrag();
        }
        else if (ReferenceEquals(eventArgs.Property, IsEnabledProperty) && !IsEnabled)
        {
            CancelDrag();
        }

        base.OnPropertyChanged(eventArgs);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        if (IsHorizontalPlacement)
        {
            _strip.Measure(availableSize);
            var stripHeight = _strip.DesiredSize.Height;
            var contentHeight = SubtractFinite(availableSize.Height, stripHeight);
            _contentArea.Measure(new Size(availableSize.Width, contentHeight));
            var width = double.IsPositiveInfinity(availableSize.Width)
                ? Math.Max(_strip.DesiredSize.Width, _contentArea.DesiredSize.Width)
                : availableSize.Width;
            var height = double.IsPositiveInfinity(availableSize.Height)
                ? AddFinite(stripHeight, _contentArea.DesiredSize.Height)
                : availableSize.Height;
            return new Size(width, height);
        }
        else
        {
            var stripWidth = ResolveVerticalStripWidth(availableSize.Width);
            _strip.Measure(new Size(stripWidth, availableSize.Height));
            var contentWidth = double.IsPositiveInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width - stripWidth);
            _contentArea.Measure(new Size(contentWidth, availableSize.Height));
            var width = double.IsPositiveInfinity(availableSize.Width)
                ? AddFinite(stripWidth, _contentArea.DesiredSize.Width)
                : availableSize.Width;
            var height = double.IsPositiveInfinity(availableSize.Height)
                ? Math.Max(_strip.DesiredSize.Height, _contentArea.DesiredSize.Height)
                : availableSize.Height;
            return new Size(width, height);
        }
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        if (IsHorizontalPlacement)
        {
            var stripHeight = Math.Min(_strip.DesiredSize.Height, contentBounds.Height);
            var contentHeight = Math.Max(0, contentBounds.Height - stripHeight);
            if (TabStripPlacement == TabStripPlacement.Top)
            {
                _strip.Arrange(new Rect(contentBounds.X, contentBounds.Y, contentBounds.Width, stripHeight));
                _contentArea.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y + stripHeight, contentBounds.Width, contentHeight));
            }
            else
            {
                _contentArea.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y, contentBounds.Width, contentHeight));
                _strip.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y + contentHeight, contentBounds.Width, stripHeight));
            }
        }
        else
        {
            var stripWidth = Math.Min(ResolveVerticalStripWidth(contentBounds.Width), contentBounds.Width);
            var contentWidth = Math.Max(0, contentBounds.Width - stripWidth);
            if (TabStripPlacement == TabStripPlacement.Left)
            {
                _strip.Arrange(new Rect(contentBounds.X, contentBounds.Y, stripWidth, contentBounds.Height));
                _contentArea.Arrange(new Rect(
                    contentBounds.X + stripWidth, contentBounds.Y, contentWidth, contentBounds.Height));
            }
            else
            {
                _contentArea.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y, contentWidth, contentBounds.Height));
                _strip.Arrange(new Rect(
                    contentBounds.X + contentWidth, contentBounds.Y, stripWidth, contentBounds.Height));
            }
        }
    }

    private static double SubtractFinite(double available, double amount) =>
        double.IsPositiveInfinity(available) ? double.PositiveInfinity : Math.Max(0, available - amount);

    private static double AddFinite(double first, double second)
    {
        var result = first + second;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Tab view layout produced a non-finite size.");
        return result;
    }
}
