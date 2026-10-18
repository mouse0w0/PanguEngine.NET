using System.Diagnostics.CodeAnalysis;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Defines shared orientation, dimensions and tick display settings for sliders.</summary>
public abstract class Slider : Control
{
    /// <summary>Identifies the slider axis.</summary>
    public static readonly Property<Orientation> OrientationProperty = Property.Register<Slider, Orientation>(
        nameof(Orientation), onChanged: static (slider, _, _) => slider.OnGeometryConfigurationChanged());

    /// <summary>Identifies whether tick marks are displayed.</summary>
    public static readonly Property<bool> ShowTicksProperty = Property.Register<Slider, bool>(nameof(ShowTicks),
        onChanged: static (slider, _, _) => slider.OnGeometryConfigurationChanged());

    /// <summary>Identifies whether tick labels are displayed.</summary>
    public static readonly Property<bool> ShowTickLabelsProperty = Property.Register<Slider, bool>(nameof(ShowTickLabels),
        onChanged: static (slider, _, _) => slider.OnGeometryConfigurationChanged());

    /// <summary>Identifies the visible track thickness.</summary>
    public static readonly Property<double> TrackThicknessProperty = RegisterLength(nameof(TrackThickness), 4);

    /// <summary>Identifies the thumb size along the track.</summary>
    public static readonly Property<double> ThumbLengthProperty = RegisterLength(nameof(ThumbLength), 20);

    /// <summary>Identifies the thumb size across the track.</summary>
    public static readonly Property<double> ThumbThicknessProperty = RegisterLength(nameof(ThumbThickness), 20);

    /// <summary>Identifies the tick mark length.</summary>
    public static readonly Property<double> TickLengthProperty = RegisterLength(nameof(TickLength), 6);

    /// <summary>Identifies the distance between tick marks and their labels.</summary>
    public static readonly Property<double> TickLabelSpacingProperty = RegisterLength(nameof(TickLabelSpacing), 4);

    static Slider()
    {
        CssRegistry.RegisterElement<Slider>("Slider");
        CssRegistry.RegisterProperty<Slider, Orientation>("orientation", OrientationProperty, CssValueConverters.ParseOrientation);
        CssRegistry.RegisterProperty<Slider, bool>("show-ticks", ShowTicksProperty, CssValueConverters.ParseBool);
        CssRegistry.RegisterProperty<Slider, bool>("show-tick-labels", ShowTickLabelsProperty, CssValueConverters.ParseBool);
        CssRegistry.RegisterProperty<Slider, double>("track-thickness", TrackThicknessProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<Slider, double>("thumb-length", ThumbLengthProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<Slider, double>("thumb-thickness", ThumbThicknessProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<Slider, double>("tick-length", TickLengthProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<Slider, double>("tick-label-spacing", TickLabelSpacingProperty, CssValueConverters.ParseLength);
    }

    /// <summary>Gets or sets the slider axis. Vertical values increase upwards.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>Gets or sets whether tick marks are displayed independently of value snapping.</summary>
    public bool ShowTicks { get => GetValue(ShowTicksProperty); set => SetValue(ShowTicksProperty, value); }

    /// <summary>Gets or sets whether non-overlapping tick labels are displayed.</summary>
    public bool ShowTickLabels { get => GetValue(ShowTickLabelsProperty); set => SetValue(ShowTickLabelsProperty, value); }

    /// <summary>Gets or sets the visible track thickness in logical pixels.</summary>
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }

    /// <summary>Gets or sets the thumb size along the track in logical pixels.</summary>
    public double ThumbLength { get => GetValue(ThumbLengthProperty); set => SetValue(ThumbLengthProperty, value); }

    /// <summary>Gets or sets the thumb size across the track in logical pixels.</summary>
    public double ThumbThickness { get => GetValue(ThumbThicknessProperty); set => SetValue(ThumbThicknessProperty, value); }

    /// <summary>Gets or sets the tick mark length in logical pixels.</summary>
    public double TickLength { get => GetValue(TickLengthProperty); set => SetValue(TickLengthProperty, value); }

    /// <summary>Gets or sets the spacing before tick labels in logical pixels.</summary>
    public double TickLabelSpacing { get => GetValue(TickLabelSpacingProperty); set => SetValue(TickLabelSpacingProperty, value); }

    /// <summary>Responds to changes in the slider's geometry configuration.</summary>
    protected virtual void OnGeometryConfigurationChanged() => InvalidateMeasure();

    protected void SynchronizePseudoClasses(bool hasValue)
    {
        SetPseudoClass(PseudoClass.Horizontal, Orientation == Orientation.Horizontal);
        SetPseudoClass(PseudoClass.Vertical, Orientation == Orientation.Vertical);
        SetPseudoClass(PseudoClass.NoValue, !hasValue);
    }

    private static Property<double> RegisterLength(string name, double defaultValue) => Property.Register<Slider, double>(
        name, defaultValue, onChanged: static (slider, _, _) => slider.OnGeometryConfigurationChanged(),
        validate: static value => double.IsFinite(value) && value >= 0,
        validationMessage: $"{name} must be finite and non-negative.");

    /// <summary>Provides a styled slider region with an optional hit target.</summary>
    protected sealed class SliderPart : Control
    {
        static SliderPart() => CssRegistry.RegisterElement<SliderPart>("SliderPart");

        internal SliderPart(string role, bool hitTestVisible)
        {
            Classes.Add(role);
            IsHitTestVisible = hitTestVisible;
        }
    }
}

/// <summary>Selects a typed value from a numeric range or a candidate list.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Slider<T> : Slider
{
    private const double InteractionThickness = 32;

    /// <summary>Identifies the selected value.</summary>
    public static readonly DirectProperty<Slider<T>, T> ValueProperty =
        Property.RegisterDirect<Slider<T>, T>(nameof(Value), static slider => slider.Value,
            static (slider, value) => slider.Value = value,
            onChanged: static (slider, _, value) => slider.OnValueChanged(value));

    /// <summary>Identifies whether a value is available.</summary>
    public static readonly DirectProperty<Slider<T>, bool> HasValueProperty =
        Property.RegisterDirect<Slider<T>, bool>(nameof(HasValue), static slider => slider.HasValue);

    /// <summary>Identifies the value model.</summary>
    public static readonly DirectProperty<Slider<T>, SliderValueModel<T>> ValueModelProperty =
        Property.RegisterDirect<Slider<T>, SliderValueModel<T>>(nameof(ValueModel), static slider => slider.ValueModel,
            static (slider, value) => slider.ValueModel = value,
            onChanged: static (slider, _, value) => slider.OnValueModelChanged(value),
            comparer: ReferenceEqualityComparer.Instance);

    /// <summary>Identifies the formatter used for tick labels.</summary>
    public static readonly Property<Func<T, string>> ValueFormatterProperty = Property.Register<Slider<T>, Func<T, string>>(
        nameof(ValueFormatter), static value => value?.ToString() ?? string.Empty,
        onChanged: static (slider, _, _) => slider.InvalidateMeasure());

    private SliderValueModel<T> _valueModel;
    private T _value;
    private SliderValueState<T> _state;
    private SliderValueState<T> _assignedState;
    private int _stateVersion;
    private int _availabilityVersion;
    private int _interactionVersion;
    private SliderModelBinding<T>.Connection _modelConnection;
    private readonly SliderPart _hitArea;
    private readonly SliderPart _track;
    private readonly SliderPart _fill;
    private readonly SliderPart _thumb;
    private readonly List<TickPart> _ticks = [];
    private bool _isDragging;
    private SliderValueState<T> _dragStart;
    private double _pointerOffset;
    private double _lastDragPointer;
    private Rect _dragScreenBounds;
    private double _dragScale;
    private double _trackStart;
    private double _travel;
    private Rect _hitBounds;
    private double? _tickPlanMain;

    /// <summary>Creates a focusable slider using the supplied value model.</summary>
    /// <param name="valueModel">The model defining the available values.</param>
    public Slider(SliderValueModel<T> valueModel)
    {
        _valueModel = valueModel;
        _state = valueModel.CreateInitialState();
        _value = _state.HasValue ? _state.Value : default!;
        _modelConnection = valueModel.Connections.Connect(this);
        _hitArea = AddPart("slider-hit-area", true);
        _track = AddPart("slider-track", false);
        _fill = AddPart("slider-fill", false);
        _thumb = AddPart("slider-thumb", true);
        Focusable = true;
        SynchronizePseudoClasses(HasValue);
        UpdateValueVisibility();
        Subscribe(ScreenProperty, (_, _) => CancelInteraction());
        Subscribe(IsFocusedProperty, (_, _) =>
        {
            if (!IsFocused)
                CancelInteraction();
        });
        Subscribe(IsEnabledProperty, (_, _) =>
        {
            if (!IsEnabled)
                CancelInteraction();
        });
        Subscribe(VisibilityProperty, (_, _) =>
        {
            if (Visibility != Visibility.Visible)
                CancelInteraction();
        });
        Subscribe(IsHitTestVisibleProperty, (_, _) =>
        {
            if (!IsHitTestVisible)
                CancelInteraction();
        });
    }

    /// <summary>Gets or sets the selected value through the model's assignment rules.</summary>
    /// <remarks>Assignments that preserve the effective value do not cancel an interaction.</remarks>
    /// <exception cref="InvalidOperationException">No candidate value is available.</exception>
    public T Value
    {
        get => HasValue ? _value : throw new InvalidOperationException("The slider has no value.");
        set
        {
            var next = _valueModel.ResolveValue(value);
            var previousAssignment = _assignedState;
            _assignedState = next;
            try
            {
                SetField(ValueProperty, ref _value, next.Value);
            }
            finally
            {
                _assignedState = previousAssignment;
            }
        }
    }

    /// <summary>Gets whether a selected value is available.</summary>
    public bool HasValue => _state.HasValue;

    /// <summary>Gets or sets the value model while preserving a legal current value when possible.</summary>
    public SliderValueModel<T> ValueModel
    {
        get => _valueModel;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetField(ValueModelProperty, ref _valueModel, value);
        }
    }

    /// <summary>Gets or sets the formatter used for tick labels.</summary>
    public Func<T, string> ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <summary>Occurs after a normal pointer release or an effective keyboard adjustment.</summary>
    public event EventHandler<SliderValueCommittedEventArgs<T>>? ValueCommitted;

    /// <summary>Attempts to read the current value without throwing for an empty model.</summary>
    /// <param name="value">The selected value when available.</param>
    /// <returns>Whether a selected value is available.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = HasValue ? _state.Value : default!;
        return HasValue;
    }

    private void OnValueChanged(T value)
    {
        if (_state.HasValue && SameValue(_state.Value, value))
            return;
        var oldState = _state;
        CancelInteraction();
        StoreState(_assignedState);
        RefreshSelection(oldState);
    }

    private void OnValueModelChanged(SliderValueModel<T> value)
    {
        var next = value.CreateReplacementState(_state);
        CancelInteraction();
        var oldState = _state;
        _modelConnection.Detach();
        var version = StoreState(next);
        _modelConnection = value.Connections.Connect(this);
        SynchronizePseudoClasses(HasValue);
        UpdateValueVisibility();
        InvalidateMeasure();
        PublishState(oldState, next, version);
    }

    private void CommitState(SliderValueState<T> state)
    {
        VerifyMutationAccess();
        var oldState = _state;
        var version = StoreState(state);
        RefreshSelection(oldState);
        PublishState(oldState, state, version);
    }

    private void RefreshSelection(SliderValueState<T> oldState)
    {
        if (oldState.HasValue != HasValue)
        {
            SynchronizePseudoClasses(HasValue);
            UpdateValueVisibility();
            InvalidateMeasure();
        }
        RefreshGeometry();
    }

    private (int State, int Availability) StoreState(SliderValueState<T> state)
    {
        var availabilityChanged = _state.HasValue != state.HasValue;
        if (availabilityChanged)
            _availabilityVersion++;
        if (availabilityChanged || state.HasValue && !SameValue(_state.Value, state.Value))
            _stateVersion++;
        _state = state;
        _value = state.HasValue ? state.Value : default!;
        return (_stateVersion, _availabilityVersion);
    }

    private void PublishState(SliderValueState<T> oldState, SliderValueState<T> state, (int State, int Availability) version)
    {
        if (oldState.HasValue != state.HasValue)
        {
            if (version.Availability == _availabilityVersion)
                RaisePropertyChanged(HasValueProperty, oldState.HasValue, state.HasValue);
        }
        else if (version.State == _stateVersion && state.HasValue && !SameValue(oldState.Value, state.Value))
            RaisePropertyChanged(ValueProperty, oldState.Value, state.Value);
    }

    internal void VerifyModelAccess() => VerifyMutationAccess();

    internal SliderSelectionChange<T> PrepareModelChange(SliderValueModelChange<T> change)
    {
        VerifyMutationAccess();
        if (change.AffectsValues)
            CancelInteraction();
        var oldState = _state;
        var next = _valueModel.RefreshState(_state, change);
        var version = StoreState(next);
        return new SliderSelectionChange<T>(oldState, next, version);
    }

    internal void PublishModelChange(SliderSelectionChange<T> change)
    {
        SynchronizePseudoClasses(HasValue);
        UpdateValueVisibility();
        InvalidateMeasure();
        RefreshGeometry();
        PublishState(change.OldState, change.State, change.Version);
    }

    /// <inheritdoc />
    protected override void OnGeometryConfigurationChanged()
    {
        CancelInteraction();
        SynchronizePseudoClasses(HasValue);
        base.OnGeometryConfigurationChanged();
    }

    private void UpdateValueVisibility()
    {
        _thumb.Visibility = HasValue ? Visibility.Visible : Visibility.Collapsed;
        _fill.Visibility = HasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left || !CanInteract())
            return;
        CancelInteraction();
        _dragStart = _state;
        _isDragging = true;
        _dragScreenBounds = GetScreenTrackBounds();
        _dragScale = Screen!.Scale;
        var pointer = Main(eventArgs.GetPosition(this));
        _lastDragPointer = pointer;
        _pointerOffset = ReferenceEquals(eventArgs.Source, _thumb)
            ? pointer - (MainOrigin(_thumb.LayoutBounds) + Main(_thumb.LayoutBounds) / 2)
            : 0;
        if (!ReferenceEquals(eventArgs.Source, _thumb))
            UpdatePointerValue(pointer, true);
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (!_isDragging)
            return;
        if (!CanInteract() || !IsPressed || !HasStableDragGeometry())
        {
            CancelInteraction();
            return;
        }
        UpdatePointerValue(Main(eventArgs.GetPosition(this)));
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !_isDragging)
            return;
        if (!CanInteract() || !HasStableDragGeometry())
        {
            CancelInteraction();
            return;
        }
        var version = _interactionVersion;
        UpdatePointerValue(Main(eventArgs.GetPosition(this)));
        if (version != _interactionVersion || !_isDragging)
            return;
        if (!CanInteract() || !HasStableDragGeometry())
        {
            CancelInteraction();
            return;
        }
        var start = _dragStart;
        var end = _state;
        CancelInteraction();
        eventArgs.Handled = true;
        PublishCommitted(start, end);
    }

    /// <inheritdoc />
    protected override void OnPointerCanceled(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerCanceled(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !_isDragging)
            return;
        CancelInteraction();
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !IsFocused || !CanInteract() || eventArgs.Modifiers != KeyModifiers.None)
            return;
        var key = eventArgs.Key;
        var direction = key switch
        {
            Key.Right when Orientation == Orientation.Horizontal => 1,
            Key.Left when Orientation == Orientation.Horizontal => -1,
            Key.Up when Orientation == Orientation.Vertical => 1,
            Key.Down when Orientation == Orientation.Vertical => -1,
            Key.PageUp => 1,
            Key.PageDown => -1,
            _ => 0
        };
        if (direction == 0 && key is not (Key.Home or Key.End))
            return;
        CancelInteraction();
        var version = _interactionVersion;
        var start = _state;
        var next = key is Key.Home or Key.End
            ? _valueModel.GetBoundary(key == Key.End)
            : _valueModel.Move(_state, direction, key is Key.PageUp or Key.PageDown);
        CommitState(next);
        eventArgs.Handled = true;
        if (version == _interactionVersion && IsFocused && CanInteract())
            PublishCommitted(start, next);
    }

    private bool CanInteract()
    {
        if (!HasValue || Screen is not { } screen || !screen.IsOpen())
            return false;
        for (Node? node = this; node is not null; node = node.Parent)
            if (!node.IsEnabled || !node.IsHitTestVisible || node.Visibility != Visibility.Visible)
                return false;
        return true;
    }

    private void UpdatePointerValue(double pointer, bool force = false)
    {
        if (_travel <= 0 || !force && pointer.Equals(_lastDragPointer))
            return;
        _lastDragPointer = pointer;
        var fraction = Math.Clamp((pointer - _pointerOffset - _trackStart) / _travel, 0, 1);
        CommitState(_valueModel.ResolvePosition(Orientation == Orientation.Vertical ? 1 - fraction : fraction));
    }

    private Rect GetScreenTrackBounds()
    {
        var origin = LocalToScreen(new Point(_hitBounds.X, _hitBounds.Y));
        return new Rect(origin.X, origin.Y, _hitBounds.Width, _hitBounds.Height);
    }

    private bool HasStableDragGeometry() => Screen!.Scale.Equals(_dragScale) && GetScreenTrackBounds() == _dragScreenBounds;

    private void CancelInteraction()
    {
        _interactionVersion++;
        _isDragging = false;
        _dragStart = default;
        _pointerOffset = 0;
        _lastDragPointer = 0;
    }

    private void PublishCommitted(SliderValueState<T> start, SliderValueState<T> end)
    {
        if (start.HasValue && end.HasValue && !SameValue(start.Value, end.Value))
            ValueCommitted?.Invoke(this, new SliderValueCommittedEventArgs<T>(start.Value, end.Value));
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var availableMain = Main(availableSize);
        var main = double.IsFinite(availableMain) ? availableMain : 160;
        _tickPlanMain ??= main;
        SynchronizeTicks(_tickPlanMain.Value);
        var labelCross = MeasureTickLabels();
        var cross = Math.Max(InteractionThickness, Math.Max(ThumbThickness, TrackThickness)) +
            (HasValue && ShowTicks ? TickLength : 0) +
            (HasValue && ShowTickLabels ? TickLabelSpacing + labelCross : 0);
        var availableCross = Cross(availableSize);
        foreach (var part in new[] { _hitArea, _track, _fill, _thumb })
            part.Measure(availableSize);
        return AxisSize(Math.Min(160, main), double.IsFinite(availableCross) ? Math.Min(cross, availableCross) : cross);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var main = Main(contentBounds);
        var cross = Cross(contentBounds);
        if ((ShowTicks || ShowTickLabels) && !Nullable.Equals(_tickPlanMain, main))
        {
            _tickPlanMain = main;
            CancelInteraction();
            InvalidateMeasure();
            return;
        }
        SynchronizeTicks(main);
        var labelCross = MeasureTickLabels();
        var barCross = Math.Min(cross, Math.Max(InteractionThickness, Math.Max(ThumbThickness, TrackThickness)));
        var extras = (HasValue && ShowTicks ? TickLength : 0) +
            (HasValue && ShowTickLabels ? TickLabelSpacing + labelCross : 0);
        var barOrigin = CrossOrigin(contentBounds) + Math.Max(0, (cross - barCross - extras) / 2);
        var mainOrigin = MainOrigin(contentBounds);
        var thumbLength = Math.Min(main, ThumbLength);
        var start = mainOrigin + thumbLength / 2;
        var travel = Math.Max(0, main - thumbLength);
        var hitBounds = AxisRect(mainOrigin, barOrigin, main, barCross);
        if (_hitBounds != hitBounds || !_trackStart.Equals(start) || !_travel.Equals(travel))
            CancelInteraction();
        _hitBounds = hitBounds;
        _trackStart = start;
        _travel = travel;

        var centerCross = barOrigin + barCross / 2;
        var trackThickness = Math.Min(barCross, TrackThickness);
        ArrangePart(_hitArea, hitBounds);
        ArrangePart(_track, AxisRect(start, centerCross - trackThickness / 2, travel, trackThickness));
        if (HasValue)
        {
            var position = _valueModel.GetPosition(_state);
            var visualPosition = Orientation == Orientation.Vertical ? 1 - position : position;
            var centerMain = start + visualPosition * travel;
            var fillStart = Orientation == Orientation.Vertical ? centerMain : start;
            ArrangePart(_fill, AxisRect(fillStart, centerCross - trackThickness / 2, travel * position, trackThickness));
            ArrangePart(_thumb, AxisRect(centerMain - thumbLength / 2,
                centerCross - Math.Min(barCross, ThumbThickness) / 2, thumbLength, Math.Min(barCross, ThumbThickness)));
        }

        var tickOrigin = barOrigin + barCross;
        var labelOrigin = tickOrigin + (ShowTicks ? TickLength : 0) + TickLabelSpacing;
        foreach (var tick in _ticks)
        {
            var center = start + VisualPosition(tick.Tick.Position) * travel;
            tick.Mark.Visibility = ShowTicks ? Visibility.Visible : Visibility.Collapsed;
            if (ShowTicks)
                ArrangePart(tick.Mark, AxisRect(center - .5, tickOrigin, 1, Math.Min(TickLength, Math.Max(0, CrossOrigin(contentBounds) + cross - tickOrigin))));
        }
        ArrangeTickLabels(contentBounds, labelOrigin);
    }

    private void SynchronizeTicks(double main)
    {
        var budget = (int)Math.Clamp(Math.Ceiling(main) + 1, 2, 2048);
        var ticks = HasValue && (ShowTicks || ShowTickLabels) ? _valueModel.GetTicks(budget, _state) : [];
        while (_ticks.Count > ticks.Count)
        {
            var part = _ticks[^1];
            Children.Remove(part.Mark);
            Children.Remove(part.Label);
            _ticks.RemoveAt(_ticks.Count - 1);
        }
        for (var index = 0; index < ticks.Count; index++)
        {
            if (index == _ticks.Count)
            {
                var mark = AddPart("slider-tick", false);
                var label = new Text { IsHitTestVisible = false };
                label.Classes.Add("slider-tick-label");
                Children.Add(label);
                _ticks.Add(new TickPart(mark, label));
            }
            var part = _ticks[index];
            part.Tick = ticks[index];
            part.Label.Content = ValueFormatter(ticks[index].Value);
            if (!ShowTickLabels)
                part.Label.Visibility = Visibility.Collapsed;
            else if (part.Label.Visibility == Visibility.Collapsed)
                part.Label.Visibility = Visibility.Hidden;
        }
    }

    private double MeasureTickLabels()
    {
        var cross = 0d;
        if (!ShowTickLabels)
            return cross;
        foreach (var part in _ticks)
        {
            part.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            cross = Math.Max(cross, Cross(part.Label.DesiredSize));
        }
        return cross;
    }

    private void ArrangeTickLabels(Rect bounds, double crossOrigin)
    {
        if (!ShowTickLabels)
            return;
        var selected = _ticks.OrderBy(part => LabelPriority(part.Tick)).ToArray();
        var occupied = new List<(double Start, double End)>();
        foreach (var part in selected)
        {
            var extent = Main(part.Label.DesiredSize);
            var crossExtent = Cross(part.Label.DesiredSize);
            var center = _trackStart + VisualPosition(part.Tick.Position) * _travel;
            var fits = extent <= Main(bounds) && crossOrigin + crossExtent <= CrossOrigin(bounds) + Cross(bounds);
            var origin = fits ? Math.Clamp(center - extent / 2, MainOrigin(bounds), MainOrigin(bounds) + Main(bounds) - extent) : MainOrigin(bounds);
            if (fits)
                fits = occupied.All(interval => origin + extent + 4 <= interval.Start || origin >= interval.End + 4);
            part.Label.Visibility = fits ? Visibility.Visible : Visibility.Hidden;
            if (!fits)
                continue;
            occupied.Add((origin, origin + extent));
            part.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            part.Label.Arrange(AxisRect(origin, crossOrigin, extent, crossExtent));
        }
    }

    private int LabelPriority(SliderTick<T> tick)
    {
        if (_state.Index >= 0 ? tick.Index == _state.Index : EqualityComparer<T>.Default.Equals(tick.Value, _state.Value))
            return 0;
        return tick.Position.Equals(0d) ? 1 : tick.Position.Equals(1d) ? 2 : 3;
    }

    private void RefreshGeometry()
    {
        if (IsArrangeValid && !IsUpdatingStyles)
            ArrangeContent(ContentBounds);
    }

    private static void ArrangePart(SliderPart part, Rect bounds)
    {
        part.Measure(new Size(bounds.Width, bounds.Height));
        part.Arrange(bounds);
    }

    private double VisualPosition(double position) => Orientation == Orientation.Vertical ? 1 - position : position;
    private double Main(Point point) => Orientation == Orientation.Vertical ? point.Y : point.X;
    private double Main(Size size) => Orientation == Orientation.Vertical ? size.Height : size.Width;
    private double Cross(Size size) => Orientation == Orientation.Vertical ? size.Width : size.Height;
    private double Main(Rect bounds) => Orientation == Orientation.Vertical ? bounds.Height : bounds.Width;
    private double Cross(Rect bounds) => Orientation == Orientation.Vertical ? bounds.Width : bounds.Height;
    private double MainOrigin(Rect bounds) => Orientation == Orientation.Vertical ? bounds.Y : bounds.X;
    private double CrossOrigin(Rect bounds) => Orientation == Orientation.Vertical ? bounds.X : bounds.Y;
    private Size AxisSize(double main, double cross) => Orientation == Orientation.Vertical ? new Size(cross, main) : new Size(main, cross);
    private Rect AxisRect(double main, double cross, double mainExtent, double crossExtent) => Orientation == Orientation.Vertical
        ? new Rect(cross, main, crossExtent, mainExtent)
        : new Rect(main, cross, mainExtent, crossExtent);

    private SliderPart AddPart(string role, bool hitTestVisible)
    {
        var part = new SliderPart(role, hitTestVisible);
        Children.Add(part);
        return part;
    }

    private static bool SameValue(T left, T right) => ValueProperty.ValuesEqual(left, right);

    private sealed class TickPart(SliderPart mark, Text label)
    {
        internal SliderPart Mark { get; } = mark;
        internal Text Label { get; } = label;
        internal SliderTick<T> Tick { get; set; }
    }
}
