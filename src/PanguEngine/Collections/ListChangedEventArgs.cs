using System.Collections.ObjectModel;

namespace PanguEngine.Collections;

/// <summary>
/// Describes one completed change to an observable list.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ListChangedEventArgs<T> : EventArgs
{
    /// <summary>
    /// Creates a change description by taking ownership of private snapshot arrays.
    /// </summary>
    /// <remarks>
    /// The caller must not supply externally owned arrays or modify the arrays after this call.
    /// </remarks>
    internal ListChangedEventArgs(
        ListChangeKind kind,
        int index,
        T[]? oldItems = null,
        T[]? newItems = null,
        int[]? permutation = null)
    {
        Kind = kind;
        Index = index;
        OldItems = oldItems is { Length: > 0 } ? new ReadOnlyCollection<T>(oldItems) : ReadOnlyCollection<T>.Empty;
        NewItems = newItems is { Length: > 0 } ? new ReadOnlyCollection<T>(newItems) : ReadOnlyCollection<T>.Empty;
        Permutation = permutation is { Length: > 0 } ? new ReadOnlyCollection<int>(permutation) : ReadOnlyCollection<int>.Empty;
    }

    /// <summary>
    /// Gets the kind of change.
    /// </summary>
    public ListChangeKind Kind { get; }

    /// <summary>
    /// Gets the start index of the affected range, or zero for a reorder.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the removed or replaced item snapshot.
    /// </summary>
    public IReadOnlyList<T> OldItems { get; }

    /// <summary>
    /// Gets the added or replacement item snapshot.
    /// </summary>
    public IReadOnlyList<T> NewItems { get; }

    /// <summary>
    /// Gets the old-index to new-index permutation for a reorder.
    /// </summary>
    public IReadOnlyList<int> Permutation { get; }
}
