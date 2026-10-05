using System.Collections.ObjectModel;

namespace PanguEngine.Collections;

/// <summary>
/// Describes the actual additions and removals of one completed set operation.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>The snapshots have no guaranteed order and do not make their elements immutable.</remarks>
public sealed class SetChangedEventArgs<T> : EventArgs
{
    /// <summary>
    /// Creates a change description by taking ownership of private snapshot arrays.
    /// </summary>
    /// <param name="addedItems">The added element snapshot.</param>
    /// <param name="removedItems">The removed element snapshot.</param>
    /// <remarks>The caller must not supply externally owned arrays or modify them after this call.</remarks>
    internal SetChangedEventArgs(T[] addedItems, T[] removedItems)
    {
        AddedItems = addedItems.Length == 0
            ? ReadOnlyCollection<T>.Empty : new ReadOnlyCollection<T>(addedItems);
        RemovedItems = removedItems.Length == 0
            ? ReadOnlyCollection<T>.Empty : new ReadOnlyCollection<T>(removedItems);
    }

    /// <summary>Gets the elements that were added to the set.</summary>
    public IReadOnlyList<T> AddedItems { get; }

    /// <summary>Gets the previously stored elements that were removed from the set.</summary>
    public IReadOnlyList<T> RemovedItems { get; }
}
