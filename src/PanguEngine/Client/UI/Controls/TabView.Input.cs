using PanguEngine.Client.UI.Input;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

public sealed partial class TabView
{
    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        _suppressClick = false;
        if (!CanReorderTabs || _isDragging || eventArgs.Handled)
            return;

        var tab = FindTabItem(eventArgs.Source);
        if (tab is null || !IsAvailable(tab) || IsInteractiveChild(eventArgs.Source, tab))
            return;

        _candidateItem = tab;
        _dragStart = eventArgs.GetPosition(this);
        _lastPointer = eventArgs.GetPosition(_strip.Viewport);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(UiPointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (_isDragging)
        {
            _lastPointer = eventArgs.GetPosition(_strip.Viewport);
            UpdateDragFromPointer();
            eventArgs.Handled = true;
            return;
        }

        if (_candidateItem is null)
            return;
        if (!_candidateItem.IsPressed)
        {
            CancelDrag();
            return;
        }

        var position = eventArgs.GetPosition(this);
        var deltaX = position.X - _dragStart.X;
        var deltaY = position.Y - _dragStart.Y;
        if (deltaX * deltaX + deltaY * deltaY < DragThreshold * DragThreshold)
            return;

        BeginDrag(eventArgs.GetPosition(_strip.Viewport));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        if (_isDragging)
        {
            EndDrag(IsWithinStrip(eventArgs.GetPosition(this)));
            eventArgs.Handled = true;
            return;
        }

        _candidateItem = null;
    }

    protected override void OnPointerCanceled(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerCanceled(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        if (_isDragging)
        {
            EndDrag(commit: true);
            eventArgs.Handled = true;
        }
        else
        {
            _candidateItem = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        if (_suppressClick)
        {
            _suppressClick = false;
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Handled)
            return;

        var tab = FindTabItem(eventArgs.Source);
        if (tab is null || !IsAvailable(tab) || IsInteractiveChild(eventArgs.Source, tab))
            return;

        SelectItemFromInteraction(tab);
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled)
            return;

        if (eventArgs.Key == Key.Escape)
        {
            if (_isDragging || _candidateItem is not null)
            {
                CancelDrag();
                _suppressClick = true;
                eventArgs.Handled = true;
            }

            return;
        }

        var control = (eventArgs.Modifiers & KeyModifiers.Control) != 0;
        var shift = (eventArgs.Modifiers & KeyModifiers.Shift) != 0;

        if (control && eventArgs.Key == Key.Tab)
        {
            CycleSelection(shift ? -1 : 1);
            eventArgs.Handled = true;
            return;
        }

        if (control && eventArgs.Key == Key.F4)
        {
            if (!eventArgs.IsRepeat)
                RequestCloseSelected();
            eventArgs.Handled = true;
            return;
        }

        var focused = GetFocusedTabItem();
        if (focused is null)
            return;

        switch (eventArgs.Key)
        {
            case Key.Left when IsHorizontalPlacement:
                MoveFocus(focused, -1);
                eventArgs.Handled = true;
                break;
            case Key.Right when IsHorizontalPlacement:
                MoveFocus(focused, 1);
                eventArgs.Handled = true;
                break;
            case Key.Up when !IsHorizontalPlacement:
                MoveFocus(focused, -1);
                eventArgs.Handled = true;
                break;
            case Key.Down when !IsHorizontalPlacement:
                MoveFocus(focused, 1);
                eventArgs.Handled = true;
                break;
            case Key.Home:
                FocusEdge(last: false);
                eventArgs.Handled = true;
                break;
            case Key.End:
                FocusEdge(last: true);
                eventArgs.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (!eventArgs.IsRepeat)
                    SelectItemFromInteraction(focused);
                eventArgs.Handled = true;
                break;
        }
    }

    internal void RequestClose(TabItem item)
    {
        if (!ReferenceEquals(item.OwnerView, this))
            return;
        if (!item.IsClosable || !IsAvailable(item))
            return;

        TabCloseRequested?.Invoke(this, new TabCloseRequestedEventArgs(item));
    }

    private void RequestCloseSelected()
    {
        var selected = Selection.SelectedItem;
        if (selected is not null)
            RequestClose(selected);
    }

    private void SelectItemFromInteraction(TabItem item)
    {
        if (!ReferenceEquals(Selection.SelectedItem, item))
            Selection.SelectedItem = item;
        item.Focus();
    }

    private void MoveFocus(TabItem from, int direction)
    {
        var available = GetAvailableTabs();
        var index = available.IndexOf(from);
        if (index < 0)
            return;
        var target = index + direction;
        if ((uint)target >= (uint)available.Count)
            return;

        var item = available[target];
        BringItemIntoView(item);
        item.Focus();
    }

    private void FocusEdge(bool last)
    {
        var available = GetAvailableTabs();
        if (available.Count == 0)
            return;

        var item = last ? available[^1] : available[0];
        BringItemIntoView(item);
        item.Focus();
    }

    private void CycleSelection(int direction)
    {
        var available = GetAvailableTabs();
        if (available.Count == 0)
            return;
        var current = Selection.SelectedItem is null ? -1 : available.IndexOf(Selection.SelectedItem);
        var next = current < 0
            ? 0
            : ((current + direction) % available.Count + available.Count) % available.Count;
        var item = available[next];
        Selection.SelectedItem = item;
        BringItemIntoView(item);
        item.Focus();
    }

    private List<TabItem> GetAvailableTabs()
    {
        var result = new List<TabItem>();
        foreach (var item in Items)
        {
            if (IsAvailable(item))
                result.Add(item);
        }

        return result;
    }

    private TabItem? GetFocusedTabItem()
    {
        return Screen?.FocusedNode is TabItem tab && ReferenceEquals(tab.OwnerView, this)
            ? tab
            : null;
    }

    private TabItem? FindTabItem(UiNode? source)
    {
        for (var node = source; node is not null; node = node.Parent)
        {
            if (node is TabItem tab && ReferenceEquals(tab.OwnerView, this))
                return tab;
            if (ReferenceEquals(node, this))
                break;
        }

        return null;
    }

    private static bool IsInteractiveChild(UiNode? source, TabItem tab)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, tab); node = node.Parent)
        {
            if (node is Control { Focusable: true })
                return true;
        }

        return false;
    }

    private bool IsWithinStrip(Point position)
    {
        var bounds = _strip.LayoutBounds;
        return position.X >= bounds.X &&
               position.Y >= bounds.Y &&
               position.X < bounds.X + bounds.Width &&
               position.Y < bounds.Y + bounds.Height;
    }
}
