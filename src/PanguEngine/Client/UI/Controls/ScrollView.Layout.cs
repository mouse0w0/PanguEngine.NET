namespace PanguEngine.Client.UI.Controls;

public sealed partial class ScrollView
{
    /// <summary>
    /// Holds one resolved layout pass of the scroll view.
    /// </summary>
    private readonly record struct ScrollLayoutSolution(
        Size Viewport,
        Size Extent,
        bool ShowHorizontal,
        bool ShowVertical,
        double HorizontalBarThickness,
        double VerticalBarThickness);

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _isInScrollLayout = true;
        try
        {
            var solution = Solve(availableSize);
            ApplySolution(solution);
            MeasureScrollChrome(solution);
            _measuredAvailableSize = availableSize;
            _isLayoutSolved = true;

            var occupiedWidth = AddBarOccupancy(
                solution.Extent.Width,
                solution.ShowVertical,
                solution.VerticalBarThickness);
            var occupiedHeight = AddBarOccupancy(
                solution.Extent.Height,
                solution.ShowHorizontal,
                solution.HorizontalBarThickness);

            return new Size(
                ConstrainDesired(occupiedWidth, availableSize.Width),
                ConstrainDesired(occupiedHeight, availableSize.Height));
        }
        finally
        {
            _isInScrollLayout = false;
        }
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        _isInScrollLayout = true;
        try
        {
            if (!_isLayoutSolved || contentBounds.Size != _measuredAvailableSize)
            {
                var solution = Solve(contentBounds.Size);
                ApplySolution(solution);
                MeasureScrollChrome(solution);
                _measuredAvailableSize = contentBounds.Size;
                _isLayoutSolved = true;
            }

            ArrangeViewportAndChrome(contentBounds);
        }
        finally
        {
            _isInScrollLayout = false;
        }
    }

    private ScrollLayoutSolution Solve(Size availableSize)
    {
        var horizontalThickness = _horizontalBar.BarThickness;
        var verticalThickness = _verticalBar.BarThickness;
        if (Screen?.UseLayoutRounding ?? true)
        {
            var scale = Screen?.Scale ?? 1;
            horizontalThickness = UiLayoutHelper.RoundLayoutValueUp(horizontalThickness, scale);
            verticalThickness = UiLayoutHelper.RoundLayoutValueUp(verticalThickness, scale);
        }
        var showHorizontal = HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
        var showVertical = VerticalScrollBarVisibility == ScrollBarVisibility.Visible;
        var viewport = Size.Zero;
        var extent = Size.Zero;

        for (var iteration = 0; iteration < 3; iteration++)
        {
            var constrainedViewport = CreateViewportSize(
                availableSize,
                showHorizontal,
                showVertical,
                horizontalThickness,
                verticalThickness);
            extent = MeasureContentNode(constrainedViewport);
            viewport = ResolveFiniteViewport(constrainedViewport, extent);

            var overflowHorizontal = IsHorizontalScrollable && extent.Width > viewport.Width;
            var overflowVertical = IsVerticalScrollable && extent.Height > viewport.Height;
            var nextShowHorizontal = showHorizontal ||
                (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && overflowHorizontal);
            var nextShowVertical = showVertical ||
                (VerticalScrollBarVisibility == ScrollBarVisibility.Auto && overflowVertical);

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
                break;

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        return new ScrollLayoutSolution(
            viewport,
            extent,
            showHorizontal,
            showVertical,
            horizontalThickness,
            verticalThickness);
    }

    private Size MeasureContentNode(Size viewport)
    {
        var constraint = new Size(
            IsHorizontalScrollable ? double.PositiveInfinity : viewport.Width,
            IsVerticalScrollable ? double.PositiveInfinity : viewport.Height);
        _viewport.ContentMeasureConstraint = constraint;
        _viewport.InvalidateMeasureSubtree();
        _viewport.Measure(viewport);
        return _viewport.DesiredSize;
    }

    private void ApplySolution(ScrollLayoutSolution solution)
    {
        _solution = solution;

        SetValue(ExtentPropertyKey, solution.Extent);
        SetValue(ViewportPropertyKey, solution.Viewport);

        _horizontalMaximum = IsHorizontalScrollable
            ? Math.Max(0, solution.Extent.Width - solution.Viewport.Width)
            : 0;
        _verticalMaximum = IsVerticalScrollable
            ? Math.Max(0, solution.Extent.Height - solution.Viewport.Height)
            : 0;
        _hasResolvedRange = true;

        _isSynchronizingBars = true;
        try
        {
            SetOffset(_offset);
            _viewport.Offset = Offset;
            SynchronizeBars(_horizontalBar, _verticalBar, solution, Offset);
        }
        finally
        {
            _isSynchronizingBars = false;
        }

        ApplyScrollChrome(solution);
    }

    private static void SynchronizeBars(
        ScrollBar horizontal,
        ScrollBar vertical,
        ScrollLayoutSolution solution,
        Point offset)
    {
        horizontal.LargeChange = solution.Viewport.Width;
        horizontal.SynchronizeRange(
            0,
            Math.Max(0, solution.Extent.Width - solution.Viewport.Width),
            solution.Viewport.Width,
            offset.X);
        vertical.LargeChange = solution.Viewport.Height;
        vertical.SynchronizeRange(
            0,
            Math.Max(0, solution.Extent.Height - solution.Viewport.Height),
            solution.Viewport.Height,
            offset.Y);
    }

    private void MeasureScrollChrome(ScrollLayoutSolution solution)
    {
        if (solution.ShowHorizontal)
        {
            _horizontalBar.Measure(new Size(
                ClampInfiniteConstraint(solution.Viewport.Width),
                solution.HorizontalBarThickness));
        }
        else
        {
            _horizontalBar.Measure(Size.Zero);
        }

        if (solution.ShowVertical)
        {
            _verticalBar.Measure(new Size(
                solution.VerticalBarThickness,
                ClampInfiniteConstraint(solution.Viewport.Height)));
        }
        else
        {
            _verticalBar.Measure(Size.Zero);
        }

        var cornerSize = solution.ShowHorizontal && solution.ShowVertical
            ? new Size(solution.VerticalBarThickness, solution.HorizontalBarThickness)
            : Size.Zero;
        _corner.Measure(cornerSize);
    }

    private void ArrangeViewportAndChrome(Rect contentBounds)
    {
        _viewport.Offset = Offset;

        var width = contentBounds.Width;
        var height = contentBounds.Height;
        var viewportWidth = SubtractLength(
            width,
            _solution.ShowVertical ? _solution.VerticalBarThickness : 0);
        var viewportHeight = SubtractLength(
            height,
            _solution.ShowHorizontal ? _solution.HorizontalBarThickness : 0);

        _viewport.Arrange(new Rect(contentBounds.X, contentBounds.Y, viewportWidth, viewportHeight));

        var horizontalRect = _solution.ShowHorizontal
            ? new Rect(
                contentBounds.X,
                contentBounds.Y + viewportHeight,
                viewportWidth,
                SubtractLength(height, viewportHeight))
            : Rect.Zero;
        var verticalRect = _solution.ShowVertical
            ? new Rect(
                contentBounds.X + viewportWidth,
                contentBounds.Y,
                SubtractLength(width, viewportWidth),
                viewportHeight)
            : Rect.Zero;

        _horizontalBar.Arrange(horizontalRect);
        _verticalBar.Arrange(verticalRect);
        var cornerRect = _solution.ShowHorizontal && _solution.ShowVertical
            ? new Rect(
                contentBounds.X + viewportWidth,
                contentBounds.Y + viewportHeight,
                verticalRect.Width,
                horizontalRect.Height)
            : Rect.Zero;
        _corner.Arrange(cornerRect);
    }

    private static Size CreateViewportSize(
        Size availableSize,
        bool showHorizontal,
        bool showVertical,
        double horizontalThickness,
        double verticalThickness) =>
        new(
            SubtractLength(availableSize.Width, showVertical ? verticalThickness : 0),
            SubtractLength(availableSize.Height, showHorizontal ? horizontalThickness : 0));

    private static Size ResolveFiniteViewport(Size viewport, Size extent) =>
        new(
            double.IsPositiveInfinity(viewport.Width) ? extent.Width : viewport.Width,
            double.IsPositiveInfinity(viewport.Height) ? extent.Height : viewport.Height);

    private static double AddBarOccupancy(double extent, bool showBar, double barThickness) =>
        showBar ? extent + barThickness : extent;

    private static double ConstrainDesired(double occupied, double available)
    {
        if (double.IsPositiveInfinity(available))
            return occupied;
        return occupied < available ? occupied : available;
    }

    private static double SubtractLength(double available, double reserved)
    {
        if (double.IsPositiveInfinity(available))
            return double.PositiveInfinity;

        var result = available - reserved;
        return result > 0 ? result : 0;
    }

    private static double ClampInfiniteConstraint(double value) =>
        double.IsPositiveInfinity(value) ? 0 : value;
}
