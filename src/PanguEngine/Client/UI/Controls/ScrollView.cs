using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Input;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Presents a single content node inside a clipped scrollable viewport with optional scroll bars.
/// </summary>
/// <remarks>
/// The scroll bars occupy layout space instead of overlaying the content. Replacing or clearing
/// <see cref="Content"/> is performed through the validated parent tree operations, and a content node
/// moved to another parent by caller code clears this scroll view's content reference through the
/// observable viewport child collection. <see cref="Offset"/> is normalized to the resolved per-axis
/// range before it is committed, so property writes, scroll methods, bindings, and user input share one
/// effective value.
/// </remarks>
public sealed partial class ScrollView : Control
{
    /// <summary>
    /// Identifies the <see cref="Content"/> property.
    /// </summary>
    public static readonly Property<UiNode?> ContentProperty =
        Property.Register<ScrollView, UiNode?>(
            nameof(Content),
            onChanged: static (view, _, newValue) => view.OnContentChanged(newValue));

    /// <summary>
    /// Identifies the <see cref="HorizontalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly Property<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty =
        Property.Register<ScrollView, ScrollBarVisibility>(
            nameof(HorizontalScrollBarVisibility),
            onChanged: static (view, _, _) => view.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="VerticalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly Property<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
        Property.Register<ScrollView, ScrollBarVisibility>(
            nameof(VerticalScrollBarVisibility),
            ScrollBarVisibility.Auto,
            onChanged: static (view, _, _) => view.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="ShowArrows"/> property.
    /// </summary>
    public static readonly Property<bool> ShowArrowsProperty =
        Property.Register<ScrollView, bool>(
            nameof(ShowArrows),
            true,
            onChanged: static (view, _, _) => view.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="IsScrollChainingEnabled"/> property.
    /// </summary>
    public static readonly Property<bool> IsScrollChainingEnabledProperty =
        Property.Register<ScrollView, bool>(
            nameof(IsScrollChainingEnabled),
            true);

    /// <summary>
    /// Identifies the <see cref="Offset"/> property.
    /// </summary>
    /// <remarks>
    /// The property carries no registered invalidation so that the layout pass can normalize the
    /// effective value without invalidating the arrangement it is computing. Ordinary changes
    /// invalidate arrangement through <c>OnPropertyChanged</c>.
    /// </remarks>
    public static readonly DirectProperty<ScrollView, Point> OffsetProperty =
        Property.RegisterDirect<ScrollView, Point>(
            nameof(Offset),
            static view => view._offset,
            static (view, value) => view.SetOffset(value));

    /// <summary>
    /// Identifies the <see cref="SmallChange"/> property.
    /// </summary>
    public static readonly Property<double> SmallChangeProperty =
        Property.Register<ScrollView, double>(
            nameof(SmallChange),
            16);

    private static readonly PropertyKey<Size> ExtentPropertyKey =
        Property.RegisterReadOnly<ScrollView, Size>(
            nameof(Extent));

    private static readonly PropertyKey<Size> ViewportPropertyKey =
        Property.RegisterReadOnly<ScrollView, Size>(
            nameof(Viewport));

    /// <summary>
    /// Identifies the read-only <see cref="Extent"/> property.
    /// </summary>
    public static readonly Property<Size> ExtentProperty = ExtentPropertyKey.Property;

    /// <summary>
    /// Identifies the read-only <see cref="Viewport"/> property.
    /// </summary>
    public static readonly Property<Size> ViewportProperty = ViewportPropertyKey.Property;

    private readonly ScrollViewport _viewport;
    private readonly ScrollBar _horizontalBar;
    private readonly ScrollBar _verticalBar;
    private readonly Panel _corner;

    private bool _isInScrollLayout;
    private bool _isSynchronizingBars;
    private bool _isUpdatingContent;
    private bool _hasResolvedRange;
    private bool _isLayoutSolved;
    private double _horizontalMaximum;
    private double _verticalMaximum;
    private Size _measuredAvailableSize;
    private ScrollLayoutSolution _solution;
    private Point _offset;

    static ScrollView()
    {
        UiCssRegistry.RegisterElement<ScrollView>("ScrollView");
        UiCssRegistry.RegisterProperty<ScrollView, ScrollBarVisibility>(
            "horizontal-scroll-bar-visibility",
            HorizontalScrollBarVisibilityProperty,
            ParseScrollBarVisibility);
        UiCssRegistry.RegisterProperty<ScrollView, ScrollBarVisibility>(
            "vertical-scroll-bar-visibility",
            VerticalScrollBarVisibilityProperty,
            ParseScrollBarVisibility);
        UiCssRegistry.RegisterProperty<ScrollView, bool>(
            "show-arrows",
            ShowArrowsProperty,
            UiCssValueConverters.ParseBool);
    }

    /// <summary>
    /// Initializes a scroll view with an internal viewport, two scroll bars, and a corner fill.
    /// </summary>
    public ScrollView()
    {
        Focusable = true;
        _viewport = new ScrollViewport { Owner = this };
        _horizontalBar = new ScrollBar
        {
            Focusable = false,
            IsWheelScrollingEnabled = false,
            Orientation = Orientation.Horizontal
        };
        _verticalBar = new ScrollBar
        {
            Focusable = false,
            IsWheelScrollingEnabled = false,
            Orientation = Orientation.Vertical
        };
        _corner = new Panel { IsHitTestVisible = false, Opacity = 0 };
        _corner.Classes.Add("scrollview-corner");
        Children.Add(_viewport);
        Children.Add(_horizontalBar);
        Children.Add(_verticalBar);
        Children.Add(_corner);
        _horizontalBar.PropertyChanged += OnInternalBarPropertyChanged;
        _verticalBar.PropertyChanged += OnInternalBarPropertyChanged;
        ApplyConfigurationToBars();
    }

    /// <summary>
    /// Gets or sets the single business content node, or null for no content.
    /// </summary>
    /// <remarks>
    /// Assigning a node moves it from its current parent. Moving the current content node to another
    /// parent by caller code clears this property.
    /// </remarks>
    public UiNode? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the horizontal axis can scroll and when its scroll bar is shown.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the vertical axis can scroll and when its scroll bar is shown.
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the internal scroll bars show their arrow buttons.
    /// </summary>
    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a non-zero wheel delta that cannot move this view is passed to an outer scroll target.
    /// </summary>
    public bool IsScrollChainingEnabled
    {
        get => GetValue(IsScrollChainingEnabledProperty);
        set => SetValue(IsScrollChainingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the effective scroll offset in logical pixels.
    /// </summary>
    /// <remarks>
    /// The stored value is constrained to each scrollable axis' available range before it is published.
    /// Before the first layout the enabled axes preserve a non-negative request and disabled axes stay zero.
    /// </remarks>
    public Point Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    /// Gets the scrollable content size in logical pixels, including content margin.
    /// </summary>
    public Size Extent => GetValue(ExtentProperty);

    /// <summary>
    /// Gets the visible content size in logical pixels, excluding decoration and scroll bars.
    /// </summary>
    public Size Viewport => GetValue(ViewportProperty);

    /// <summary>
    /// Gets or sets the finite non-negative single-step distance in logical pixels.
    /// </summary>
    public double SmallChange
    {
        get => GetValue(SmallChangeProperty);
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(SmallChange), "SmallChange must be a finite non-negative value.");

            SetValue(SmallChangeProperty, value);
        }
    }

    /// <summary>
    /// Scrolls to an absolute offset using the same constraint and notification path as <see cref="Offset"/>.
    /// </summary>
    /// <param name="offsetX">The requested horizontal offset.</param>
    /// <param name="offsetY">The requested vertical offset.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is not finite.</exception>
    public void ScrollTo(double offsetX, double offsetY)
    {
        Offset = new Point(offsetX, offsetY);
    }

    /// <summary>
    /// Scrolls by a relative delta using the same constraint and notification path as <see cref="Offset"/>.
    /// </summary>
    /// <param name="deltaX">The horizontal delta.</param>
    /// <param name="deltaY">The vertical delta.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a delta is not finite or the result is not finite.</exception>
    public void ScrollBy(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX))
            throw new ArgumentOutOfRangeException(nameof(deltaX), "A scroll delta must be finite.");
        if (!double.IsFinite(deltaY))
            throw new ArgumentOutOfRangeException(nameof(deltaY), "A scroll delta must be finite.");

        var current = Offset;
        Offset = new Point(current.X + deltaX, current.Y + deltaY);
    }

    private bool IsHorizontalScrollable =>
        HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;

    private bool IsVerticalScrollable =>
        VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

    private void SetOffset(Point value) =>
        SetField(OffsetProperty, ref _offset, CoerceOffset(value));

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, OffsetProperty))
        {
            if (!_isInScrollLayout)
                _viewport.InvalidateArrange();
            if (!_isSynchronizingBars)
                ApplyOffsetToBars();
        }
        else if (ReferenceEquals(eventArgs.Property, ShowArrowsProperty) ||
                 ReferenceEquals(eventArgs.Property, SmallChangeProperty))
        {
            ApplyConfigurationToBars();
        }
        else if (ReferenceEquals(eventArgs.Property, HorizontalScrollBarVisibilityProperty) ||
                 ReferenceEquals(eventArgs.Property, VerticalScrollBarVisibilityProperty))
        {
            SetOffset(_offset);
        }

        base.OnPropertyChanged(eventArgs);
    }

    /// <summary>
    /// Clears the content reference when the content node leaves the internal viewport.
    /// </summary>
    /// <param name="content">The content node that left.</param>
    internal void OnViewportContentRemoved(UiNode content)
    {
        if (_isUpdatingContent)
            return;

        if (!ReferenceEquals(Content, content))
            return;

        Content = null;
    }

    private void OnContentChanged(UiNode? newContent)
    {
        _isUpdatingContent = true;
        try
        {
            _viewport.SetContent(newContent);
        }
        finally
        {
            _isUpdatingContent = false;
        }
    }

    private Point CoerceOffset(Point value)
    {
        var horizontal = CoerceOffsetAxis(value.X, IsHorizontalScrollable, _horizontalMaximum);
        var vertical = CoerceOffsetAxis(value.Y, IsVerticalScrollable, _verticalMaximum);
        return new Point(horizontal, vertical);
    }

    private double CoerceOffsetAxis(double requested, bool scrollable, double maximum)
    {
        if (!scrollable)
            return 0;
        if (!_hasResolvedRange)
            return requested < 0 ? 0 : requested;

        if (requested < 0)
            return 0;
        return requested > maximum ? maximum : requested;
    }

    private void ApplyOffsetToBars()
    {
        var offset = Offset;
        _isSynchronizingBars = true;
        try
        {
            _horizontalBar.Value = offset.X;
            _verticalBar.Value = offset.Y;
        }
        finally
        {
            _isSynchronizingBars = false;
        }
    }

    private void ApplyBarValuesToOffset()
    {
        _isSynchronizingBars = true;
        try
        {
            Offset = new Point(_horizontalBar.Value, _verticalBar.Value);
        }
        finally
        {
            _isSynchronizingBars = false;
        }

        ApplyOffsetToBars();
    }

    private void OnInternalBarPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Property, ScrollBar.ValueProperty))
            return;
        if (_isSynchronizingBars)
            return;

        ApplyBarValuesToOffset();
    }

    private void ApplyConfigurationToBars()
    {
        var showArrows = ShowArrows;
        var smallChange = SmallChange;
        _horizontalBar.ShowArrows = showArrows;
        _verticalBar.ShowArrows = showArrows;
        _horizontalBar.SmallChange = smallChange;
        _verticalBar.SmallChange = smallChange;
    }

    private void ApplyScrollChrome(ScrollLayoutSolution solution)
    {
        ApplyBarChrome(_horizontalBar, solution.ShowHorizontal);
        ApplyBarChrome(_verticalBar, solution.ShowVertical);
        if (solution.ShowHorizontal && solution.ShowVertical)
            _corner.ClearValue(OpacityProperty);
        else
            _corner.Opacity = 0;
    }

    private static void ApplyBarChrome(ScrollBar bar, bool shown)
    {
        if (shown)
        {
            bar.ClearValue(OpacityProperty);
            bar.IsHitTestVisible = true;
            return;
        }

        bar.CancelInteraction();
        bar.SetValue(OpacityProperty, 0d);
        bar.IsHitTestVisible = false;
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(UiPointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheel(eventArgs);
        if (eventArgs.Handled)
            return;

        HandleWheel(eventArgs);
    }

    private void HandleWheel(UiPointerWheelEventArgs eventArgs)
    {
        var deltaX = eventArgs.DeltaX;
        var deltaY = eventArgs.DeltaY;
        if ((eventArgs.Modifiers & KeyModifiers.Shift) != 0)
        {
            deltaX += deltaY;
            deltaY = 0;
        }

        if (deltaX == 0 && deltaY == 0)
            return;

        var step = 3 * SmallChange;
        var before = Offset;
        Offset = new Point(before.X - deltaX * step, before.Y - deltaY * step);
        if (Offset != before)
        {
            eventArgs.Handled = true;
            return;
        }

        if (!IsScrollChainingEnabled)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || eventArgs.Modifiers != KeyModifiers.None)
            return;

        HandleNavigationKey(eventArgs.Key, eventArgs);
    }

    private void HandleNavigationKey(Key key, UiKeyEventArgs eventArgs)
    {
        var step = SmallChange;
        switch (key)
        {
            case Key.Left when IsHorizontalScrollable:
                Offset = new Point(Offset.X - step, Offset.Y);
                break;
            case Key.Right when IsHorizontalScrollable:
                Offset = new Point(Offset.X + step, Offset.Y);
                break;
            case Key.Up when IsVerticalScrollable:
                Offset = new Point(Offset.X, Offset.Y - step);
                break;
            case Key.Down when IsVerticalScrollable:
                Offset = new Point(Offset.X, Offset.Y + step);
                break;
            case Key.PageUp when IsVerticalScrollable:
                Offset = new Point(Offset.X, Offset.Y - Viewport.Height);
                break;
            case Key.PageDown when IsVerticalScrollable:
                Offset = new Point(Offset.X, Offset.Y + Viewport.Height);
                break;
            case Key.Home when IsVerticalScrollable:
                Offset = new Point(Offset.X, 0);
                break;
            case Key.Home when IsHorizontalScrollable:
                Offset = new Point(0, Offset.Y);
                break;
            case Key.End when IsVerticalScrollable:
                Offset = new Point(Offset.X, _verticalMaximum);
                break;
            case Key.End when IsHorizontalScrollable:
                Offset = new Point(_horizontalMaximum, Offset.Y);
                break;
            default:
                return;
        }

        eventArgs.Handled = true;
    }

    private static ScrollBarVisibility ParseScrollBarVisibility(string value)
    {
        if (string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase))
            return ScrollBarVisibility.Disabled;
        if (string.Equals(value, "hidden", StringComparison.OrdinalIgnoreCase))
            return ScrollBarVisibility.Hidden;
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            return ScrollBarVisibility.Auto;
        if (string.Equals(value, "visible", StringComparison.OrdinalIgnoreCase))
            return ScrollBarVisibility.Visible;

        throw new FormatException($"Value '{value}' is not a recognized scroll bar visibility.");
    }
}
