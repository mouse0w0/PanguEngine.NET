using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Collections;

/// <summary>
/// Provides a live read-only view of an observable list.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Source changes are reflected immediately. The view does not make its elements immutable.
/// Notifications occur synchronously on the calling thread. Listener exceptions propagate
/// without undoing the completed change, and nested source mutations during notification are rejected.
/// This collection is not thread-safe.
/// </remarks>
public sealed class ReadOnlyObservableList<T> : IReadOnlyObservableList<T>
{
    private readonly ObservableList<T> _source;
    private EventHandler<ListChangedEventArgs<T>>? _changed;

    /// <summary>
    /// Creates an independent read-only view of the specified list.
    /// </summary>
    /// <param name="source">The list to observe.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public ReadOnlyObservableList(ObservableList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>
    /// Occurs after the source list changes.
    /// </summary>
    /// <remarks>
    /// The sender is this view, and the event arguments are those reported by the source.
    /// Subscription changes during a notification from this view affect subsequent notifications.
    /// </remarks>
    public event EventHandler<ListChangedEventArgs<T>>? Changed
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
            _changed -= value;
            if (_changed is null)
                _source.Changed -= OnSourceChanged;
        }
    }

    /// <summary>
    /// Gets the number of items.
    /// </summary>
    public int Count => _source.Count;

    /// <summary>
    /// Gets an item at an index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    public T this[int index] => _source[index];

    /// <summary>
    /// Determines whether an item exists using the source list's matching rules.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>Whether a matching item exists.</returns>
    public bool Contains(T? item) => _source.Contains(item);

    /// <summary>
    /// Finds an item index using the source list's matching rules.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>The first matching index, or -1 when absent.</returns>
    public int IndexOf(T? item) => _source.IndexOf(item);

    /// <summary>
    /// Copies items into an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting destination index.</param>
    public void CopyTo(T[] array, int arrayIndex) => _source.CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator over the source list.
    /// </summary>
    /// <returns>An enumerator in list order.</returns>
    public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void OnSourceChanged(object? sender, ListChangedEventArgs<T> change) =>
        _changed?.Invoke(this, change);
}
