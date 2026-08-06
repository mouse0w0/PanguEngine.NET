namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private bool _isHitTestLayoutValid;

    /// <summary>
    /// Identifies the <see cref="Width"/> property.
    /// </summary>
    public static readonly UiProperty<double> WidthProperty =
        UiProperty.Register<UiNode, double>(
            nameof(Width),
            double.NaN,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="Height"/> property.
    /// </summary>
    public static readonly UiProperty<double> HeightProperty =
        UiProperty.Register<UiNode, double>(
            nameof(Height),
            double.NaN,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="MinWidth"/> property.
    /// </summary>
    public static readonly UiProperty<double> MinWidthProperty =
        UiProperty.Register<UiNode, double>(
            nameof(MinWidth),
            invalidation: UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="MinHeight"/> property.
    /// </summary>
    public static readonly UiProperty<double> MinHeightProperty =
        UiProperty.Register<UiNode, double>(
            nameof(MinHeight),
            invalidation: UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="MaxWidth"/> property.
    /// </summary>
    public static readonly UiProperty<double> MaxWidthProperty =
        UiProperty.Register<UiNode, double>(
            nameof(MaxWidth),
            double.PositiveInfinity,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="MaxHeight"/> property.
    /// </summary>
    public static readonly UiProperty<double> MaxHeightProperty =
        UiProperty.Register<UiNode, double>(
            nameof(MaxHeight),
            double.PositiveInfinity,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="Margin"/> property.
    /// </summary>
    public static readonly UiProperty<Thickness> MarginProperty =
        UiProperty.Register<UiNode, Thickness>(
            nameof(Margin),
            Thickness.Zero,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="HorizontalAlignment"/> property.
    /// </summary>
    public static readonly UiProperty<HorizontalAlignment> HorizontalAlignmentProperty =
        UiProperty.Register<UiNode, HorizontalAlignment>(
            nameof(HorizontalAlignment),
            HorizontalAlignment.Stretch,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Identifies the <see cref="VerticalAlignment"/> property.
    /// </summary>
    public static readonly UiProperty<VerticalAlignment> VerticalAlignmentProperty =
        UiProperty.Register<UiNode, VerticalAlignment>(
            nameof(VerticalAlignment),
            VerticalAlignment.Stretch,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Identifies the <see cref="Visibility"/> property.
    /// </summary>
    public static readonly UiProperty<Visibility> VisibilityProperty =
        UiProperty.Register<UiNode, Visibility>(
            nameof(Visibility),
            Visibility.Visible,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    private Size _lastMeasureConstraint;
    private Size _desiredContentSize;
    private Rect _lastArrangeRect;
    private int _measurePassDepth;
    private int _arrangePassDepth;
    private ulong _measureInvalidationVersion;
    private ulong _arrangeInvalidationVersion;

    /// <summary>
    /// Gets or sets the requested width, or NaN for automatic sizing.
    /// </summary>
    public double Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the requested height, or NaN for automatic sizing.
    /// </summary>
    public double Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum width.
    /// </summary>
    public double MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum height.
    /// </summary>
    public double MinHeight
    {
        get => GetValue(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum width.
    /// </summary>
    public double MaxWidth
    {
        get => GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum height.
    /// </summary>
    public double MaxHeight
    {
        get => GetValue(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-negative spacing outside the layout bounds.
    /// </summary>
    public Thickness Margin
    {
        get => GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets horizontal positioning within the allocated slot.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment
    {
        get => GetValue(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets vertical positioning within the allocated slot.
    /// </summary>
    public VerticalAlignment VerticalAlignment
    {
        get => GetValue(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets how this node participates in layout, drawing, and hit testing.
    /// </summary>
    public Visibility Visibility
    {
        get => GetValue(VisibilityProperty);
        set => SetValue(VisibilityProperty, value);
    }

    /// <summary>
    /// Gets the measured size, including margin.
    /// </summary>
    public Size DesiredSize { get; private set; }

    /// <summary>
    /// Gets the arranged boundary in parent coordinates, excluding margin.
    /// </summary>
    /// <remarks>
    /// A root node uses the coordinate system supplied by its arrange caller.
    /// </remarks>
    public Rect LayoutBounds { get; private set; }

    /// <summary>
    /// Gets whether measurement is valid for the last constraint.
    /// </summary>
    public bool IsMeasureValid { get; private set; }

    /// <summary>
    /// Gets whether arrangement is valid for the last layout rectangle.
    /// </summary>
    public bool IsArrangeValid { get; private set; }

    /// <summary>
    /// Measures this node within an available size.
    /// </summary>
    /// <param name="availableSize">The available size, which may contain positive infinity.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from another thread, or when a layout property or
    /// measured result has an invalid value.
    /// </exception>
    public void Measure(Size availableSize)
    {
        VerifyLayoutAccess();
        VerifyLayoutProperties();
        if (IsMeasureValid && _lastMeasureConstraint == availableSize)
            return;

        InvalidateMeasureState();

        if (Visibility == Visibility.Collapsed)
        {
            _lastMeasureConstraint = availableSize;
            _desiredContentSize = Size.Zero;
            DesiredSize = Size.Zero;
            IsMeasureValid = true;
            return;
        }

        var margin = Margin;
        var width = Width;
        var height = Height;
        var minWidth = MinWidth;
        var minHeight = MinHeight;
        var effectiveMaxWidth = Math.Max(minWidth, MaxWidth);
        var effectiveMaxHeight = Math.Max(minHeight, MaxHeight);
        var coreConstraint = new Size(
            GetMeasureConstraint(
                availableSize.Width,
                margin.Left,
                margin.Right,
                width,
                effectiveMaxWidth),
            GetMeasureConstraint(
                availableSize.Height,
                margin.Top,
                margin.Bottom,
                height,
                effectiveMaxHeight));
        var invalidationVersion = _measureInvalidationVersion;
        Size coreDesiredSize;
        _measurePassDepth++;
        try
        {
            coreDesiredSize = MeasureCore(coreConstraint);
        }
        finally
        {
            _measurePassDepth--;
        }

        VerifyCoreSize(coreDesiredSize, "Measurement returned an invalid desired size.");
        if (_measureInvalidationVersion != invalidationVersion)
            return;

        var desiredContentSize = new Size(
            ResolveDesiredDimension(coreDesiredSize.Width, width, minWidth, effectiveMaxWidth),
            ResolveDesiredDimension(coreDesiredSize.Height, height, minHeight, effectiveMaxHeight));
        var desiredSize = new Size(
            AddMargin(desiredContentSize.Width, margin.Left, margin.Right),
            AddMargin(desiredContentSize.Height, margin.Top, margin.Bottom));

        _lastMeasureConstraint = availableSize;
        _desiredContentSize = desiredContentSize;
        DesiredSize = desiredSize;
        IsMeasureValid = true;
    }

    /// <summary>
    /// Arranges this node within a final layout rectangle.
    /// </summary>
    /// <param name="finalRect">The final slot in parent or caller coordinates.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from another thread, measurement is invalid, or a
    /// layout property has an invalid value.
    /// </exception>
    public void Arrange(Rect finalRect)
    {
        VerifyLayoutAccess();
        VerifyLayoutProperties();
        if (!IsMeasureValid)
            throw new InvalidOperationException("A UI node must have a valid measure before it can be arranged.");
        if (IsArrangeValid && _lastArrangeRect == finalRect)
            return;

        InvalidateArrangeState();

        if (Visibility == Visibility.Collapsed)
        {
            _lastArrangeRect = finalRect;
            LayoutBounds = Rect.Zero;
            IsArrangeValid = true;
            _isHitTestLayoutValid = true;
            return;
        }

        var margin = Margin;
        var width = Width;
        var height = Height;
        var minWidth = MinWidth;
        var minHeight = MinHeight;
        var effectiveMaxWidth = Math.Max(minWidth, MaxWidth);
        var effectiveMaxHeight = Math.Max(minHeight, MaxHeight);
        var slotWidth = SubtractMargin(finalRect.Width, margin.Left, margin.Right);
        var slotHeight = SubtractMargin(finalRect.Height, margin.Top, margin.Bottom);
        var actualWidth = ResolveArrangeDimension(
            slotWidth,
            _desiredContentSize.Width,
            width,
            minWidth,
            effectiveMaxWidth,
            HorizontalAlignment == HorizontalAlignment.Stretch);
        var actualHeight = ResolveArrangeDimension(
            slotHeight,
            _desiredContentSize.Height,
            height,
            minHeight,
            effectiveMaxHeight,
            VerticalAlignment == VerticalAlignment.Stretch);
        var horizontalRemaining = slotWidth - actualWidth;
        var verticalRemaining = slotHeight - actualHeight;
        var x = finalRect.X + margin.Left + GetHorizontalOffset(horizontalRemaining);
        var y = finalRect.Y + margin.Top + GetVerticalOffset(verticalRemaining);
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new InvalidOperationException("Arrangement produced a non-finite layout origin.");

        var actualSize = new Size(actualWidth, actualHeight);
        var layoutBounds = new Rect(x, y, actualSize);
        var invalidationVersion = _arrangeInvalidationVersion;
        _arrangePassDepth++;
        try
        {
            ArrangeCore(actualSize);
        }
        finally
        {
            _arrangePassDepth--;
        }

        if (_arrangeInvalidationVersion != invalidationVersion)
            return;

        _lastArrangeRect = finalRect;
        LayoutBounds = layoutBounds;
        IsArrangeValid = true;
        _isHitTestLayoutValid = true;
    }

    /// <summary>
    /// Invalidates measurement and arrangement for this node and its ancestors.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from another thread.
    /// </exception>
    public void InvalidateMeasure()
    {
        VerifyLayoutAccess();
        InvalidateMeasureCore();
    }

    /// <summary>
    /// Invalidates arrangement for this node and its ancestors.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from another thread.
    /// </exception>
    public void InvalidateArrange()
    {
        VerifyLayoutAccess();
        InvalidateArrangeCore();
    }

    /// <summary>
    /// Measures the content of this node.
    /// </summary>
    /// <param name="availableSize">The content size available after framework constraints.</param>
    /// <returns>The finite non-negative desired content size, excluding margin.</returns>
    protected virtual Size MeasureCore(Size availableSize) =>
        Size.Zero;

    /// <summary>
    /// Arranges the content of this node within its final size.
    /// </summary>
    /// <param name="finalSize">The finite non-negative content size, excluding margin.</param>
    protected virtual void ArrangeCore(Size finalSize)
    {
    }

    private void VerifyLayoutAccess() =>
        Screen?.VerifyTreeAccess();

    private void VerifyPropertyAccess(UiProperty property)
    {
        if (property.Invalidation != UiPropertyInvalidation.None)
            VerifyLayoutAccess();
    }

    private void ApplyPropertyInvalidation(UiProperty property)
    {
        if ((property.Invalidation & UiPropertyInvalidation.Measure) != 0)
        {
            InvalidateMeasureCore();
            return;
        }

        if ((property.Invalidation & UiPropertyInvalidation.Arrange) != 0)
            InvalidateArrangeCore();
    }

    private void InvalidateMeasureCore()
    {
        for (var node = this; node is not null; node = node.Parent)
            node.InvalidateMeasureState();
    }

    private void InvalidateArrangeCore()
    {
        for (var node = this; node is not null; node = node.Parent)
            node.InvalidateArrangeState();
    }

    private void InvalidateMeasureState()
    {
        IsMeasureValid = false;
        IsArrangeValid = false;
        _isHitTestLayoutValid = false;
        if (_measurePassDepth != 0)
            _measureInvalidationVersion++;
        if (_arrangePassDepth != 0)
            _arrangeInvalidationVersion++;
    }

    private void InvalidateArrangeState()
    {
        IsArrangeValid = false;
        _isHitTestLayoutValid = false;
        if (_arrangePassDepth != 0)
            _arrangeInvalidationVersion++;
    }

    internal bool CanPreserveHitTestLayoutAfterChildOrderChange()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (!node._isHitTestLayoutValid)
                return false;
        }

        return true;
    }

    internal void RestoreHitTestLayoutAfterChildOrderChange()
    {
        for (var node = this; node is not null; node = node.Parent)
            node._isHitTestLayoutValid = true;
    }

    private void VerifyLayoutProperties()
    {
        VerifyRequestedDimension(Width, nameof(Width));
        VerifyRequestedDimension(Height, nameof(Height));
        VerifyMinimumDimension(MinWidth, nameof(MinWidth));
        VerifyMinimumDimension(MinHeight, nameof(MinHeight));
        VerifyMaximumDimension(MaxWidth, nameof(MaxWidth));
        VerifyMaximumDimension(MaxHeight, nameof(MaxHeight));

        if (HorizontalAlignment is not (
                HorizontalAlignment.Left or
                HorizontalAlignment.Center or
                HorizontalAlignment.Right or
                HorizontalAlignment.Stretch))
        {
            throw new InvalidOperationException("HorizontalAlignment has an undefined value.");
        }

        if (VerticalAlignment is not (
                VerticalAlignment.Top or
                VerticalAlignment.Center or
                VerticalAlignment.Bottom or
                VerticalAlignment.Stretch))
        {
            throw new InvalidOperationException("VerticalAlignment has an undefined value.");
        }

        if (Visibility is not (
                Visibility.Visible or
                Visibility.Hidden or
                Visibility.Collapsed))
        {
            throw new InvalidOperationException("Visibility has an undefined value.");
        }
    }

    private double GetHorizontalOffset(double remaining) =>
        HorizontalAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Center => remaining / 2,
            HorizontalAlignment.Right => remaining,
            HorizontalAlignment.Stretch => remaining == 0 ? 0 : remaining / 2,
            _ => throw new InvalidOperationException("HorizontalAlignment has an undefined value.")
        };

    private double GetVerticalOffset(double remaining) =>
        VerticalAlignment switch
        {
            VerticalAlignment.Top => 0,
            VerticalAlignment.Center => remaining / 2,
            VerticalAlignment.Bottom => remaining,
            VerticalAlignment.Stretch => remaining == 0 ? 0 : remaining / 2,
            _ => throw new InvalidOperationException("VerticalAlignment has an undefined value.")
        };

    private static double GetMeasureConstraint(
        double available,
        double leadingMargin,
        double trailingMargin,
        double requested,
        double effectiveMax)
    {
        var constraint = Math.Min(
            SubtractMargin(available, leadingMargin, trailingMargin),
            effectiveMax);
        return double.IsNaN(requested) ? constraint : Math.Min(constraint, requested);
    }

    private static double ResolveDesiredDimension(
        double coreDesired,
        double requested,
        double minimum,
        double effectiveMax) =>
        Math.Clamp(double.IsNaN(requested) ? coreDesired : requested, minimum, effectiveMax);

    private static double ResolveArrangeDimension(
        double slot,
        double desired,
        double requested,
        double minimum,
        double effectiveMax,
        bool stretches) =>
        double.IsNaN(requested)
            ? stretches ? Math.Clamp(slot, minimum, effectiveMax) : desired
            : Math.Clamp(requested, minimum, effectiveMax);

    private static double SubtractMargin(
        double available,
        double leadingMargin,
        double trailingMargin)
    {
        if (double.IsPositiveInfinity(available))
            return double.PositiveInfinity;

        var margin = leadingMargin + trailingMargin;
        return double.IsPositiveInfinity(margin) ? 0 : Math.Max(0, available - margin);
    }

    private static double AddMargin(
        double content,
        double leadingMargin,
        double trailingMargin)
    {
        var result = content + leadingMargin + trailingMargin;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Measurement produced a non-finite desired size.");

        return result;
    }

    private static void VerifyCoreSize(Size size, string message)
    {
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height))
            throw new InvalidOperationException(message);
    }

    private static void VerifyRequestedDimension(double value, string propertyName)
    {
        if (!double.IsNaN(value) && (!double.IsFinite(value) || value < 0))
            throw new InvalidOperationException($"{propertyName} must be Auto or a finite non-negative value.");
    }

    private static void VerifyMinimumDimension(double value, string propertyName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new InvalidOperationException($"{propertyName} must be a finite non-negative value.");
    }

    private static void VerifyMaximumDimension(double value, string propertyName)
    {
        if (double.IsNaN(value) || value < 0 || double.IsNegativeInfinity(value))
            throw new InvalidOperationException($"{propertyName} must be non-negative or positive infinity.");
    }

    partial void OnTreeStructureInvalidated()
    {
        InvalidateMeasureState();
    }
}
