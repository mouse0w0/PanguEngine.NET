using System.Collections;

namespace PanguEngine.Collections;

/// <summary>
/// Represents an observable mutable list with explicit stable sorting and reorder notifications.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Changes are reported synchronously on the calling thread. Nested mutations during a mutation
/// or notification throw <see cref="InvalidOperationException"/>. This collection is not thread-safe.
/// Listener exceptions stop notification and propagate without undoing the completed change.
/// Public mutations enter a mutation scope and call the corresponding protected operation.
/// Overrides must complete their associated state updates before returning and must not publish
/// the operation's event themselves. The public entry point publishes after the override returns.
/// Range operations have separate extension points and do not call the single-item overrides.
/// </remarks>
public class ObservableList<T> : IObservableList<T>
{
    private readonly List<T> _items;
    private ReadOnlyObservableList<T>? _readOnlyView;
    private bool _isMutating;

    /// <summary>
    /// Creates an empty observable list.
    /// </summary>
    public ObservableList()
    {
        _items = [];
    }

    /// <summary>
    /// Creates an observable list containing a copy of the specified items.
    /// </summary>
    /// <param name="items">The initial items.</param>
    public ObservableList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
    }

    /// <summary>
    /// Occurs after the list changes.
    /// </summary>
    /// <remarks>
    /// The sender is this list. Subscription changes during notification affect subsequent events.
    /// </remarks>
    public event EventHandler<ListChangedEventArgs<T>>? Changed;

    /// <summary>
    /// Gets the number of items.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets a value indicating whether the list is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or replaces an item at an index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    public T this[int index]
    {
        get => _items[index];
        set
        {
            using var mutation = BeginMutation();
            var oldItem = _items[index];
            SetItem(index, value);
            PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Replace, index, [oldItem], [value]));
        }
    }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item) => Insert(Count, item);

    /// <summary>
    /// Inserts an item at an index.
    /// </summary>
    /// <param name="index">The insertion index, from zero through Count.</param>
    /// <param name="item">The item to insert.</param>
    public void Insert(int index, T item)
    {
        using var mutation = BeginMutation();
        ValidateInsertIndex(index);
        InsertItem(index, item);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Add, index, newItems: [item]));
    }

    /// <inheritdoc />
    public void AddRange(IEnumerable<T> items) => InsertRange(Count, items);

    /// <inheritdoc />
    public void InsertRange(int index, IEnumerable<T> items)
    {
        using var mutation = BeginMutation();
        ValidateInsertIndex(index);
        var additions = CopyItems(items);
        if (additions.Length == 0)
            return;
        InsertItems(index, additions);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Add, index, newItems: additions));
    }

    /// <inheritdoc />
    public void ReplaceAll(IEnumerable<T> items)
    {
        using var mutation = BeginMutation();
        var replacements = CopyItems(items);
        if (_items.Count == 0 && replacements.Length == 0)
            return;
        var kind = _items.Count == 0 ? ListChangeKind.Add :
            replacements.Length == 0 ? ListChangeKind.Remove : ListChangeKind.Replace;
        var previous = _items.ToArray();
        ReplaceItems(replacements);
        PublishChange(new ListChangedEventArgs<T>(kind, 0, previous, replacements));
    }

    /// <inheritdoc />
    public void RemoveRange(int index, int count)
    {
        using var mutation = BeginMutation();
        ValidateRange(index, count);
        if (count == 0)
            return;
        var removed = _items.GetRange(index, count).ToArray();
        RemoveItems(index, count);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: removed));
    }

    /// <inheritdoc />
    public void Move(int oldIndex, int newIndex)
    {
        using var mutation = BeginMutation();
        ValidateItemIndex(oldIndex, nameof(oldIndex));
        ValidateItemIndex(newIndex, nameof(newIndex));
        if (oldIndex != newIndex)
            MoveCore(oldIndex, newIndex);
    }

    /// <summary>
    /// Stably sorts the list using the default comparer without maintaining automatic ordering.
    /// </summary>
    public void Sort() => Sort((IComparer<T>?)null);

    /// <summary>
    /// Stably sorts the list using a comparer without maintaining automatic ordering.
    /// </summary>
    /// <param name="comparer">The comparer, or null for the default comparer.</param>
    public void Sort(IComparer<T>? comparer)
    {
        using var mutation = BeginMutation();
        SortCore(comparer ?? Comparer<T>.Default);
    }

    /// <summary>
    /// Stably sorts the list using a comparison delegate without maintaining automatic ordering.
    /// </summary>
    /// <param name="comparison">The comparison delegate.</param>
    public void Sort(Comparison<T> comparison)
    {
        using var mutation = BeginMutation();
        ArgumentNullException.ThrowIfNull(comparison);
        SortCore(Comparer<T>.Create(comparison));
    }

    /// <summary>
    /// Reverses the list.
    /// </summary>
    public void Reverse()
    {
        using var mutation = BeginMutation();
        if (_items.Count < 2)
            return;
        var permutation = new int[_items.Count];
        for (var oldIndex = 0; oldIndex < permutation.Length; oldIndex++)
            permutation[oldIndex] = permutation.Length - oldIndex - 1;
        ReorderItems(permutation);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation));
    }

    /// <inheritdoc />
    public bool Remove(T? item)
    {
        using var mutation = BeginMutation();
        var index = IndexOfItem(item);
        if (index < 0)
            return false;
        var removed = _items[index];
        RemoveItem(index);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: [removed]));
        return true;
    }

    /// <summary>
    /// Removes an item at an index.
    /// </summary>
    /// <param name="index">The zero-based item index.</param>
    public void RemoveAt(int index)
    {
        using var mutation = BeginMutation();
        ValidateItemIndex(index);
        var removed = _items[index];
        RemoveItem(index);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: [removed]));
    }

    /// <summary>
    /// Removes all items.
    /// </summary>
    public void Clear()
    {
        using var mutation = BeginMutation();
        if (_items.Count == 0)
            return;
        var removed = _items.ToArray();
        ClearItems();
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Remove, 0, oldItems: removed));
    }

    /// <inheritdoc />
    public bool Contains(T? item) => IndexOfItem(item) >= 0;

    /// <inheritdoc />
    public int IndexOf(T? item) => IndexOfItem(item);

    /// <summary>
    /// Copies items into an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting destination index.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if ((uint)arrayIndex > (uint)array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        _items.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Returns an enumerator over the list.
    /// </summary>
    /// <returns>An enumerator in list order.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns a live read-only view of this list that reports its changes.
    /// </summary>
    /// <returns>The same read-only view on every call for this list.</returns>
    public ReadOnlyObservableList<T> AsReadOnly() => _readOnlyView ??= new ReadOnlyObservableList<T>(this);

    /// <summary>
    /// Enters a mutation, rejecting nested writes until the returned scope is disposed.
    /// </summary>
    /// <returns>The mutation scope.</returns>
    protected IDisposable BeginMutation()
    {
        if (_isMutating)
            throw new InvalidOperationException(
                "The list cannot be modified during change notification or preparation.");
        _isMutating = true;
        return new MutationScope(this);
    }

    private readonly struct MutationScope(ObservableList<T> owner) : IDisposable
    {
        public void Dispose() => owner._isMutating = false;
    }

    /// <summary>
    /// Finds an item using this collection's identity rules.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>The matching index, or -1.</returns>
    protected virtual int IndexOfItem(T? item) => _items.IndexOf(item!);

    /// <summary>
    /// Replaces one item within the active mutation scope.
    /// </summary>
    /// <param name="index">The item index.</param>
    /// <param name="value">The replacement value.</param>
    protected virtual void SetItem(int index, T value) => _items[index] = value;

    /// <summary>
    /// Inserts an item without publishing a notification.
    /// </summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="item">The item to insert.</param>
    protected virtual void InsertItem(int index, T item) => _items.Insert(index, item);

    /// <summary>
    /// Removes an item without publishing a notification.
    /// </summary>
    /// <param name="index">The item index.</param>
    protected virtual void RemoveItem(int index) => _items.RemoveAt(index);

    /// <summary>Clears the items without publishing a notification.</summary>
    protected virtual void ClearItems() => _items.Clear();

    /// <summary>Moves an item without publishing a notification.</summary>
    /// <param name="oldIndex">The current index.</param>
    /// <param name="newIndex">The final index.</param>
    protected virtual void MoveItem(int oldIndex, int newIndex)
    {
        var item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
    }

    /// <summary>Inserts a range without publishing a notification.</summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="items">The prepared item snapshot.</param>
    protected virtual void InsertItems(int index, IReadOnlyList<T> items) => _items.InsertRange(index, items);

    /// <summary>Removes a range without publishing a notification.</summary>
    /// <param name="index">The first item index.</param>
    /// <param name="count">The item count.</param>
    protected virtual void RemoveItems(int index, int count) => _items.RemoveRange(index, count);

    /// <summary>Replaces all items without publishing a notification.</summary>
    /// <param name="items">The prepared replacement snapshot.</param>
    protected virtual void ReplaceItems(IReadOnlyList<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }

    /// <summary>Reorders items without publishing a notification.</summary>
    /// <param name="permutation">The old-index to new-index permutation.</param>
    protected virtual void ReorderItems(IReadOnlyList<int> permutation)
    {
        var previous = _items.ToArray();
        for (var index = 0; index < previous.Length; index++)
            _items[permutation[index]] = previous[index];
    }

    /// <summary>
    /// Publishes a committed change to the current subscribers.
    /// </summary>
    /// <param name="change">The completed change.</param>
    protected void PublishChange(ListChangedEventArgs<T> change) => Changed?.Invoke(this, change);

    private static T[] CopyItems(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.ToArray();
    }

    private void ValidateItemIndex(int index, string parameterName = "index")
    {
        if ((uint)index >= (uint)_items.Count)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private void ValidateInsertIndex(int index)
    {
        if ((uint)index > (uint)_items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void ValidateRange(int index, int count)
    {
        ValidateInsertIndex(index);
        if (count < 0 || count > _items.Count - index)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private void MoveCore(int oldIndex, int newIndex)
    {
        var permutation = CreateIdentityPermutation(_items.Count);
        permutation[oldIndex] = newIndex;
        if (oldIndex < newIndex)
        {
            for (var index = oldIndex + 1; index <= newIndex; index++)
                permutation[index] = index - 1;
        }
        else
        {
            for (var index = newIndex; index < oldIndex; index++)
                permutation[index] = index + 1;
        }

        MoveItem(oldIndex, newIndex);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation));
    }

    private void SortCore(IComparer<T> comparer)
    {
        if (_items.Count < 2)
            return;
        var entries = new SortEntry[_items.Count];
        for (var index = 0; index < entries.Length; index++)
            entries[index] = new SortEntry(_items[index], index);

        var buffer = new SortEntry[entries.Length];
        MergeSort(entries, buffer, 0, entries.Length, comparer);
        var changed = false;
        for (var newIndex = 0; newIndex < entries.Length; newIndex++)
        {
            if (entries[newIndex].OriginalIndex != newIndex)
            {
                changed = true;
                break;
            }
        }

        if (!changed)
            return;

        var permutation = new int[entries.Length];
        for (var newIndex = 0; newIndex < entries.Length; newIndex++)
            permutation[entries[newIndex].OriginalIndex] = newIndex;
        ReorderItems(permutation);
        PublishChange(new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation));
    }

    private static void MergeSort(
        SortEntry[] entries,
        SortEntry[] buffer,
        int start,
        int end,
        IComparer<T> comparer)
    {
        if (end - start < 2)
            return;

        var middle = start + (end - start) / 2;
        MergeSort(entries, buffer, start, middle, comparer);
        MergeSort(entries, buffer, middle, end, comparer);
        var left = start;
        var right = middle;
        var target = start;
        while (left < middle && right < end)
        {
            if (comparer.Compare(entries[left].Item, entries[right].Item) <= 0)
                buffer[target++] = entries[left++];
            else
                buffer[target++] = entries[right++];
        }

        while (left < middle)
            buffer[target++] = entries[left++];
        while (right < end)
            buffer[target++] = entries[right++];
        Array.Copy(buffer, start, entries, start, end - start);
    }

    private static int[] CreateIdentityPermutation(int count)
    {
        var permutation = new int[count];
        for (var index = 0; index < count; index++)
            permutation[index] = index;
        return permutation;
    }

    private readonly record struct SortEntry(T Item, int OriginalIndex);
}