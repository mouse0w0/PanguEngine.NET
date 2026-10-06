using PanguEngine.Client.UI.Input;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides the independent close affordance of a closable <see cref="TabItem"/>.
/// </summary>
internal sealed class TabCloseButton : Control
{
    private const double DefaultSize = 16;

    private readonly TabItem _item;
    private readonly Path _icon;

    internal TabCloseButton(TabItem item)
    {
        _item = item;
        Focusable = false;
        Classes.Add("tab-close-button");
        _icon = new Path { IsHitTestVisible = false };
        _icon.Classes.Add("tab-close-icon");
        Children.Add(_icon);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var width = Width;
        var height = Height;
        _icon.Measure(availableSize);
        return new Size(
            double.IsNaN(width) ? DefaultSize : width,
            double.IsNaN(height) ? DefaultSize : height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var width = Math.Min(_icon.DesiredSize.Width, contentBounds.Width);
        var height = Math.Min(_icon.DesiredSize.Height, contentBounds.Height);
        _icon.Arrange(new Rect(
            contentBounds.X + (contentBounds.Width - width) / 2,
            contentBounds.Y + (contentBounds.Height - height) / 2,
            width,
            height));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !IsEnabled)
            return;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        eventArgs.Handled = true;
        if (IsEnabled && !_item.IsDragging)
            _item.NotifyCloseRequested();
    }
}

/// <summary>
/// Scrolls a tab strip viewport by one viewport length in a single direction.
/// </summary>
internal sealed class TabScrollButton : Control
{
    private const double DefaultSize = 24;

    private readonly TabStripPanel _strip;
    private readonly int _direction;
    private readonly Path _arrow;

    internal TabScrollButton(TabStripPanel strip, int direction)
    {
        _strip = strip;
        _direction = direction;
        Focusable = false;
        Classes.Add("tab-scroll-button");
        Classes.Add(direction < 0 ? "tab-scroll-decrease" : "tab-scroll-increase");
        _arrow = new Path();
        _arrow.Classes.Add("tab-scroll-arrow");
        Children.Add(_arrow);
        SynchronizeOrientation();
    }

    internal void SynchronizeOrientation()
    {
        var horizontal = _strip.IsHorizontal;
        Classes.Remove(horizontal ? "tab-scroll-vertical" : "tab-scroll-horizontal");
        Classes.Add(horizontal ? "tab-scroll-horizontal" : "tab-scroll-vertical");
        _arrow.Classes.Remove(CreateArrowDirectionClass(!horizontal, _direction));
        _arrow.Classes.Add(CreateArrowDirectionClass(horizontal, _direction));
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var width = Width;
        var height = Height;
        _arrow.Measure(new Size(DefaultSize, DefaultSize));
        return new Size(
            double.IsNaN(width) ? DefaultSize : width,
            double.IsNaN(height) ? DefaultSize : height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var size = Math.Min(12, Math.Min(contentBounds.Width, contentBounds.Height));
        _arrow.Arrange(new Rect(
            contentBounds.X + (contentBounds.Width - size) / 2,
            contentBounds.Y + (contentBounds.Height - size) / 2,
            size,
            size));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !IsEnabled)
            return;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        eventArgs.Handled = true;
        if (IsEnabled)
            _strip.ScrollByViewport(_direction);
    }

    private static string CreateArrowDirectionClass(bool horizontal, int direction) =>
        horizontal
            ? direction < 0 ? "tab-scroll-arrow-left" : "tab-scroll-arrow-right"
            : direction < 0 ? "tab-scroll-arrow-up" : "tab-scroll-arrow-down";
}
