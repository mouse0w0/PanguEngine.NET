using PanguEngine.Collections;

namespace PanguEngine.Client.UI.Controls;

public sealed partial class TabView
{
    private TabItem? _selectionBeforeCollectionChange;
    private int _selectionBeforeCollectionIndex = -1;
    private bool _focusWasRemovedByCollection;

    internal void BeginCollectionNotification() => _collectionNotificationPending = true;

    internal void InsertItemFromCollection(
        int index,
        IReadOnlyList<TabItem> items,
        Action updateStorage)
    {
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        if ((uint)index > (uint)Items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        ValidateNewItems(items);
        CancelDrag();
        CaptureCollectionState([]);

        _collectionMutationDepth++;
        try
        {
            foreach (var item in items)
                item.OwnerView = this;
            updateStorage();
            for (var offset = 0; offset < items.Count; offset++)
            {
                var item = items[offset];
                item.ContentHost.Visibility = Visibility.Collapsed;
                _strip.Viewport.InsertTab(index + offset, item);
                _contentArea.InsertHost(index + offset, item.ContentHost);
            }
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    internal void RemoveItemFromCollection(
        int index,
        int count,
        Action updateStorage)
    {
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        if ((uint)index > (uint)Items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0 || count > Items.Count - index)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return;

        var removed = Items.Skip(index).Take(count).ToArray();
        CaptureCollectionState(removed);
        CancelDrag();

        _collectionMutationDepth++;
        try
        {
            updateStorage();
            foreach (var item in removed)
            {
                if (ReferenceEquals(item.Parent, _strip.Viewport))
                    _strip.Viewport.RemoveTab(item);
                if (ReferenceEquals(item.ContentHost.Parent, _contentArea))
                    _contentArea.RemoveHost(item.ContentHost);
                item.OwnerView = null;
                CancelPendingBringIntoViewFor(item);
            }
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    internal void ReplaceItemFromCollection(int index, TabItem item, Action updateStorage)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        if ((uint)index >= (uint)Items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var oldItem = Items[index];
        if (ReferenceEquals(oldItem, item))
        {
            CaptureCollectionState([]);
            return;
        }
        ValidateNewItems([item]);
        CaptureCollectionState([oldItem]);
        CancelDrag();

        _collectionMutationDepth++;
        try
        {
            item.OwnerView = this;
            updateStorage();
            if (ReferenceEquals(oldItem.Parent, _strip.Viewport))
                _strip.Viewport.RemoveTab(oldItem);
            if (ReferenceEquals(oldItem.ContentHost.Parent, _contentArea))
                _contentArea.RemoveHost(oldItem.ContentHost);
            oldItem.OwnerView = null;
            CancelPendingBringIntoViewFor(oldItem);
            item.ContentHost.Visibility = Visibility.Collapsed;
            _strip.Viewport.InsertTab(index, item);
            _contentArea.InsertHost(index, item.ContentHost);
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    internal void ReplaceItemsFromCollection(IReadOnlyList<TabItem> items, Action updateStorage)
    {
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        var replacedItems = new HashSet<TabItem>(Items, ReferenceEqualityComparer.Instance);
        ValidateNewItems(items, replacedItems);
        var removed = Items.ToArray();
        CaptureCollectionState(removed);
        CancelDrag();

        _collectionMutationDepth++;
        try
        {
            foreach (var item in items)
                item.OwnerView = this;
            updateStorage();
            foreach (var item in removed)
            {
                if (ReferenceEquals(item.Parent, _strip.Viewport))
                    _strip.Viewport.RemoveTab(item);
                if (ReferenceEquals(item.ContentHost.Parent, _contentArea))
                    _contentArea.RemoveHost(item.ContentHost);
                item.OwnerView = null;
                CancelPendingBringIntoViewFor(item);
            }

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                item.ContentHost.Visibility = Visibility.Collapsed;
                _strip.Viewport.InsertTab(index, item);
                _contentArea.InsertHost(index, item.ContentHost);
            }
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    internal void MoveItemFromCollection(int oldIndex, int newIndex, Action updateStorage)
    {
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        if ((uint)oldIndex >= (uint)Items.Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if ((uint)newIndex >= (uint)Items.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex)
            return;
        CaptureCollectionState([]);
        CancelDrag();

        _collectionMutationDepth++;
        try
        {
            updateStorage();
            _strip.Viewport.MoveTabChild(oldIndex, newIndex);
            _contentArea.MoveHost(oldIndex, newIndex);
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    internal void ReorderItemsFromCollection(IReadOnlyList<int> permutation, Action updateStorage)
    {
        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        if (permutation.Count != Items.Count)
            throw new ArgumentException("The reorder permutation must match the item count.", nameof(permutation));

        var oldItems = Items.ToArray();
        CaptureCollectionState([]);
        CancelDrag();
        var oldIndexForNewIndex = new int[permutation.Count];
        for (var oldIndex = 0; oldIndex < permutation.Count; oldIndex++)
            oldIndexForNewIndex[permutation[oldIndex]] = oldIndex;
        _collectionMutationDepth++;
        try
        {
            updateStorage();
            for (var newIndex = 0; newIndex < oldItems.Length; newIndex++)
            {
                var oldIndex = oldIndexForNewIndex[newIndex];
                var item = oldItems[oldIndex];
                var currentIndex = _strip.Viewport.IndexOfTabChild(item);
                if (currentIndex == newIndex)
                    continue;
                _strip.Viewport.MoveTabChild(currentIndex, newIndex);
                _contentArea.MoveHost(currentIndex, newIndex);
            }
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }

    private void CaptureCollectionState(IReadOnlyList<TabItem> removed)
    {
        _selectionBeforeCollectionChange = Selection.SelectedItem;
        _selectionBeforeCollectionIndex = Selection.SelectedIndex;
        _focusWasRemovedByCollection = removed.Any(IsFocusInsideItem);
    }

    private void OnItemsChanged(object? sender, ListChangedEventArgs<TabItem> change)
    {
        var previousSelection = _selectionBeforeCollectionChange;
        _selectionBeforeCollectionChange = null;
        var previousIndex = _selectionBeforeCollectionIndex;
        _selectionBeforeCollectionIndex = -1;

        var selectionWasRemoved = previousSelection is not null &&
            change.OldItems.Any(item => ReferenceEquals(item, previousSelection));
        var shouldChooseFirst = previousSelection is null && change.Kind == ListChangeKind.Add;
        if (Selection.SelectedItem is null && (selectionWasRemoved || shouldChooseFirst))
        {
            var candidate = FindNearestAvailable(change.Index, null);
            if (candidate is not null)
                Selection.Select(Items.IndexOf(candidate));
        }

        if (_focusWasRemovedByCollection)
        {
            _focusWasRemovedByCollection = false;
            var target = Selection.SelectedItem;
            if (target is not null && IsAvailable(target))
                target.Focus();
            else
                Screen?.ClearFocus();
        }

        var finalSelection = Selection.SelectedItem;
        _collectionNotificationPending = false;
        _pendingOldSelectionIndex = -1;
        if (!ReferenceEquals(previousSelection, finalSelection))
            UpdateSelection(previousSelection, finalSelection, previousIndex);
    }

    internal void OnItemStateChanged(TabItem item)
    {
        if (!ReferenceEquals(item.OwnerView, this))
            return;
        if (_collectionMutationDepth > 0 || _selectionUpdateDepth > 0)
            return;

        var index = Items.IndexOf(item);
        if (index < 0)
            return;

        if (ReferenceEquals(_candidateItem, item) && !IsAvailable(item))
            _candidateItem = null;

        if (ReferenceEquals(_dragItem, item) && !IsAvailable(item))
            CancelDrag();

        if (ReferenceEquals(Selection.SelectedItem, item) && !item.IsEnabled)
            Selection.SelectedItem = FindNearestAvailable(index, item);

        if (IsFocusInsideItem(item) && !IsAvailable(item))
        {
            var target = Selection.SelectedItem;
            if (target is not null && IsAvailable(target))
                target.Focus();
            else
                Screen?.ClearFocus();
        }
    }

    private void EnsureCollectionAccess()
    {
        if (_collectionMutationDepth > 0 || _selectionUpdateDepth > 0)
            throw new InvalidOperationException(
                "The tab item collection cannot change during a tab collection or selection notification.");
    }

    private void ValidateNewItems(
        IReadOnlyList<TabItem> items,
        IReadOnlySet<TabItem>? replacedItems = null)
    {
        var seen = new HashSet<TabItem>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!seen.Add(item) ||
                (Items.Contains(item) && (replacedItems is null || !replacedItems.Contains(item))))
                throw new InvalidOperationException("The tab item already belongs to this view.");
            if (item.OwnerView is not null && !ReferenceEquals(item.OwnerView, this))
                throw new InvalidOperationException("The tab item already belongs to another view.");

            Screen?.VerifyTreeMutationAccess();
            item.Screen?.VerifyTreeMutationAccess();
            if (item.Parent is null && item.Screen is not null)
                throw new InvalidOperationException("A UI screen root must be cleared before it can join a tab view.");
            if (IsAncestorOfView(item))
                throw new InvalidOperationException("Adding the tab item would create a parent cycle.");

            ValidateSlotNode(item.Header);
            ValidateSlotNode(item.Content);
        }
    }

    private void ValidateSlotNode(UiNode? node)
    {
        if (node is null)
            return;
        if (TabView.IsInternalPartNode(node))
            throw new InvalidOperationException("A tab view internal node cannot be used as a tab slot.");
        if (node.Parent is null && node.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can be a tab slot.");
        if (IsAncestorOfView(node))
            throw new InvalidOperationException("A tab slot node cannot contain the tab view.");
    }

    private bool IsAncestorOfView(UiNode node)
    {
        for (UiNode? current = this; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, node))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reconciles a tab item that left the strip through an external tree operation.
    /// </summary>
    internal void OnTabRemovedFromStrip(TabItem item)
    {
        var index = Items.IndexOf(item);
        if (index < 0)
            return;

        EnsureCollectionAccess();
        Screen?.VerifyTreeMutationAccess();
        Items.Remove(item);
    }

    internal void OnContentHostRemoved(TabContentHost host)
    {
        var item = Items.FirstOrDefault(candidate => ReferenceEquals(candidate.ContentHost, host));
        if (item is not null)
            Items.Remove(item);
    }

    private void CommitDragMove(int oldIndex, int newIndex)
    {
        Items.Move(oldIndex, newIndex);
        _collectionMutationDepth++;
        try
        {
            var item = Items[newIndex];
            TabReordered?.Invoke(this, new TabReorderedEventArgs(item, oldIndex, newIndex));
        }
        finally
        {
            _collectionMutationDepth--;
        }
    }
}
