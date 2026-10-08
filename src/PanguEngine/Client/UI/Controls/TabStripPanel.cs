using PanguEngine.Client.UI.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Hosts a tab strip viewport together with its overflow scroll buttons.
/// </summary>
internal sealed class TabStripPanel : Control
{
    private readonly TabView _owner;
    private readonly TabStripViewport _viewport;
    private readonly TabScrollButton _decrease;
    private readonly TabScrollButton _increase;
    private double _buttonMainLength;
    private double _buttonCrossLength;
    private bool _hasOverflow;

    internal TabStripPanel(TabView owner)
    {
        _owner = owner;
        Classes.Add("tab-strip");
        _viewport = new TabStripViewport(owner);
        _decrease = new TabScrollButton(this, -1);
        _increase = new TabScrollButton(this, 1);
        Children.Add(_viewport);
        Children.Add(_decrease);
        Children.Add(_increase);
    }

    internal TabStripViewport Viewport => _viewport;
    internal bool IsHorizontal => _owner.IsHorizontalPlacement;

    internal void SynchronizeOrientation()
    {
        _decrease.SynchronizeOrientation();
        _increase.SynchronizeOrientation();
    }

    internal void ScrollByViewport(int direction)
    {
        var step = _viewport.ViewportLength;
        if (step <= 0)
            return;
        _viewport.ScrollBy(direction * step);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var horizontal = _owner.IsHorizontalPlacement;
        var length = horizontal ? availableSize.Width : availableSize.Height;
        var cross = horizontal ? availableSize.Height : availableSize.Width;

        _viewport.Measure(availableSize);
        _hasOverflow = _viewport.HasOverflow;
        _decrease.Measure(availableSize);
        _increase.Measure(availableSize);

        if (_hasOverflow)
        {
            _buttonMainLength = horizontal
                ? _decrease.DesiredSize.Width + _increase.DesiredSize.Width
                : _decrease.DesiredSize.Height + _increase.DesiredSize.Height;
            _buttonCrossLength = horizontal
                ? Math.Max(_decrease.DesiredSize.Height, _increase.DesiredSize.Height)
                : Math.Max(_decrease.DesiredSize.Width, _increase.DesiredSize.Width);

            var reducedMain = Math.Max(0, Subtract(length, _buttonMainLength));
            _viewport.Measure(horizontal
                ? new Size(reducedMain, cross)
                : new Size(cross, reducedMain));
        }
        else
        {
            _buttonMainLength = 0;
            _buttonCrossLength = 0;
        }

        var viewportDesired = _viewport.DesiredSize;
        var viewportMain = horizontal ? viewportDesired.Width : viewportDesired.Height;
        var viewportCross = horizontal ? viewportDesired.Height : viewportDesired.Width;
        var desiredMain = double.IsPositiveInfinity(length)
            ? AddFinite(viewportMain, _buttonMainLength)
            : length;
        var desiredCross = Math.Max(viewportCross, _buttonCrossLength);
        return horizontal
            ? new Size(desiredMain, desiredCross)
            : new Size(desiredCross, desiredMain);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var horizontal = _owner.IsHorizontalPlacement;
        var length = horizontal ? contentBounds.Width : contentBounds.Height;

        if (_hasOverflow && _buttonMainLength > 0 && _buttonMainLength < length)
        {
            if (horizontal)
            {
                var decreaseWidth = _decrease.DesiredSize.Width;
                var increaseWidth = _increase.DesiredSize.Width;
                _decrease.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y, decreaseWidth, contentBounds.Height));
                var viewportX = contentBounds.X + decreaseWidth;
                var viewportWidth = Math.Max(0, contentBounds.Width - _buttonMainLength);
                _viewport.Arrange(new Rect(
                    viewportX, contentBounds.Y, viewportWidth, contentBounds.Height));
                _increase.Arrange(new Rect(
                    viewportX + viewportWidth, contentBounds.Y, increaseWidth, contentBounds.Height));
            }
            else
            {
                var decreaseHeight = _decrease.DesiredSize.Height;
                var increaseHeight = _increase.DesiredSize.Height;
                _decrease.Arrange(new Rect(
                    contentBounds.X, contentBounds.Y, contentBounds.Width, decreaseHeight));
                var viewportY = contentBounds.Y + decreaseHeight;
                var viewportHeight = Math.Max(0, contentBounds.Height - _buttonMainLength);
                _viewport.Arrange(new Rect(
                    contentBounds.X, viewportY, contentBounds.Width, viewportHeight));
                _increase.Arrange(new Rect(
                    contentBounds.X, viewportY + viewportHeight, contentBounds.Width, increaseHeight));
            }
        }
        else
        {
            _viewport.Arrange(contentBounds);
            _decrease.Arrange(Rect.Zero);
            _increase.Arrange(Rect.Zero);
        }

        UpdateScrollButtons();
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(UiPointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheel(eventArgs);
        if (eventArgs.Handled || !_viewport.HasOverflow)
            return;

        var lines = UiToolkit.WheelScrollLines;
        if (lines == 0)
            return;

        var delta = _owner.IsHorizontalPlacement
            ? (eventArgs.DeltaX != 0 ? eventArgs.DeltaX : eventArgs.DeltaY)
            : eventArgs.DeltaY;
        if (delta == 0)
            return;

        var step = UiToolkit.GetWheelScrollStep(lines, TabView.WheelSmallChange, _viewport.ViewportLength);
        _viewport.ScrollBy(-delta * step);
        eventArgs.Handled = true;
    }

    private void UpdateScrollButtons()
    {
        var buttonVisibility = _hasOverflow ? Visibility.Visible : Visibility.Hidden;
        _decrease.Visibility = buttonVisibility;
        _increase.Visibility = buttonVisibility;
        var max = _viewport.GetMaxOffset();
        var offset = _viewport.ScrollOffset;
        var canDecrease = offset > 0;
        var canIncrease = offset < max;
        if (_decrease.IsEnabled != canDecrease)
            _decrease.IsEnabled = canDecrease;
        if (_increase.IsEnabled != canIncrease)
            _increase.IsEnabled = canIncrease;
    }

    private static double Subtract(double available, double amount) =>
        double.IsPositiveInfinity(available) ? double.PositiveInfinity : Math.Max(0, available - amount);

    private static double AddFinite(double first, double second)
    {
        var result = first + second;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Tab strip layout produced a non-finite size.");
        return result;
    }
}
