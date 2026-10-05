namespace PanguEngine.Collections;

/// <summary>
/// Provides read-only access to a set and notifications of its changes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>Enumeration and change snapshots have no guaranteed order.</remarks>
public interface IReadOnlyObservableSet<T> : IReadOnlySet<T>
{
    /// <summary>
    /// Gets the comparer used for membership and set relations.
    /// </summary>
    IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Occurs after membership mutation completes and changes the set.
    /// </summary>
    /// <remarks>
    /// Operations that do not change membership do not raise this event.
    /// Failures during membership mutation or snapshot preparation may retain completed changes without raising this event.
    /// </remarks>
    event EventHandler<SetChangedEventArgs<T>>? Changed;
}
