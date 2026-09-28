namespace PanguEngine.Collections;

/// <summary>
/// Provides read-only access to a list and notifications of its changes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IReadOnlyObservableList<T> : IReadOnlyList<T>
{
    /// <summary>
    /// Occurs after the list changes.
    /// </summary>
    event EventHandler<ListChangedEventArgs<T>>? Changed;
}
