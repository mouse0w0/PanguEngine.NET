using System.Collections;

namespace PanguEngine.Collections;

/// <summary>
/// Represents an observable mutable set with an explicit equality comparer.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Enumeration has no guaranteed order. Changes are reported synchronously on the calling thread
/// after membership mutation completes. Nested mutations during preparation, mutation, or notification throw
/// <see cref="InvalidOperationException"/>, including unchanged requests.
/// Listener exceptions stop notification and propagate without undoing committed changes.
/// Mutations update the backing set in place. Failures during membership mutation or snapshot preparation
/// retain completed changes without publishing a change event. Bulk inputs are read before membership is modified.
/// This collection is not thread-safe.
/// </remarks>
public class ObservableSet<T> : IObservableSet<T>
{
    private readonly HashSet<T> _items;
    private ReadOnlyObservableSet<T>? _readOnlyView;
    private bool _isMutating;

    /// <summary>Creates an empty set using the default equality comparer.</summary>
    public ObservableSet() : this(null)
    {
    }

    /// <summary>Creates an empty set using the specified equality comparer.</summary>
    /// <param name="comparer">The equality comparer, or null for the default comparer.</param>
    public ObservableSet(IEqualityComparer<T>? comparer)
    {
        _items = new HashSet<T>(comparer);
    }

    /// <summary>Creates a set containing the distinct initial elements using the specified comparer.</summary>
    /// <param name="items">The initial elements.</param>
    /// <param name="comparer">The equality comparer, or null for the default comparer.</param>
    /// <exception cref="ArgumentNullException">The initial sequence is null.</exception>
    public ObservableSet(IEnumerable<T> items, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new HashSet<T>(items, comparer);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The sender is this set. Subscribers are selected when the event is published.
    /// Subscription changes during an event affect subsequent events.
    /// </remarks>
    public event EventHandler<SetChangedEventArgs<T>>? Changed;

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public IEqualityComparer<T> Comparer => _items.Comparer;

    /// <summary>Gets a value indicating whether this set is read-only.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds an element when no equivalent element already exists.</summary>
    /// <param name="item">The element to add.</param>
    /// <returns>Whether the element was added.</returns>
    /// <remarks>Adding an equivalent element preserves the stored element and produces no event.</remarks>
    public virtual bool Add(T item)
    {
        using var mutation = BeginMutation();
        if (!_items.Add(item))
            return false;

        NotifyChange([item], []);
        return true;
    }

    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>Removes an element equivalent to the specified value.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>Whether an element was removed.</returns>
    /// <remarks>The change reports the stored element. An absent element produces no event.</remarks>
    public virtual bool Remove(T item)
    {
        using var mutation = BeginMutation();
        if (!_items.TryGetValue(item, out var stored))
            return false;

        _items.Remove(stored);
        NotifyChange([], [stored]);
        return true;
    }

    /// <summary>Removes all elements, reporting one change unless the set is already empty.</summary>
    public virtual void Clear()
    {
        using var mutation = BeginMutation();
        if (_items.Count == 0)
            return;

        var removed = _items.ToArray();
        _items.Clear();
        NotifyChange([], removed);
    }

    /// <summary>Adds the distinct elements of another collection with a single change notification.</summary>
    /// <param name="other">The elements to include.</param>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    public virtual void UnionWith(IEnumerable<T> other)
    {
        using var mutation = BeginMutation();
        var added = new List<T>();
        foreach (var item in ReadItems(other))
            if (_items.Add(item))
                added.Add(item);

        NotifyChange(added.ToArray(), []);
    }

    /// <summary>Keeps only elements also present in another collection with a single change notification.</summary>
    /// <param name="other">The elements to retain.</param>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    public virtual void IntersectWith(IEnumerable<T> other)
    {
        using var mutation = BeginMutation();
        ArgumentNullException.ThrowIfNull(other);
        var retained = new HashSet<T>(other, Comparer);
        var removed = new List<T>();
        foreach (var item in _items)
            if (!retained.Contains(item))
                removed.Add(item);

        foreach (var item in removed)
            _items.Remove(item);

        NotifyChange([], removed.ToArray());
    }

    /// <summary>Removes elements present in another collection with a single change notification.</summary>
    /// <param name="other">The elements to exclude.</param>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    public virtual void ExceptWith(IEnumerable<T> other)
    {
        using var mutation = BeginMutation();
        var removed = new List<T>();
        foreach (var item in ReadItems(other))
        {
            if (!_items.TryGetValue(item, out var stored))
                continue;

            _items.Remove(stored);
            removed.Add(stored);
        }

        NotifyChange([], removed.ToArray());
    }

    /// <summary>Keeps elements present in exactly one collection with a single change notification.</summary>
    /// <param name="other">The elements to compare.</param>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    public virtual void SymmetricExceptWith(IEnumerable<T> other)
    {
        using var mutation = BeginMutation();
        ArgumentNullException.ThrowIfNull(other);
        var toggled = new HashSet<T>(other, Comparer);
        var added = new List<T>();
        var removed = new List<T>();
        foreach (var item in toggled)
        {
            if (_items.TryGetValue(item, out var stored))
            {
                _items.Remove(stored);
                removed.Add(stored);
            }
            else
            {
                _items.Add(item);
                added.Add(item);
            }
        }

        NotifyChange(added.ToArray(), removed.ToArray());
    }

    /// <inheritdoc />
    public bool Contains(T item) => _items.Contains(item);

    /// <inheritdoc />
    public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);

    /// <inheritdoc />
    public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);

    /// <inheritdoc />
    public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);

    /// <inheritdoc />
    public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);

    /// <inheritdoc />
    public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);

    /// <inheritdoc />
    public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);

    /// <summary>Copies the elements to an array without guaranteeing their order.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting destination index.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator over the current elements without guaranteeing their order.</summary>
    /// <returns>An element enumerator.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns a live read-only view of this set that reports its changes.</summary>
    /// <returns>The same read-only view on every call for this set.</returns>
    public ReadOnlyObservableSet<T> AsReadOnly() =>
        _readOnlyView ??= new ReadOnlyObservableSet<T>(this);

    private void NotifyChange(T[] added, T[] removed)
    {
        if (added.Length == 0 && removed.Length == 0)
            return;

        var change = new SetChangedEventArgs<T>(added, removed);
        Changed?.Invoke(this, change);
    }

    private static T[] ReadItems(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.ToArray();
    }

    private MutationScope BeginMutation()
    {
        if (_isMutating)
            throw new InvalidOperationException(
                "The set cannot be modified during change notification or preparation.");
        _isMutating = true;
        return new MutationScope(this);
    }

    private readonly struct MutationScope(ObservableSet<T> owner) : IDisposable
    {
        public void Dispose() => owner._isMutating = false;
    }
}
