using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Displays a numeric progress value within a range.</summary>
public sealed class ProgressBar : Control
{
    private static readonly PseudoClass ReversedPseudoClass = PseudoClass.Get("reversed");

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

    private readonly Panel _fill;

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

    /// <summary>Gets or sets the finite upper bound. A non-increasing range displays empty progress.</summary>
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
        var progress = GetProgress();
        var offset = forward ? 0 : (1 - progress) * length;
        var filledLength = progress * length;
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

    private void OnDirectionChanged()
    {
        SynchronizePseudoClasses();
        InvalidateArrange();
    }

    private void SynchronizePseudoClasses()
    {
        SetPseudoClass(PseudoClass.Horizontal, Orientation == Orientation.Horizontal);
        SetPseudoClass(PseudoClass.Vertical, Orientation == Orientation.Vertical);
        SetPseudoClass(ReversedPseudoClass, IsReversed);
    }

    private static void EnsureFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Progress values must be finite.");
    }
}
