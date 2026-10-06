using PanguEngine.Collections;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides validated observable mutable access to the tab items of a <see cref="TabView"/>.
/// </summary>
/// <remarks>
/// The owning view completes the corresponding tree and host update before the inherited collection
/// publishes its change. Selection observes the same notification stream through the view's read-only
/// source, so item identity and selection indices remain synchronized.
/// </remarks>
public sealed class TabItemCollection : ObservableList<TabItem>
{
    private readonly TabView _owner;

    internal TabItemCollection(TabView owner) => _owner = owner;

    /// <summary>Gets or replaces the tab item at an index.</summary>
    /// <param name="index">The zero-based item index.</param>
    public new TabItem this[int index]
    {
        get => base[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(base[index], value))
                return;
            base[index] = value;
        }
    }

    protected override int IndexOfItem(TabItem? item)
    {
        for (var index = 0; index < Count; index++)
        {
            if (ReferenceEquals(this[index], item))
                return index;
        }

        return -1;
    }

    protected override void SetItem(int index, TabItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _owner.ReplaceItemFromCollection(index, value, () => base.SetItem(index, value));
        _owner.BeginCollectionNotification();
    }

    protected override void InsertItem(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertItemFromCollection(index, [item], () => base.InsertItem(index, item));
        _owner.BeginCollectionNotification();
    }

    protected override void RemoveItem(int index)
    {
        _owner.RemoveItemFromCollection(index, 1, () => base.RemoveItem(index));
        _owner.BeginCollectionNotification();
    }

    protected override void ClearItems()
    {
        _owner.RemoveItemFromCollection(0, Count, () => base.ClearItems());
        _owner.BeginCollectionNotification();
    }

    protected override void InsertItems(int index, IReadOnlyList<TabItem> items)
    {
        _owner.InsertItemFromCollection(index, items, () => base.InsertItems(index, items));
        _owner.BeginCollectionNotification();
    }

    protected override void RemoveItems(int index, int count)
    {
        _owner.RemoveItemFromCollection(index, count, () => base.RemoveItems(index, count));
        _owner.BeginCollectionNotification();
    }

    protected override void ReplaceItems(IReadOnlyList<TabItem> items)
    {
        _owner.ReplaceItemsFromCollection(items, () => base.ReplaceItems(items));
        _owner.BeginCollectionNotification();
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        _owner.MoveItemFromCollection(oldIndex, newIndex, () => base.MoveItem(oldIndex, newIndex));
        _owner.BeginCollectionNotification();
    }

    protected override void ReorderItems(IReadOnlyList<int> permutation)
    {
        _owner.ReorderItemsFromCollection(permutation, () => base.ReorderItems(permutation));
        _owner.BeginCollectionNotification();
    }
}
