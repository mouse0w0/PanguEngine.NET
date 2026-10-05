namespace PanguEngine.Collections;

/// <summary>
/// Provides mutable access to a set and notifications of its changes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Changes are reported synchronously on the calling thread after membership mutation completes.
/// Each completed membership mutation reports its actual additions and removals in a single event, and unchanged
/// membership produces no event. Nested mutations during preparation, mutation, or notification are rejected,
/// including requests that would leave membership unchanged.
/// Listener exceptions stop notification and propagate without undoing committed changes.
/// Failures during membership mutation or snapshot preparation retain completed changes without publishing a change event.
/// Enumeration has no guaranteed order. This contract does not provide thread safety.
/// </remarks>
public interface IObservableSet<T> : ISet<T>, IReadOnlyObservableSet<T>
{
    /// <summary>Gets the number of elements.</summary>
    new int Count { get; }

    /// <summary>Determines whether an equivalent element exists in the set.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>Whether a matching element exists.</returns>
    new bool Contains(T item);

    /// <summary>Determines whether every element of this set belongs to another collection.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether this set is a subset of the collection.</returns>
    new bool IsSubsetOf(IEnumerable<T> other);

    /// <summary>Determines whether this set contains every element of another collection.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether this set is a superset of the collection.</returns>
    new bool IsSupersetOf(IEnumerable<T> other);

    /// <summary>Determines whether this set is a strict subset of another collection.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether this set is a proper subset of the collection.</returns>
    new bool IsProperSubsetOf(IEnumerable<T> other);

    /// <summary>Determines whether this set is a strict superset of another collection.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether this set is a proper superset of the collection.</returns>
    new bool IsProperSupersetOf(IEnumerable<T> other);

    /// <summary>Determines whether this set shares any elements with another collection.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether the collections overlap.</returns>
    new bool Overlaps(IEnumerable<T> other);

    /// <summary>Determines whether another collection contains the same set of elements.</summary>
    /// <param name="other">The collection to compare.</param>
    /// <returns>Whether the collections have equal membership.</returns>
    new bool SetEquals(IEnumerable<T> other);
}
