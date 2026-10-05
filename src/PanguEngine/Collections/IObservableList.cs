namespace PanguEngine.Collections;

/// <summary>
/// Provides mutable access to a list and notifications of its changes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Changes are reported synchronously on the calling thread after the list is updated.
/// Nested mutations during a mutation or notification are rejected. Listener exceptions
/// propagate without undoing the completed change. This contract does not provide thread safety.
/// </remarks>
public interface IObservableList<T> : IList<T>, IReadOnlyObservableList<T>
{
    /// <summary>
    /// Gets the number of items.
    /// </summary>
    new int Count { get; }

    /// <summary>
    /// Gets or replaces an item at an index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    new T this[int index] { get; set; }

    /// <summary>
    /// Determines whether an item exists using the list's matching rules.
    /// </summary>
    /// <param name="item">The item to locate. Reference-type and nullable value-type arguments may be null.</param>
    /// <returns>Whether a matching item exists.</returns>
    new bool Contains(T? item);

    /// <summary>
    /// Finds the first item index using the list's matching rules.
    /// </summary>
    /// <param name="item">The item to locate. Reference-type and nullable value-type arguments may be null.</param>
    /// <returns>The first matching index, or -1 when absent.</returns>
    new int IndexOf(T? item);

    /// <summary>
    /// Removes the first matching occurrence of an item.
    /// </summary>
    /// <param name="item">The item to remove. Reference-type and nullable value-type arguments may be null.</param>
    /// <returns>Whether a matching item was removed.</returns>
    new bool Remove(T? item);

    /// <summary>
    /// Adds a sequence of items to the end of the list with a single change notification.
    /// </summary>
    /// <param name="items">The items to append.</param>
    /// <remarks>An empty sequence does not produce a notification.</remarks>
    /// <exception cref="ArgumentNullException">The items sequence is null.</exception>
    /// <exception cref="InvalidOperationException">The list is already being modified or notifying listeners.</exception>
    void AddRange(IEnumerable<T> items);

    /// <summary>
    /// Inserts a sequence of items at an index with a single change notification.
    /// </summary>
    /// <param name="index">The insertion index, from zero through Count.</param>
    /// <param name="items">The items to insert.</param>
    /// <remarks>An empty sequence does not produce a notification.</remarks>
    /// <exception cref="ArgumentNullException">The items sequence is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is less than zero or greater than Count.</exception>
    /// <exception cref="InvalidOperationException">The list is already being modified or notifying listeners.</exception>
    void InsertRange(int index, IEnumerable<T> items);

    /// <summary>
    /// Removes a contiguous range of items with a single change notification.
    /// </summary>
    /// <param name="index">The starting index, from zero through Count.</param>
    /// <param name="count">The number of items to remove.</param>
    /// <remarks>An empty range does not produce a notification.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The index or count is negative, or the range extends beyond the list.
    /// </exception>
    /// <exception cref="InvalidOperationException">The list is already being modified or notifying listeners.</exception>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Replaces all items with a sequence and reports a single change notification.
    /// </summary>
    /// <param name="items">The replacement items.</param>
    /// <remarks>Replacing an empty list with an empty sequence does not produce a notification.</remarks>
    /// <exception cref="ArgumentNullException">The items sequence is null.</exception>
    /// <exception cref="InvalidOperationException">The list is already being modified or notifying listeners.</exception>
    void ReplaceAll(IEnumerable<T> items);

    /// <summary>
    /// Moves an item to its final index and reports a reorder notification.
    /// </summary>
    /// <param name="oldIndex">The current item index.</param>
    /// <param name="newIndex">The final item index.</param>
    /// <remarks>Moving an item to its current index does not produce a notification.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Either index does not identify an item in the list.</exception>
    /// <exception cref="InvalidOperationException">The list is already being modified or notifying listeners.</exception>
    void Move(int oldIndex, int newIndex);
}