using System.Collections;
using PanguEngine.Collections;

namespace PanguEngine.UI.Selection;

internal sealed class SelectionResultView<T> : IReadOnlyObservableList<T>
{
    internal T[] Items { get; private set; } = [];

    public int Count => Items.Length;
    public T this[int index] => Items[index];
    public event EventHandler<ListChangedEventArgs<T>>? Changed;

    internal void Commit(T[] items) => Items = items;

    internal void Publish(T[] previous) => Changed?.Invoke(this,
        new ListChangedEventArgs<T>(ListChangeKind.Replace, 0, previous, Items));

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
