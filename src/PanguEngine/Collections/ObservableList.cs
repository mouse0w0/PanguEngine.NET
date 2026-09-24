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
/// </remarks>
public sealed class ObservableList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> _items;
    private bool _mutationActive;

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
            var handler = Changed;
            var change = handler is null ? null :
                new ListChangedEventArgs<T>(ListChangeKind.Replace, index, [oldItem], [value]);
            _items[index] = value;
            handler?.Invoke(this, change!);
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
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(ListChangeKind.Add, index, newItems: [item]);
        _items.Insert(index, item);
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Adds a sequence of items to the end of the list.
    /// </summary>
    /// <param name="items">The items to append.</param>
    public void AddRange(IEnumerable<T> items) => InsertRange(Count, items);

    /// <summary>
    /// Inserts a sequence of items at an index.
    /// </summary>
    /// <param name="index">The insertion index, from zero through Count.</param>
    /// <param name="items">The items to insert.</param>
    public void InsertRange(int index, IEnumerable<T> items)
    {
        using var mutation = BeginMutation();
        ValidateInsertIndex(index);
        var additions = CopyItems(items);
        if (additions.Length == 0)
            return;
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(ListChangeKind.Add, index, newItems: additions);
        _items.InsertRange(index, additions);
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Replaces all items with a sequence.
    /// </summary>
    /// <param name="items">The replacement items.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        using var mutation = BeginMutation();
        var replacements = CopyItems(items);
        if (_items.Count == 0 && replacements.Length == 0)
            return;
        var kind = _items.Count == 0 ? ListChangeKind.Add :
            replacements.Length == 0 ? ListChangeKind.Remove : ListChangeKind.Replace;
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(kind, 0, _items.ToArray(), replacements);
        _items.Clear();
        _items.AddRange(replacements);
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Removes a contiguous range of items.
    /// </summary>
    /// <param name="index">The starting index, from zero through Count.</param>
    /// <param name="count">The number of items to remove.</param>
    public void RemoveRange(int index, int count)
    {
        using var mutation = BeginMutation();
        ValidateRange(index, count);
        if (count == 0)
            return;
        var handler = Changed;
        ListChangedEventArgs<T>? change = null;
        if (handler is not null)
        {
            var removed = new T[count];
            _items.CopyTo(index, removed, 0, count);
            change = new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: removed);
        }
        _items.RemoveRange(index, count);
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Moves an item to its final index.
    /// </summary>
    /// <param name="oldIndex">The current item index.</param>
    /// <param name="newIndex">The final item index.</param>
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
        var handler = Changed;
        ListChangedEventArgs<T>? change = null;
        if (handler is not null)
        {
            var permutation = new int[_items.Count];
            for (var oldIndex = 0; oldIndex < permutation.Length; oldIndex++)
                permutation[oldIndex] = permutation.Length - oldIndex - 1;
            change = new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation);
        }
        _items.Reverse();
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Removes the first occurrence of an item.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>Whether a matching item was removed.</returns>
    public bool Remove(T item)
    {
        using var mutation = BeginMutation();
        var index = _items.IndexOf(item);
        if (index < 0)
            return false;
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: [_items[index]]);
        _items.RemoveAt(index);
        handler?.Invoke(this, change!);
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
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(ListChangeKind.Remove, index, oldItems: [_items[index]]);
        _items.RemoveAt(index);
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Removes all items.
    /// </summary>
    public void Clear()
    {
        using var mutation = BeginMutation();
        if (_items.Count == 0)
            return;
        var handler = Changed;
        var change = handler is null ? null :
            new ListChangedEventArgs<T>(ListChangeKind.Remove, 0, oldItems: _items.ToArray());
        _items.Clear();
        handler?.Invoke(this, change!);
    }

    /// <summary>
    /// Determines whether an item exists in the list.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>Whether a matching item exists.</returns>
    public bool Contains(T item) => _items.Contains(item);

    /// <summary>
    /// Finds an item index.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>The first matching index, or -1 when absent.</returns>
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <summary>
    /// Copies items into an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting destination index.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator over the list.
    /// </summary>
    /// <returns>An enumerator in list order.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private MutationScope BeginMutation()
    {
        if (_mutationActive)
            throw new InvalidOperationException("The list cannot be modified during change notification or preparation.");
        _mutationActive = true;
        return new MutationScope(this);
    }

    private readonly struct MutationScope(ObservableList<T> owner) : IDisposable
    {
        public void Dispose() => owner._mutationActive = false;
    }

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
        var handler = Changed;
        ListChangedEventArgs<T>? change = null;
        if (handler is not null)
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
            change = new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation);
        }

        var item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
        handler?.Invoke(this, change!);
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

        var handler = Changed;
        ListChangedEventArgs<T>? change = null;
        if (handler is not null)
        {
            var permutation = new int[entries.Length];
            for (var newIndex = 0; newIndex < entries.Length; newIndex++)
                permutation[entries[newIndex].OriginalIndex] = newIndex;
            change = new ListChangedEventArgs<T>(ListChangeKind.Reorder, 0, permutation: permutation);
        }
        for (var newIndex = 0; newIndex < entries.Length; newIndex++)
            _items[newIndex] = entries[newIndex].Item;
        handler?.Invoke(this, change!);
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

        var middle = start + ((end - start) / 2);
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
