using System.Collections;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class ReadOnlyObservableListTests
{
    [Fact]
    public void InterfaceSourcePreservesMatchingRulesAndLiveContents()
    {
        var source = new WrappedList(["first", null, "third"]);
        var view = new ReadOnlyObservableList<string?>(source);
        Assert.Equal(3, view.Count);
        Assert.Null(view[1]);
        Assert.True(view.Contains("FIRST"));
        Assert.Equal(0, view.IndexOf("FIRST"));
        Assert.True(view.Contains(null));
        Assert.Equal(1, view.IndexOf(null));
        Assert.False(view.Contains("missing"));
        Assert.Equal(-1, view.IndexOf("missing"));
        var destination = new string?[5];
        view.CopyTo(destination, 1);
        Assert.Equal(new string?[] { null, "first", null, "third", null }, destination);
        Assert.Equal(new string?[] { "first", null, "third" }, view);
        Assert.Equal(new string?[] { "first", null, "third" }, ((IEnumerable)view).Cast<string?>());
        Assert.True(source.Remove("FIRST"));
        source[1] = "last";
        Assert.Equal(2, view.Count);
        Assert.Equal("last", view[1]);
        Assert.Equal(new string?[] { null, "last" }, view);
    }

    [Fact]
    public void InterfaceSourceForwardsCommittedChangesWithViewSender()
    {
        var source = new WrappedList(["first"]);
        var view = new ReadOnlyObservableList<string?>(source);
        var changes = new List<ListChangedEventArgs<string?>>();
        string?[] expected = ["first", "second", "third"];
        view.Changed += (sender, change) =>
        {
            Assert.Same(view, sender);
            Assert.Same(source.LastChange, change);
            Assert.Equal(expected, view);
            changes.Add(change);
        };
        source.AddRange(["second", "third"]);
        var addition = Assert.Single(changes);
        Assert.Equal(ListChangeKind.Add, addition.Kind);
        Assert.Equal(1, addition.Index);
        Assert.Equal(new string?[] { "second", "third" }, addition.NewItems);
        Assert.Empty(addition.OldItems);
        Assert.Empty(addition.Permutation);
        expected = ["second", "third", "first"];
        source.Move(0, 2);
        Assert.Equal(2, changes.Count);
        Assert.Equal(ListChangeKind.Reorder, changes[1].Kind);
        Assert.Equal([2, 0, 1], changes[1].Permutation);
    }

    [Fact]
    public void InterfaceSourceIsSubscribedOnlyWhileViewHasListeners()
    {
        var source = new WrappedList([]);
        var view = new ReadOnlyObservableList<string?>(source);
        var firstCalls = 0;
        var secondCalls = 0;
        EventHandler<ListChangedEventArgs<string?>> first = (_, _) => firstCalls++;
        EventHandler<ListChangedEventArgs<string?>> second = (_, _) => secondCalls++;
        Assert.Equal(0, source.SubscriberCount);
        view.Changed += first;
        view.Changed += second;
        Assert.Equal(1, source.SubscriberCount);
        source.Add("first");
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
        view.Changed -= first;
        Assert.Equal(1, source.SubscriberCount);
        source.Add("second");
        Assert.Equal(1, firstCalls);
        Assert.Equal(2, secondCalls);
        view.Changed -= second;
        Assert.Equal(0, source.SubscriberCount);
        source.Clear();
        Assert.Empty(view);
        Assert.Equal(1, firstCalls);
        Assert.Equal(2, secondCalls);
        view.Changed += first;
        Assert.Equal(1, source.SubscriberCount);
        source.Add("third");
        Assert.Equal(2, firstCalls);
        view.Changed -= first;
        Assert.Equal(0, source.SubscriberCount);
    }

    private sealed class WrappedList : IObservableList<string?>
    {
        private readonly ObservableList<string?> _items;
        private EventHandler<ListChangedEventArgs<string?>>? _changed;

        internal WrappedList(IEnumerable<string?> items)
        {
            _items = new ObservableList<string?>(items);
            _items.Changed += (_, change) =>
            {
                LastChange = change;
                _changed?.Invoke(this, change);
            };
        }

        internal int SubscriberCount { get; private set; }
        internal ListChangedEventArgs<string?>? LastChange { get; private set; }

        public event EventHandler<ListChangedEventArgs<string?>>? Changed
        {
            add
            {
                _changed += value;
                SubscriberCount++;
            }
            remove
            {
                _changed -= value;
                SubscriberCount--;
            }
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public string? this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public bool Contains(string? item) => IndexOf(item) >= 0;

        public int IndexOf(string? item)
        {
            for (var index = 0; index < Count; index++)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(this[index], item))
                    return index;
            }

            return -1;
        }

        public bool Remove(string? item)
        {
            var index = IndexOf(item);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        public void Add(string? item) => _items.Add(item);
        public void Insert(int index, string? item) => _items.Insert(index, item);
        public void RemoveAt(int index) => _items.RemoveAt(index);
        public void Clear() => _items.Clear();
        public void CopyTo(string?[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public void AddRange(IEnumerable<string?> items) => _items.AddRange(items);
        public void InsertRange(int index, IEnumerable<string?> items) => _items.InsertRange(index, items);
        public void RemoveRange(int index, int count) => _items.RemoveRange(index, count);
        public void ReplaceAll(IEnumerable<string?> items) => _items.ReplaceAll(items);
        public void Move(int oldIndex, int newIndex) => _items.Move(oldIndex, newIndex);
        public IEnumerator<string?> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}