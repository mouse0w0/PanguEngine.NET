using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Displays a numeric progress value or a looping indeterminate indicator.</summary>
public sealed class ProgressBar : Control
{
    /// <summary>Identifies the lower bound of the progress range.</summary>
    public static readonly DirectProperty<ProgressBar, double> MinimumProperty =
        Property.RegisterDirect<ProgressBar, double>(nameof(Minimum), static bar => bar.Minimum,
            static (bar, value) => bar.Minimum = value, unsetValue: 0d,
            onChanged: static (bar, _, _) => bar.InvalidateArrange());

    /// <summary>Identifies the upper bound of the progress range.</summary>
    public static readonly DirectProperty<ProgressBar, double> MaximumProperty =
        Property.RegisterDirect<ProgressBar, double>(nameof(Maximum), static bar => bar.Maximum,
            static (bar, value) => bar.Maximum = value, unsetValue: 100d,
            onChanged: static (bar, _, _) => bar.InvalidateArrange());

    /// <summary>Identifies the raw progress value.</summary>
    public static readonly DirectProperty<ProgressBar, double> ValueProperty =
        Property.RegisterDirect<ProgressBar, double>(nameof(Value), static bar => bar.Value,
            static (bar, value) => bar.Value = value, unsetValue: 0d,
            onChanged: static (bar, _, _) => bar.InvalidateArrange());

    /// <summary>Identifies the progress axis.</summary>
    public static readonly Property<Orientation> OrientationProperty = Property.Register<ProgressBar, Orientation>(
        nameof(Orientation), Orientation.Horizontal,
        onChanged: static (bar, _, _) => bar.OnDirectionChanged());

    /// <summary>Identifies whether the fill origin is reversed.</summary>
    public static readonly Property<bool> IsReversedProperty = Property.Register<ProgressBar, bool>(
        nameof(IsReversed), onChanged: static (bar, _, _) => bar.OnDirectionChanged());

    /// <summary>Identifies whether progress is displayed as a looping indicator.</summary>
    public static readonly DirectProperty<ProgressBar, bool> IsIndeterminateProperty =
        Property.RegisterDirect<ProgressBar, bool>(nameof(IsIndeterminate), static bar => bar.IsIndeterminate,
            static (bar, value) => bar.IsIndeterminate = value, unsetValue: false,
            onChanged: static (bar, _, _) => bar.OnModeChanged());

    private readonly Panel _fill;
    private IDisposable? _sceneSubscription;
    private IDisposable? _sceneOpenSubscription;
    private IDisposable? _visibilitySubscription;
    private Ticker? _animationTicker;
    private TimeSpan? _lastAnimationTime;
    private double _animationPhase;

    static ProgressBar()
    {
        CssRegistry.RegisterElement<ProgressBar>("ProgressBar");
        CssRegistry.RegisterProperty<ProgressBar, Orientation>("orientation", OrientationProperty,
            CssValueConverters.ParseOrientation);
        CssRegistry.RegisterProperty<ProgressBar, bool>("is-reversed", IsReversedProperty,
            CssValueConverters.ParseBool);
    }

    /// <summary>Creates a non-interactive horizontal progress bar.</summary>
    public ProgressBar()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        _fill = new Panel { IsHitTestVisible = false };
        _fill.Classes.Add("progress-bar-fill");
        Children.Add(_fill);
        SynchronizePseudoClasses();
    }

    /// <summary>Gets or sets the finite lower bound without modifying the value or upper bound.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    public double Minimum
    {
        get;
        set
        {
            EnsureFinite(value, nameof(Minimum));
            SetField(MinimumProperty, ref field, value);
        }
    }

    /// <summary>Gets or sets the finite upper bound. A non-increasing range displays empty determinate progress.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    public double Maximum
    {
        get;
        set
        {
            EnsureFinite(value, nameof(Maximum));
            SetField(MaximumProperty, ref field, value);
        }
    } = 100;

    /// <summary>Gets or sets the finite raw value. Out-of-range values affect only the displayed proportion.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    public double Value
    {
        get;
        set
        {
            EnsureFinite(value, nameof(Value));
            SetField(ValueProperty, ref field, value);
        }
    }

    /// <summary>Gets or sets the axis. Vertical progress normally increases upwards.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets whether progress fills from the opposite end of the track.</summary>
    public bool IsReversed
    {
        get => GetValue(IsReversedProperty);
        set => SetValue(IsReversedProperty, value);
    }

    /// <summary>Gets or sets whether a looping indicator replaces the displayed numeric proportion.</summary>
    /// <remarks>
    /// Numeric values are preserved. Animation runs while the owning scene is open, pauses when this
    /// element or an ancestor is hidden or collapsed, and continues while disabled.
    /// </remarks>
    public bool IsIndeterminate
    {
        get;
        set => SetField(IsIndeterminateProperty, ref field, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _fill.Measure(availableSize);
        return Size.Zero;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var forward = horizontal ? !IsReversed : IsReversed;
        var length = horizontal ? contentBounds.Width : contentBounds.Height;
        double start;
        double end;
        if (IsIndeterminate)
        {
            var position = -0.3 + 1.3 * _animationPhase;
            start = Math.Clamp(position, 0, 1);
            end = Math.Clamp(position + 0.3, 0, 1);
        }
        else
        {
            start = 0;
            end = GetProgress();
        }
        var offset = (forward ? start : 1 - end) * length;
        var filledLength = (end - start) * length;
        _fill.Arrange(horizontal
            ? new Rect(contentBounds.X + offset, contentBounds.Y, filledLength, contentBounds.Height)
            : new Rect(contentBounds.X, contentBounds.Y + offset, contentBounds.Width, filledLength));
    }

    private double GetProgress()
    {
        if (Maximum <= Minimum || Value <= Minimum)
            return 0;
        if (Value >= Maximum)
            return 1;
        var range = Maximum - Minimum;
        return Math.Clamp(double.IsFinite(range)
            ? (Value - Minimum) / range
            : (Value / 2 - Minimum / 2) / (Maximum / 2 - Minimum / 2), 0, 1);
    }

    private void OnModeChanged()
    {
        SetPseudoClass(PseudoClass.Indeterminate, IsIndeterminate);
        if (IsIndeterminate)
        {
            _sceneSubscription = Subscribe(SceneProperty, OnSceneChanged);
            _visibilitySubscription = Subscribe(IsEffectivelyVisibleProperty, OnEffectiveVisibilityChanged);
            _sceneOpenSubscription = Scene?.Subscribe(UIScene.IsOpenProperty, OnSceneOpenChanged);
        }
        else
        {
            _sceneSubscription?.Dispose();
            _sceneSubscription = null;
            _sceneOpenSubscription?.Dispose();
            _sceneOpenSubscription = null;
            _visibilitySubscription?.Dispose();
            _visibilitySubscription = null;
        }
        RestartAnimation();
    }

    private void OnSceneChanged(object? sender, PropertyChangedEventArgs<UIScene?> eventArgs)
    {
        if (!IsIndeterminate)
            return;

        _sceneOpenSubscription?.Dispose();
        _sceneOpenSubscription = eventArgs.NewValue?.Subscribe(UIScene.IsOpenProperty, OnSceneOpenChanged);
        RestartAnimation();
    }

    private void OnSceneOpenChanged(object? sender, PropertyChangedEventArgs<bool> eventArgs)
    {
        RestartAnimation();
    }

    private void OnEffectiveVisibilityChanged(object? sender, PropertyChangedEventArgs<bool> eventArgs)
    {
        SynchronizeAnimation();
    }

    private void RestartAnimation()
    {
        _animationTicker?.Stop();
        _lastAnimationTime = null;
        _animationPhase = 0;
        InvalidateArrange();
        SynchronizeAnimation();
    }

    private void SynchronizeAnimation()
    {
        if (IsIndeterminate && Scene is { IsOpen: true } && IsEffectivelyVisible)
            (_animationTicker ??= UIToolkit.CreateTicker(OnAnimationTick)).Start();
        else
        {
            _animationTicker?.Stop();
            _lastAnimationTime = null;
        }
    }

    private void OnAnimationTick(TimeSpan frameTime)
    {
        var previous = _lastAnimationTime;
        _lastAnimationTime = frameTime;
        if (previous is not { } baseline)
            return;
        var elapsed = (frameTime - baseline).TotalSeconds;
        if (elapsed <= 0)
            return;

        _animationPhase = (_animationPhase + elapsed / 1.5) % 1;
        InvalidateArrange();
    }

    private void OnDirectionChanged()
    {
        SynchronizePseudoClasses();
        InvalidateArrange();
    }

    private void SynchronizePseudoClasses()
    {
        SetPseudoClass(PseudoClass.Horizontal, Orientation == Orientation.Horizontal);
        SetPseudoClass(PseudoClass.Vertical, Orientation == Orientation.Vertical);
        SetPseudoClass(PseudoClass.Reversed, IsReversed);
    }

    private static void EnsureFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Progress values must be finite.");
    }
}
