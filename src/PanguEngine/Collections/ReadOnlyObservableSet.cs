using System.Collections;

namespace PanguEngine.Collections;

/// <summary>
/// Provides a live read-only view of an observable set.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Source changes are reflected immediately. The view does not make its elements immutable.
/// Enumeration has no guaranteed order. Notifications occur synchronously on the calling thread.
/// Listener exceptions propagate without undoing committed changes, and nested source mutations
/// during notification are rejected. This collection is not thread-safe.
/// </remarks>
public sealed class ReadOnlyObservableSet<T> : IReadOnlyObservableSet<T>
{
    private readonly IObservableSet<T> _source;
    private EventHandler<SetChangedEventArgs<T>>? _changed;

    /// <summary>Creates an independent read-only view of an observable mutable set.</summary>
    /// <param name="source">The set to observe.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public ReadOnlyObservableSet(IObservableSet<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Occurs when the source set publishes a completed membership change.</summary>
    /// <remarks>
    /// The sender is this view, and the event arguments are those reported by the source.
    /// Subscription changes during a notification from this view affect subsequent notifications.
    /// Source mutations that fail before publication may change membership without raising this event.
    /// </remarks>
    public event EventHandler<SetChangedEventArgs<T>>? Changed
    {
        add
        {
            var wasEmpty = _changed is null;
            _changed += value;
            if (wasEmpty && _changed is not null)
                _source.Changed += OnSourceChanged;
        }
        remove
        {
            var hadListeners = _changed is not null;
            _changed -= value;
            if (hadListeners && _changed is null)
                _source.Changed -= OnSourceChanged;
        }
    }

    /// <inheritdoc />
    public int Count => _source.Count;

    /// <inheritdoc />
    public IEqualityComparer<T> Comparer => _source.Comparer;

    /// <inheritdoc />
    public bool Contains(T item) => _source.Contains(item);

    /// <inheritdoc />
    public bool IsSubsetOf(IEnumerable<T> other) => _source.IsSubsetOf(other);

    /// <inheritdoc />
    public bool IsSupersetOf(IEnumerable<T> other) => _source.IsSupersetOf(other);

    /// <inheritdoc />
    public bool IsProperSubsetOf(IEnumerable<T> other) => _source.IsProperSubsetOf(other);

    /// <inheritdoc />
    public bool IsProperSupersetOf(IEnumerable<T> other) => _source.IsProperSupersetOf(other);

    /// <inheritdoc />
    public bool Overlaps(IEnumerable<T> other) => _source.Overlaps(other);

    /// <inheritdoc />
    public bool SetEquals(IEnumerable<T> other) => _source.SetEquals(other);

    /// <summary>Returns an enumerator over the source elements without guaranteeing their order.</summary>
    /// <returns>An element enumerator.</returns>
    public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void OnSourceChanged(object? sender, SetChangedEventArgs<T> change) =>
        _changed?.Invoke(this, change);
}
