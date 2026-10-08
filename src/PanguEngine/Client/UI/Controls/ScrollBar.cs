using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides a single-axis scroll bar with a track, a draggable thumb, and optional step arrows.
/// </summary>
/// <remarks>
/// The control owns exactly one set of four interactive parts. Its orientation is exposed to styles through
/// the <c>horizontal</c> and <c>vertical</c> pseudo classes so descendant rules can select the active axis
/// without mutating part classes at runtime. <see cref="Value"/> is normalized to the current range before it
/// is committed, so every write path and binding observes the same effective position.
/// </remarks>
public sealed partial class ScrollBar : Control
{
    private static readonly UiPseudoClass HorizontalPseudoClass = UiPseudoClass.Get("horizontal");
    private static readonly UiPseudoClass VerticalPseudoClass = UiPseudoClass.Get("vertical");

    /// <summary>
    /// Identifies the <see cref="Orientation"/> property.
    /// </summary>
    public static readonly Property<Orientation> OrientationProperty =
        Property.Register<ScrollBar, Orientation>(
            nameof(Orientation),
            Orientation.Vertical,
            onChanged: static (bar, _, _) => bar.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="Minimum"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollBar, double> MinimumProperty =
        Property.RegisterDirect<ScrollBar, double>(
            nameof(Minimum),
            static bar => bar._minimum,
            static (bar, value) => bar.SetMinimum(value));

    /// <summary>
    /// Identifies the <see cref="Maximum"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollBar, double> MaximumProperty =
        Property.RegisterDirect<ScrollBar, double>(
            nameof(Maximum),
            static bar => bar._maximum,
            static (bar, value) => bar.SetMaximum(value),
            unsetValue: 100d);

    /// <summary>
    /// Identifies the <see cref="Value"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollBar, double> ValueProperty =
        Property.RegisterDirect<ScrollBar, double>(
            nameof(Value),
            static bar => bar._value,
            static (bar, value) => bar.SetPosition(value));

    /// <summary>
    /// Identifies the <see cref="ViewportSize"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollBar, double> ViewportSizeProperty =
        Property.RegisterDirect<ScrollBar, double>(
            nameof(ViewportSize),
            static bar => bar._viewportSize,
            static (bar, value) => bar.SetViewportSize(value));

    /// <summary>
    /// Identifies the <see cref="SmallChange"/> property.
    /// </summary>
    public static readonly Property<double> SmallChangeProperty =
        Property.Register<ScrollBar, double>(
            nameof(SmallChange),
            16d,
            validate: IsFiniteNonNegative,
            validationMessage: "SmallChange must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="LargeChange"/> property.
    /// </summary>
    public static readonly Property<double> LargeChangeProperty =
        Property.Register<ScrollBar, double>(
            nameof(LargeChange),
            100d,
            validate: IsFiniteNonNegative,
            validationMessage: "LargeChange must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="ShowArrows"/> property.
    /// </summary>
    public static readonly Property<bool> ShowArrowsProperty =
        Property.Register<ScrollBar, bool>(
            nameof(ShowArrows),
            true);

    /// <summary>
    /// Identifies the <see cref="IsWheelScrollingEnabled"/> property.
    /// </summary>
    public static readonly Property<bool> IsWheelScrollingEnabledProperty =
        Property.Register<ScrollBar, bool>(
            nameof(IsWheelScrollingEnabled),
            true);

    /// <summary>
    /// Identifies the <see cref="BarThickness"/> property.
    /// </summary>
    public static readonly Property<double> BarThicknessProperty =
        Property.Register<ScrollBar, double>(
            nameof(BarThickness),
            16d,
            onChanged: static (bar, _, _) => bar.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "BarThickness must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="MinimumThumbLength"/> property.
    /// </summary>
    public static readonly Property<double> MinimumThumbLengthProperty =
        Property.Register<ScrollBar, double>(
            nameof(MinimumThumbLength),
            16d,
            onChanged: static (bar, _, _) => bar.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "MinimumThumbLength must be a finite non-negative value.");

    static ScrollBar()
    {
        UiCssRegistry.RegisterElement<ScrollBar>("ScrollBar");
        UiCssRegistry.RegisterProperty<ScrollBar, Orientation>(
            "orientation",
            OrientationProperty,
            UiCssValueConverters.ParseOrientation);
        UiCssRegistry.RegisterProperty<ScrollBar, bool>(
            "show-arrows",
            ShowArrowsProperty,
            UiCssValueConverters.ParseBool);
        UiCssRegistry.RegisterProperty<ScrollBar, double>(
            "bar-thickness",
            BarThicknessProperty,
            UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<ScrollBar, double>(
            "minimum-thumb-length",
            MinimumThumbLengthProperty,
            UiCssValueConverters.ParseLength);
    }

    private readonly ScrollBarPart _track;
    private readonly ScrollBarPart _thumb;
    private readonly ScrollBarPart _decrease;
    private readonly ScrollBarPart _increase;
    private bool _isSynchronizingRange;
    private Rect _trackBounds;
    private bool _wasFocusedAtLastArrange;
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private double _viewportSize;

    /// <summary>
    /// Initializes a focusable vertical scroll bar with its internal parts.
    /// </summary>
    public ScrollBar()
    {
        Focusable = true;
        _repeatTicker = CreateTicker(OnRepeatTick);
        _track = CreatePart(ScrollBarPartKind.Track);
        _decrease = CreatePart(ScrollBarPartKind.Decrease);
        _increase = CreatePart(ScrollBarPartKind.Increase);
        _thumb = CreatePart(ScrollBarPartKind.Thumb);
        SynchronizeOrientationPseudoClass();
        UpdatePartVisibility();
        UpdateArrowStates();
    }

    /// <summary>
    /// Gets or sets the axis along which the bar scrolls.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the position represented by the start of the track.
    /// </summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the position represented by the end of the track.
    /// </summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the effective scroll position, constrained to the current range.
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the visible length used to size the thumb proportionally.
    /// </summary>
    public double ViewportSize
    {
        get => GetValue(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative distance moved by one arrow step.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double SmallChange
    {
        get => GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative distance moved by one track page.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double LargeChange
    {
        get => GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the two step arrows occupy layout space and are drawn.
    /// </summary>
    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether unhandled wheel input can change the scroll position.
    /// </summary>
    public bool IsWheelScrollingEnabled
    {
        get => GetValue(IsWheelScrollingEnabledProperty);
        set => SetValue(IsWheelScrollingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative preferred cross-axis size.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double BarThickness
    {
        get => GetValue(BarThicknessProperty);
        set => SetValue(BarThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative minimum thumb length.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double MinimumThumbLength
    {
        get => GetValue(MinimumThumbLengthProperty);
        set => SetValue(MinimumThumbLengthProperty, value);
    }

    private void SetMinimum(double value)
    {
        EnsureFinite(value, nameof(Minimum));
        SetField(MinimumProperty, ref _minimum, value);
    }

    private void SetMaximum(double value)
    {
        EnsureFinite(value, nameof(Maximum));
        SetField(MaximumProperty, ref _maximum, value);
    }

    private void SetPosition(double value) =>
        SetField(ValueProperty, ref _value, CoercePosition(value));

    private void SetViewportSize(double value)
    {
        EnsureNonNegative(value, nameof(ViewportSize));
        SetField(ViewportSizeProperty, ref _viewportSize, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, BorderThicknessProperty) &&
            IsUpdatingStyles && IsFocused && !_wasFocusedAtLastArrange &&
            _interaction == InteractionKind.DragThumb)
        {
            _isThumbDragRebasePending = true;
        }

        if (ReferenceEquals(eventArgs.Property, OrientationProperty))
        {
            CancelInteraction();
            SynchronizeOrientationPseudoClass();
            UpdateArrowStates();
            RefreshGeometry();
        }
        else if (ReferenceEquals(eventArgs.Property, MinimumProperty) ||
                 ReferenceEquals(eventArgs.Property, MaximumProperty))
        {
            CancelInteraction();
            SetPosition(_value);
            UpdateArrowStates();
            RefreshGeometry();
        }
        else if (ReferenceEquals(eventArgs.Property, ValueProperty))
        {
            UpdateArrowStates();
            RefreshGeometry();
        }
        else if (ReferenceEquals(eventArgs.Property, ViewportSizeProperty) ||
                 ReferenceEquals(eventArgs.Property, MinimumThumbLengthProperty) ||
                 ReferenceEquals(eventArgs.Property, BarThicknessProperty))
        {
            CancelInteraction();
            RefreshGeometry();
        }
        else if (ReferenceEquals(eventArgs.Property, ShowArrowsProperty))
        {
            CancelInteraction();
            UpdatePartVisibility();
            RefreshGeometry();
        }
        else if (ReferenceEquals(eventArgs.Property, IsEnabledProperty) && !IsEnabled ||
                 ReferenceEquals(eventArgs.Property, VisibilityProperty) && Visibility != Visibility.Visible ||
                 ReferenceEquals(eventArgs.Property, IsPressedProperty) && !IsPressed ||
                 ReferenceEquals(eventArgs.Property, ScreenProperty))
        {
            CancelInteraction();
        }

        base.OnPropertyChanged(eventArgs);
    }

    private double Range => Math.Max(0, Maximum - Minimum);

    private void RefreshGeometry()
    {
        if (_isSynchronizingRange || IsUpdatingStyles || !IsArrangeValid)
            return;

        ArrangeContent(ContentBounds);
    }

    /// <summary>
    /// Applies a range and position update supplied by the owning scroll host during its own layout.
    /// </summary>
    /// <param name="minimum">The new range start.</param>
    /// <param name="maximum">The new range end.</param>
    /// <param name="viewportSize">The visible length used to size the thumb.</param>
    /// <param name="value">The effective scroll position.</param>
    /// <remarks>
    /// The supplied values are interpreted as an owner-managed layout update and do not invalidate
    /// the owning host's layout pass.
    /// </remarks>
    internal void SynchronizeRange(double minimum, double maximum, double viewportSize, double value)
    {
        _isSynchronizingRange = true;
        try
        {
            SetValue(MinimumProperty, minimum);
            SetValue(MaximumProperty, maximum);
            SetValue(ViewportSizeProperty, viewportSize);
            SetValue(ValueProperty, value);
        }
        finally
        {
            _isSynchronizingRange = false;
        }

        RefreshGeometry();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var orientation = Orientation;
        var planMain = ResolvePlanMain(availableSize, orientation);
        var planCross = ResolvePlanCross(availableSize, orientation);
        var plan = ComputePlan(planMain);
        MeasureParts(plan, orientation, planCross);

        var desiredMain = ShowArrows ? 3 * BarThickness : BarThickness;
        var availableMain = GetMain(availableSize, orientation);
        if (double.IsFinite(availableMain))
            desiredMain = Math.Min(desiredMain, availableMain);
        var desiredCross = BarThickness;
        var availableCross = GetCross(availableSize, orientation);
        if (double.IsFinite(availableCross))
            desiredCross = Math.Min(desiredCross, availableCross);
        return CreateSize(orientation, desiredMain, desiredCross);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var orientation = Orientation;
        var main = GetMain(contentBounds, orientation);
        var cross = GetCross(contentBounds, orientation);
        var plan = ComputePlan(main);
        MeasureParts(plan, orientation, cross);

        var thickness = ResolveBarThickness(cross);
        var crossOffset = Math.Max(0, (cross - thickness) / 2);
        var mainOrigin = GetMainOrigin(contentBounds, orientation);
        var crossOrigin = GetCrossOrigin(contentBounds, orientation) + crossOffset;

        if (ShowArrows)
        {
            _decrease.Arrange(CreateRect(
                orientation,
                mainOrigin,
                crossOrigin,
                plan.ArrowLength,
                thickness));
            _increase.Arrange(CreateRect(
                orientation,
                mainOrigin + plan.ArrowLength + plan.TrackLength,
                crossOrigin,
                plan.ArrowLength,
                thickness));
        }

        var trackBounds = CreateRect(
            orientation,
            mainOrigin + plan.ArrowLength,
            crossOrigin,
            plan.TrackLength,
            thickness);
        _track.Arrange(trackBounds);
        DetectTrackGeometryChange(_track.LayoutBounds);
        _thumb.Arrange(CreateRect(
            orientation,
            mainOrigin + plan.ArrowLength + plan.ThumbOffset,
            crossOrigin,
            plan.ThumbLength,
            thickness));

        if (_isThumbDragRebasePending)
        {
            _thumbDragStartPointer = GetMainPoint(_lastPointerPosition, orientation);
            _thumbDragStartValue = Value;
            _isThumbDragRebasePending = false;
        }

        if (IsStyleValid)
            _wasFocusedAtLastArrange = IsFocused;
    }

    private void DetectTrackGeometryChange(Rect trackBounds)
    {
        if (trackBounds == _trackBounds)
            return;

        _trackBounds = trackBounds;
        if (_interaction == InteractionKind.DragThumb && !_isThumbDragRebasePending)
            CancelInteraction();
    }

    private ScrollBarPart CreatePart(ScrollBarPartKind kind)
    {
        var part = new ScrollBarPart(kind);
        Children.Add(part);
        return part;
    }

    private void SynchronizeOrientationPseudoClass()
    {
        var horizontal = Orientation == Orientation.Horizontal;
        SetPseudoClass(HorizontalPseudoClass, horizontal);
        SetPseudoClass(VerticalPseudoClass, !horizontal);
    }

    private void UpdatePartVisibility()
    {
        var arrowVisibility = ShowArrows ? Visibility.Visible : Visibility.Collapsed;
        _decrease.Visibility = arrowVisibility;
        _increase.Visibility = arrowVisibility;
    }

    private void UpdateArrowStates()
    {
        var minimum = Minimum;
        var maximum = Math.Max(minimum, Maximum);
        var value = Value;
        SetArrowState(_decrease, value, minimum, maximum, isDecrease: true);
        SetArrowState(_increase, value, minimum, maximum, isDecrease: false);
    }

    private static void SetArrowState(
        ScrollBarPart arrow,
        double value,
        double minimum,
        double maximum,
        bool isDecrease)
    {
        var atBoundary = isDecrease ? value <= minimum : value >= maximum;
        arrow.IsEnabled = !atBoundary;
    }

    private void MeasureParts(BarPlan plan, Orientation orientation, double cross)
    {
        var thickness = ResolveBarThickness(cross);
        if (ShowArrows)
        {
            _decrease.Measure(CreateSize(orientation, plan.ArrowLength, thickness));
            _increase.Measure(CreateSize(orientation, plan.ArrowLength, thickness));
        }

        _track.Measure(CreateSize(orientation, plan.TrackLength, thickness));
        _thumb.Measure(CreateSize(orientation, plan.ThumbLength, thickness));
    }

    private BarPlan ComputePlan(double main)
    {
        var useLayoutRounding = Screen?.UseLayoutRounding ?? true;
        var scale = Screen?.Scale ?? 1;
        if (useLayoutRounding)
            main = UiLayoutHelper.RoundLayoutValueUp(main, scale);
        var showArrows = ShowArrows;
        var thickness = useLayoutRounding ? UiLayoutHelper.RoundLayoutValueUp(BarThickness, scale) : BarThickness;
        var maximumArrowLength = useLayoutRounding
            ? Math.Floor(Math.Round(main * scale) / 2) / scale
            : main / 2;
        var arrowLength = showArrows
            ? Math.Min(thickness, Math.Max(0, maximumArrowLength))
            : 0;
        var trackLength = Math.Max(0, main - 2 * arrowLength);
        var range = Range;
        double thumbLength;
        if (trackLength <= 0)
        {
            thumbLength = 0;
        }
        else if (range <= 0)
        {
            thumbLength = trackLength;
        }
        else
        {
            var viewport = Math.Max(0, ViewportSize);
            var denominator = range + viewport;
            var theoretical = denominator > 0 ? viewport / denominator * trackLength : 0;
            thumbLength = Math.Min(
                trackLength,
                Math.Max(Math.Max(0, MinimumThumbLength), theoretical));
        }

        if (useLayoutRounding)
            thumbLength = Math.Min(trackLength, UiLayoutHelper.RoundLayoutValueUp(thumbLength, scale));

        var travel = trackLength - thumbLength;
        var thumbOffset = 0d;
        if (travel > 0 && range > 0)
        {
            var fraction = Math.Clamp((Value - Minimum) / range, 0, 1);
            thumbOffset = fraction * travel;
            if (useLayoutRounding)
                thumbOffset = Math.Clamp(UiLayoutHelper.RoundLayoutValue(thumbOffset, scale), 0, travel);
        }

        return new BarPlan(arrowLength, trackLength, thumbLength, thumbOffset);
    }

    private double ResolveBarThickness(double cross)
    {
        var thickness = BarThickness;
        if (Screen?.UseLayoutRounding ?? true)
            thickness = UiLayoutHelper.RoundLayoutValueUp(thickness, Screen?.Scale ?? 1);
        return Math.Min(thickness, Math.Max(0, cross));
    }

    private double ResolvePlanMain(Size availableSize, Orientation orientation)
    {
        var main = GetMain(availableSize, orientation);
        if (double.IsFinite(main))
            return Math.Max(0, main);
        return ShowArrows ? 3 * BarThickness : BarThickness;
    }

    private double ResolvePlanCross(Size availableSize, Orientation orientation)
    {
        var cross = GetCross(availableSize, orientation);
        return double.IsFinite(cross) ? Math.Max(0, cross) : BarThickness;
    }

    private static double GetMain(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Height : size.Width;

    private static double GetCross(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Width : size.Height;

    private static double GetMain(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Height : rect.Width;

    private static double GetCross(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Width : rect.Height;

    private static double GetMainOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Y : rect.X;

    private static double GetCrossOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.X : rect.Y;

    private static Size CreateSize(Orientation orientation, double main, double cross) =>
        orientation == Orientation.Vertical
            ? new Size(Math.Max(0, cross), Math.Max(0, main))
            : new Size(Math.Max(0, main), Math.Max(0, cross));

    private static Rect CreateRect(
        Orientation orientation,
        double mainOrigin,
        double crossOrigin,
        double mainExtent,
        double crossExtent)
    {
        mainExtent = Math.Max(0, mainExtent);
        crossExtent = Math.Max(0, crossExtent);
        return orientation == Orientation.Vertical
            ? new Rect(crossOrigin, mainOrigin, crossExtent, mainExtent)
            : new Rect(mainOrigin, crossOrigin, mainExtent, crossExtent);
    }

    private double CoercePosition(double value)
    {
        EnsureFinite(value, nameof(Value));
        var minimum = Minimum;
        var maximum = Math.Max(minimum, Maximum);
        return Math.Clamp(value, minimum, maximum);
    }

    private static void EnsureFinite(double value, string propertyName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(propertyName, "The value must be finite.");
    }

    private static void EnsureNonNegative(double value, string propertyName)
    {
        if (!IsFiniteNonNegative(value))
            throw new ArgumentOutOfRangeException(propertyName, "The value must be finite and non-negative.");
    }

    private readonly record struct BarPlan(
        double ArrowLength,
        double TrackLength,
        double ThumbLength,
        double ThumbOffset);
}
