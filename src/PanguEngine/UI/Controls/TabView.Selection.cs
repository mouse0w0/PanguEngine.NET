using PanguEngine.ComponentModel;
using PanguEngine.UI.Selection;

namespace PanguEngine.UI.Controls;

public sealed partial class TabView
{
    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Property, SingleSelectionModel<TabItem>.SelectedItemProperty))
            return;

        var selection = (PropertyChangedEventArgs<TabItem?>)eventArgs;
        if (_collectionNotificationPending)
            return;
        var oldIndex = selection.OldValue is { } oldItem ? Items.IndexOf(oldItem) : -1;
        UpdateSelection(selection.OldValue, selection.NewValue, oldIndex);
    }

    private void UpdateSelection(TabItem? oldItem, TabItem? newItem, int oldIndex)
    {
        var focusWasInOldContent = oldItem?.ContentHost is { } oldHost &&
                                   IsDescendantOrSelf(Screen?.FocusedNode, oldHost);

        _selectionUpdateDepth++;
        try
        {
            oldItem?.SetSelected(false);
            newItem?.SetSelected(true);
            SetContentVisibility(oldItem, false);
            SetContentVisibility(newItem, true);

            var newIndex = newItem is null ? -1 : Items.IndexOf(newItem);
            SelectionChanged?.Invoke(
                this,
                new TabSelectionChangedEventArgs(oldItem, newItem, oldIndex, newIndex));

            if (newItem is not null && ReferenceEquals(Selection.SelectedItem, newItem))
                BringItemIntoView(newItem);
            if (newItem is not null &&
                ReferenceEquals(Selection.SelectedItem, newItem) &&
                focusWasInOldContent)
                newItem.Focus();
        }
        finally
        {
            _selectionUpdateDepth--;
        }
    }

    private void SetContentVisibility(TabItem? item, bool visible)
    {
        if (item?.ContentHost is { } host)
            host.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BringItemIntoView(TabItem item) =>
        _strip.Viewport.BringIntoView(item);

    private void CancelPendingBringIntoViewFor(TabItem item) =>
        _strip.Viewport.CancelBringIntoView(item);

    private TabItem? FindNearestAvailable(int startIndex, TabItem? excluded)
    {
        startIndex = Math.Max(0, startIndex);
        for (var index = startIndex; index < Items.Count; index++)
        {
            var item = Items[index];
            if (!ReferenceEquals(item, excluded) && item.IsEnabled)
                return item;
        }

        for (var index = startIndex - 1; index >= 0; index--)
        {
            var item = Items[index];
            if (!ReferenceEquals(item, excluded) && item.IsEnabled)
                return item;
        }

        return null;
    }

    private static bool IsAvailable(TabItem item) =>
        item.IsEnabled && item.Visibility == Visibility.Visible;

    private static bool IsDescendantOrSelf(UIElement? node, UIElement ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    private bool IsFocusInsideItem(TabItem item)
    {
        if (Screen?.FocusedNode is not { } focused)
            return false;

        return IsDescendantOrSelf(focused, item) ||
               (item.ContentHost is { } host && IsDescendantOrSelf(focused, host));
    }
}