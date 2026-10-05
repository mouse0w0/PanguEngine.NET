using System.Collections;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class ReadOnlyObservableSetTests
{
    [Fact]
    public void InterfaceSourcePreservesMatchingRulesAndLiveMembers()
    {
        var source = new WrappedSet(["Primary", "wide"]);
        var view = new ReadOnlyObservableSet<string>(source);

        Assert.Same(StringComparer.OrdinalIgnoreCase, view.Comparer);
        Assert.Equal(2, view.Count);
        Assert.True(view.Contains("PRIMARY"));
        Assert.True(view.IsSubsetOf(["primary", "wide", "extra"]));
        Assert.True(view.IsSupersetOf(["wide"]));
        Assert.True(view.IsProperSubsetOf(["primary", "wide", "extra"]));
        Assert.True(view.IsProperSupersetOf(["wide"]));
        Assert.True(view.Overlaps(["WIDE"]));
        Assert.True(view.SetEquals(["primary", "wide"]));
        Assert.False(view.Contains("other"));
        Assert.False(view.Overlaps(["other"]));
        Assert.False((object)view is ISet<string>);
        Assert.False((object)view is ICollection<string>);
        Assert.True(new HashSet<string>(((IEnumerable)view).Cast<string>()).SetEquals(["Primary", "wide"]));

        source.Remove("PRIMARY");
        source.Add("extra");

        Assert.Equal(2, view.Count);
        Assert.True(view.SetEquals(["WIDE", "EXTRA"]));
        Assert.Equal(0, source.SubscriberCount);
    }

    [Fact]
    public void AsReadOnlyReturnsOneLiveView()
    {
        var set = new ObservableSet<int>([1]);
        var view = set.AsReadOnly();

        Assert.Same(view, set.AsReadOnly());
        set.Add(2);
        Assert.True(view.SetEquals([1, 2]));
        set.Clear();
        Assert.Empty(view);
    }

    [Fact]
    public void ViewAllowsNullElementsFromANullableSource()
    {
        var source = new ObservableSet<string?>([null]);
        var view = source.AsReadOnly();

        Assert.True(view.Contains(null));
        Assert.Null(Assert.Single(view));
        source.Remove(null);
        Assert.Empty(view);
    }

    [Fact]
    public void NullSourceIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ReadOnlyObservableSet<string>(null!));
    }

    [Fact]
    public void InterfaceSourceForwardsCommittedChangesWithViewSender()
    {
        var source = new WrappedSet(["Primary"]);
        var view = new ReadOnlyObservableSet<string>(source);
        var changes = new List<SetChangedEventArgs<string>>();
        view.Changed += (sender, change) =>
        {
            Assert.Same(view, sender);
            Assert.Same(source.LastChange, change);
            Assert.True(view.SetEquals(["wide", "extra"]));
            changes.Add(change);
        };

        source.SymmetricExceptWith(["PRIMARY", "wide", "extra"]);

        var change = Assert.Single(changes);
        Assert.True(new HashSet<string>(change.AddedItems).SetEquals(["wide", "extra"]));
        Assert.Equal("Primary", Assert.Single(change.RemovedItems));
    }

    [Fact]
    public void InterfaceSourceIsSubscribedOnlyWhileTheViewHasListeners()
    {
        var source = new WrappedSet([]);
        var view = new ReadOnlyObservableSet<string>(source);
        var firstCalls = 0;
        var secondCalls = 0;
        EventHandler<SetChangedEventArgs<string>> first = (_, _) => firstCalls++;
        EventHandler<SetChangedEventArgs<string>> second = (_, _) => secondCalls++;

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

    [Fact]
    public void EmptyOrUnmatchedSubscriptionsDoNotChangeSourceSubscription()
    {
        var source = new WrappedSet([]);
        var view = new ReadOnlyObservableSet<string>(source);
        EventHandler<SetChangedEventArgs<string>> listener = (_, _) => { };
        EventHandler<SetChangedEventArgs<string>> unknown = (_, _) => { };

        view.Changed += null;
        view.Changed -= null;
        view.Changed -= unknown;
        Assert.Equal(0, source.SubscriberCount);
        view.Changed += listener;
        view.Changed -= unknown;
        Assert.Equal(1, source.SubscriberCount);
        view.Changed -= listener;
        view.Changed -= listener;
        Assert.Equal(0, source.SubscriberCount);
    }

    [Fact]
    public void ViewCallbacksCannotModifyTheSourceSet()
    {
        var source = new ObservableSet<int>();
        var view = source.AsReadOnly();
        var calls = 0;
        view.Changed += (_, _) =>
        {
            calls++;
            Assert.True(view.Contains(1));
            Assert.Throws<InvalidOperationException>(() => source.Add(1));
            Assert.Throws<InvalidOperationException>(source.Clear);
        };

        source.Add(1);

        Assert.Equal(1, calls);
        Assert.True(view.SetEquals([1]));
    }

    [Fact]
    public void ViewListenerFailureStopsSourceNotificationWithoutUndoingTheChange()
    {
        var expected = new InvalidOperationException("view listener failed");
        var source = new ObservableSet<int>();
        var view = source.AsReadOnly();
        var laterViewCalls = 0;
        var laterSourceCalls = 0;
        EventHandler<SetChangedEventArgs<int>> failing = (_, _) => throw expected;
        view.Changed += failing;
        view.Changed += (_, _) => laterViewCalls++;
        source.Changed += (_, _) => laterSourceCalls++;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => source.Add(1)));

        Assert.True(source.SetEquals([1]));
        Assert.True(view.SetEquals([1]));
        Assert.Equal(0, laterViewCalls);
        Assert.Equal(0, laterSourceCalls);
        view.Changed -= failing;
        source.Add(2);
        Assert.Equal(1, laterViewCalls);
        Assert.Equal(1, laterSourceCalls);
    }

    [Fact]
    public void ViewSubscriptionChangesDuringForwardingAffectSubsequentEvents()
    {
        var source = new ObservableSet<int>();
        var view = source.AsReadOnly();
        var removedCalls = 0;
        var addedCalls = 0;
        EventHandler<SetChangedEventArgs<int>> removed = (_, _) => removedCalls++;
        EventHandler<SetChangedEventArgs<int>> added = (_, _) => addedCalls++;
        EventHandler<SetChangedEventArgs<int>> first = (_, _) =>
        {
            view.Changed -= removed;
            view.Changed += added;
        };
        view.Changed += first;
        view.Changed += removed;

        source.Add(1);
        Assert.Equal(1, removedCalls);
        Assert.Equal(0, addedCalls);
        view.Changed -= first;
        source.Add(2);
        Assert.Equal(1, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    private sealed class WrappedSet : IObservableSet<string>
    {
        private readonly ObservableSet<string> _items;
        private EventHandler<SetChangedEventArgs<string>>? _changed;

        internal WrappedSet(IEnumerable<string> items)
        {
            _items = new ObservableSet<string>(items, StringComparer.OrdinalIgnoreCase);
            _items.Changed += (_, change) =>
            {
                LastChange = change;
                _changed?.Invoke(this, change);
            };
        }

        internal int SubscriberCount { get; private set; }
        internal SetChangedEventArgs<string>? LastChange { get; private set; }

        public event EventHandler<SetChangedEventArgs<string>>? Changed
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
        public IEqualityComparer<string> Comparer => _items.Comparer;
        public bool Add(string item) => _items.Add(item);
        void ICollection<string>.Add(string item) => Add(item);
        public bool Remove(string item) => _items.Remove(item);
        public void Clear() => _items.Clear();
        public void UnionWith(IEnumerable<string> other) => _items.UnionWith(other);
        public void IntersectWith(IEnumerable<string> other) => _items.IntersectWith(other);
        public void ExceptWith(IEnumerable<string> other) => _items.ExceptWith(other);
        public void SymmetricExceptWith(IEnumerable<string> other) => _items.SymmetricExceptWith(other);
        public bool Contains(string item) => _items.Contains(item);
        public bool IsSubsetOf(IEnumerable<string> other) => _items.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<string> other) => _items.IsSupersetOf(other);
        public bool IsProperSubsetOf(IEnumerable<string> other) => _items.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<string> other) => _items.IsProperSupersetOf(other);
        public bool Overlaps(IEnumerable<string> other) => _items.Overlaps(other);
        public bool SetEquals(IEnumerable<string> other) => _items.SetEquals(other);
        public void CopyTo(string[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
