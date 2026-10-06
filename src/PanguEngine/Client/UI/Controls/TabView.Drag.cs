namespace PanguEngine.Client.UI.Controls;

public sealed partial class TabView
{
    private TabItem? _candidateItem;
    private TabItem? _dragItem;
    private bool _isDragging;
    private Point _dragStart;
    private Point _lastPointer;
    private int _insertionIndex;
    private bool _suppressClick;
    private bool _hasFrameBaseline;
    private TimeSpan _lastFrameTime;

    private void BeginDrag(Point viewportPosition)
    {
        var item = _candidateItem;
        _candidateItem = null;
        if (item is null || !IsAvailable(item))
            return;

        _isDragging = true;
        _dragItem = item;
        item.SetDragging(true);
        _lastPointer = viewportPosition;
        _insertionIndex = Items.IndexOf(item);
        _hasFrameBaseline = false;
        _dragTicker.Start();
        UpdateDragFromPointer();
        _strip.Viewport.SetMarkerVisible(true);
    }

    private void UpdateDragFromPointer()
    {
        var viewport = _strip.Viewport;
        if (viewport.IsArrangeValid)
            _insertionIndex = ComputeInsertionIndex(GetMain(_lastPointer));
        viewport.InvalidateArrange();
    }

    private void EndDrag(bool commit)
    {
        _candidateItem = null;
        if (!_isDragging)
            return;

        var item = _dragItem!;
        var oldIndex = Items.IndexOf(item);
        var newIndex = _insertionIndex;

        _isDragging = false;
        _dragItem = null;
        item.SetDragging(false);
        _dragTicker.Stop();
        _strip.Viewport.SetMarkerVisible(false);
        _hasFrameBaseline = false;
        _suppressClick = true;

        if (commit && oldIndex >= 0 && newIndex >= 0 && newIndex != oldIndex)
            CommitDragMove(oldIndex, newIndex);
    }

    private void CancelDrag()
    {
        _candidateItem = null;
        if (_isDragging)
            EndDrag(commit: false);
    }

    private void OnDragTick(TimeSpan frameTime)
    {
        if (!_isDragging)
            return;

        if (_dragItem is not { IsPressed: true })
        {
            CancelDrag();
            return;
        }

        var delta = _hasFrameBaseline ? Math.Max(0, (frameTime - _lastFrameTime).TotalSeconds) : 0;
        _lastFrameTime = frameTime;
        _hasFrameBaseline = true;
        if (delta <= 0)
            return;

        AutoScroll(delta);
    }

    private void AutoScroll(double deltaSeconds)
    {
        var viewport = _strip.Viewport;
        if (!viewport.HasOverflow)
            return;

        var length = viewport.ViewportLength;
        if (length <= 0)
            return;

        var edge = Math.Min(AutoScrollEdge, length / 2);
        var main = GetMain(_lastPointer);
        if (main < edge)
            viewport.ScrollBy(-AutoScrollSpeed * deltaSeconds);
        else if (main > length - edge)
            viewport.ScrollBy(AutoScrollSpeed * deltaSeconds);
    }

    internal double GetArrangedDragMarkerMain()
    {
        _insertionIndex = ComputeInsertionIndex(GetMain(_lastPointer));
        return ComputeMarkerMain(_insertionIndex);
    }

    private int ComputeInsertionIndex(double pointerMain)
    {
        var slot = 0;
        var counted = 0;
        var resolved = false;
        foreach (var item in Items)
        {
            if (ReferenceEquals(item, _dragItem))
                continue;

            if (!resolved &&
                item.IsArrangeValid &&
                item.Visibility != Visibility.Collapsed &&
                pointerMain < GetItemMainCenter(item))
            {
                slot = counted;
                resolved = true;
            }

            counted++;
        }

        return resolved ? slot : counted;
    }

    private double ComputeMarkerMain(int insertionIndex)
    {
        var nonDrag = new List<TabItem>();
        foreach (var item in Items)
        {
            if (!ReferenceEquals(item, _dragItem))
                nonDrag.Add(item);
        }

        for (var index = insertionIndex; index < nonDrag.Count; index++)
        {
            var item = nonDrag[index];
            if (item.IsArrangeValid && item.Visibility != Visibility.Collapsed)
                return GetItemStartMain(item);
        }

        for (var index = nonDrag.Count - 1; index >= 0; index--)
        {
            var item = nonDrag[index];
            if (item.IsArrangeValid && item.Visibility != Visibility.Collapsed)
                return GetItemStartMain(item) + GetItemMainLength(item);
        }

        return 0;
    }

    private double GetItemMainCenter(TabItem item)
    {
        var horizontal = IsHorizontalPlacement;
        return horizontal
            ? item.LayoutBounds.X + item.LayoutBounds.Width / 2
            : item.LayoutBounds.Y + item.LayoutBounds.Height / 2;
    }

    private double GetItemStartMain(TabItem item) =>
        IsHorizontalPlacement ? item.LayoutBounds.X : item.LayoutBounds.Y;

    private double GetItemMainLength(TabItem item) =>
        IsHorizontalPlacement ? item.LayoutBounds.Width : item.LayoutBounds.Height;

    private double GetMain(Point point) => IsHorizontalPlacement ? point.X : point.Y;
}
