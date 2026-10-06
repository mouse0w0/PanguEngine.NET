namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Lays out tab headers along the strip axis and clips them to the visible strip region.
/// </summary>
internal sealed class TabStripViewport : Control
{
    private readonly TabView _owner;
    private readonly Panel _marker = new()
    {
        Focusable = false,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private double _scrollOffset;
    private double _tabSlotLength;
    private double _viewportLength;
    private bool _hasOverflow;
    private bool _markerVisible;
    private TabItem? _bringIntoView;
    private double _lastArrangedLength = -1;
    private TabItem? _lastSelected;
    private double _lastSelectedStart;
    private double _lastSelectedSize;
    private bool _hasSelectedRange;

    internal TabStripViewport(TabView owner)
    {
        _owner = owner;
        Classes.Add("tab-strip-viewport");
        ClipToBounds = true;
        _marker.Classes.Add("tab-insertion-marker");
        Children.Add(_marker);
        Children.Changed += (_, change) =>
        {
            if (SuppressChildRemovedNotification)
                return;
            foreach (var child in change.OldItems)
            {
                if (child is TabItem item && !ReferenceEquals(item.Parent, this))
                    _owner.OnTabRemovedFromStrip(item);
            }
        };
    }

    internal bool HasOverflow => _hasOverflow;

    internal double ViewportLength => _viewportLength;

    internal double ScrollOffset => _scrollOffset;

    internal bool SuppressChildRemovedNotification { get; set; }

    internal void InsertTab(int index, TabItem item) =>
        Children.Insert(Math.Min(index, Children.Count - 1), item);

    internal void RemoveTab(TabItem item)
    {
        SuppressChildRemovedNotification = true;
        try
        {
            Children.Remove(item);
        }
        finally
        {
            SuppressChildRemovedNotification = false;
        }
    }

    internal void MoveTabChild(int oldIndex, int newIndex)
    {
        Children.Move(oldIndex, newIndex);
    }

    internal int IndexOfTabChild(TabItem item) => Children.IndexOf(item);

    internal void SetMarkerVisible(bool visible)
    {
        if (_markerVisible == visible)
            return;
        _markerVisible = visible;
        _marker.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        InvalidateArrange();
    }

    internal double GetMaxOffset() =>
        Math.Max(0, ComputeTotalMain() - _viewportLength);

    internal void ScrollBy(double delta)
    {
        if (delta == 0)
            return;
        var offset = Math.Clamp(_scrollOffset + delta, 0, GetMaxOffset());
        if (offset == _scrollOffset)
            return;
        _scrollOffset = offset;
        InvalidateArrange();
    }

    internal void BringIntoView(TabItem item)
    {
        _bringIntoView = item;
        InvalidateArrange();
    }

    internal void CancelBringIntoView(TabItem item)
    {
        if (ReferenceEquals(_bringIntoView, item))
            _bringIntoView = null;
    }

    internal void ResetBringIntoView()
    {
        _bringIntoView = null;
        _lastArrangedLength = -1;
    }

    private void ApplyBringIntoView(TabItem item)
    {
        if (!TryGetItemRange(item, out var start, out var size))
            return;

        var offset = _scrollOffset;
        if (start < offset)
            offset = start;
        else if (start + size > offset + _viewportLength)
            offset = size > _viewportLength ? start : start + size - _viewportLength;

        _scrollOffset = Math.Clamp(offset, 0, GetMaxOffset());
    }

    private bool TryGetItemRange(TabItem item, out double start, out double size)
    {
        start = 0;
        size = 0;
        foreach (var child in Children)
        {
            if (child is not TabItem tab || tab.Visibility == Visibility.Collapsed)
                continue;
            var itemSize = _owner.IsHorizontalPlacement ? _tabSlotLength : tab.DesiredSize.Height;
            if (ReferenceEquals(tab, item))
            {
                size = itemSize;
                return true;
            }

            start += itemSize;
        }

        return false;
    }

    private bool HasSelectedRangeChanged()
    {
        if (!_hasSelectedRange || _lastSelected is null || !ReferenceEquals(_lastSelected, _owner.Selection.SelectedItem))
            return true;

        return !TryGetItemRange(_lastSelected, out var start, out var size) ||
               start != _lastSelectedStart ||
               size != _lastSelectedSize;
    }

    private void UpdateSelectedRangeSnapshot()
    {
        var selected = _owner.Selection.SelectedItem;
        _lastSelected = selected;
        if (selected is not null && TryGetItemRange(selected, out var start, out var size))
        {
            _lastSelectedStart = start;
            _lastSelectedSize = size;
            _hasSelectedRange = true;
            return;
        }

        _hasSelectedRange = false;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _marker.Measure(availableSize);
        var horizontal = _owner.IsHorizontalPlacement;
        var count = CountVisibleTabs();
        if (count == 0)
        {
            foreach (var child in Children)
            {
                if (child is TabItem item)
                    item.Measure(availableSize);
            }

            _hasOverflow = false;
            _tabSlotLength = 0;
            _viewportLength = 0;
            return Size.Zero;
        }

        if (horizontal)
        {
            var length = availableSize.Width;
            var cross = availableSize.Height;
            var (min, max) = _owner.GetTabWidthBounds();

            double slot;
            if (double.IsPositiveInfinity(length) || length >= count * max)
            {
                slot = max;
                _hasOverflow = false;
            }
            else if (length >= count * min)
            {
                slot = length / count;
                _hasOverflow = false;
            }
            else
            {
                slot = min;
                _hasOverflow = true;
            }

            _tabSlotLength = slot;
            var desiredCross = 0d;
            foreach (var child in Children)
            {
                if (child is not TabItem item)
                    continue;
                item.Measure(new Size(slot, cross));
                if (item.Visibility == Visibility.Collapsed)
                    continue;
                desiredCross = Math.Max(desiredCross, item.DesiredSize.Height);
            }

            _viewportLength = double.IsPositiveInfinity(length)
                ? count * slot
                : Math.Min(length, count * slot);
            var desiredMain = double.IsPositiveInfinity(length)
                ? count * slot
                : Math.Max(length, count * slot);
            return new Size(desiredMain, desiredCross);
        }
        else
        {
            var cross = availableSize.Width;
            var length = availableSize.Height;
            var total = 0d;
            var desiredCross = 0d;
            foreach (var child in Children)
            {
                if (child is not TabItem item)
                    continue;
                item.Measure(new Size(cross, double.PositiveInfinity));
                if (item.Visibility == Visibility.Collapsed)
                    continue;
                total += item.DesiredSize.Height;
                desiredCross = Math.Max(desiredCross, item.DesiredSize.Width);
            }

            _tabSlotLength = 0;
            _hasOverflow = !double.IsPositiveInfinity(length) && total > length;
            _viewportLength = double.IsPositiveInfinity(length) ? total : Math.Min(length, total);
            var desiredMain = double.IsPositiveInfinity(length) ? total : Math.Max(length, total);
            var resolvedCross = double.IsPositiveInfinity(cross) ? desiredCross : cross;
            return new Size(resolvedCross, desiredMain);
        }
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var horizontal = _owner.IsHorizontalPlacement;
        var length = horizontal ? contentBounds.Width : contentBounds.Height;
        var cross = horizontal ? contentBounds.Height : contentBounds.Width;
        _viewportLength = length;

        var target = _bringIntoView;
        if (target is null &&
            (length != _lastArrangedLength || HasSelectedRangeChanged()))
        {
            target = _owner.Selection.SelectedItem;
        }

        if (target is not null && length > 0)
            ApplyBringIntoView(target);
        if (length > 0)
            _bringIntoView = null;
        _lastArrangedLength = length;
        UpdateSelectedRangeSnapshot();

        var total = ComputeTotalMain();
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, total - length));

        var origin = horizontal ? contentBounds.X : contentBounds.Y;
        var crossOrigin = horizontal ? contentBounds.Y : contentBounds.X;
        var main = origin - _scrollOffset;

        foreach (var child in Children)
        {
            if (child is not TabItem item)
                continue;
            if (item.Visibility == Visibility.Collapsed)
            {
                item.Arrange(Rect.Zero);
                continue;
            }

            if (horizontal)
            {
                item.Arrange(new Rect(main, crossOrigin, _tabSlotLength, cross));
                main += _tabSlotLength;
            }
            else
            {
                var itemMain = item.DesiredSize.Height;
                item.Arrange(new Rect(crossOrigin, main, cross, itemMain));
                main += itemMain;
            }
        }

        ArrangeMarker(horizontal, cross, crossOrigin);
    }

    private void ArrangeMarker(bool horizontal, double cross, double crossOrigin)
    {
        if (!_markerVisible)
        {
            _marker.Arrange(Rect.Zero);
            return;
        }

        const double thickness = 2;
        var markerMain = _owner.GetArrangedDragMarkerMain();
        _marker.Arrange(horizontal
            ? new Rect(markerMain - thickness / 2, crossOrigin, thickness, cross)
            : new Rect(crossOrigin, markerMain - thickness / 2, cross, thickness));
    }

    private double ComputeTotalMain()
    {
        if (_owner.IsHorizontalPlacement)
            return CountVisibleTabs() * _tabSlotLength;

        var total = 0d;
        foreach (var child in Children)
        {
            if (child is TabItem item && item.Visibility != Visibility.Collapsed)
                total += item.DesiredSize.Height;
        }

        return total;
    }

    private int CountVisibleTabs()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child is TabItem item && item.Visibility != Visibility.Collapsed)
                count++;
        }

        return count;
    }

}
