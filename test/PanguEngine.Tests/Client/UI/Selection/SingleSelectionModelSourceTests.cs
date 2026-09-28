using System.Collections;
using System.Runtime.CompilerServices;
using PanguEngine.Client.UI.Selection;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Client.UI.Selection;

public sealed class SingleSelectionModelSourceTests
{
    [Fact]
    public void SwitchingSourceClearsSelectionAndPublishesCommittedStateInOrder()
    {
        var first = new TrackingList<string>(["a", "b"]);
        var second = new TrackingList<string>(["b", "a"]);
        var model = new SingleSelectionModel<string>(first) { SelectedIndex = 1 };
        var changes = new List<(string Name, object? Old, object? New)>();
        model.PropertyChanged += (_, args) =>
        {
            Assert.Same(second, model.Source);
            Assert.Equal(-1, model.SelectedIndex);
            Assert.Null(model.SelectedItem);
            Assert.Equal(0, first.SubscriberCount);
            Assert.Equal(1, second.SubscriberCount);
            changes.Add((args.Property.Name, args.OldValue, args.NewValue));
        };

        model.Source = second;

        Assert.Equal(new (string, object?, object?)[]
        {
            ("Source", first, second), ("SelectedIndex", 1, -1), ("SelectedItem", "b", null)
        }, changes);
    }

    [Fact]
    public void SameSourceKeepsSelectionAndNullImmediatelyUnsubscribes()
    {
        var source = new TrackingList<int>([1, 2]);
        var model = new SingleSelectionModel<int>(source) { SelectedIndex = 1 };
        var notifications = 0;
        model.PropertyChanged += (_, _) => notifications++;

        model.Source = source;
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(0, notifications);
        Assert.Equal(1, source.SubscriberCount);

        model.Source = null;
        Assert.Equal(0, source.SubscriberCount);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(3, notifications);
        source.Items.Clear();
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void StaleCallbackFromOldSourceCannotChangeNewSelection()
    {
        var first = new TrackingList<int>([1, 2]);
        var second = new TrackingList<int>([10, 20]);
        var model = new SingleSelectionModel<int>();
        first.Changed += (_, _) =>
        {
            model.Source = second;
            model.Select(1);
        };
        model.Source = first;
        model.Select(0);

        first.Items.Insert(0, 9);

        Assert.Same(second, model.Source);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(20, model.SelectedItem);
        Assert.Equal(1, first.SubscriberCount);
        Assert.Equal(1, second.SubscriberCount);
        second.Items.Insert(0, 5);
        Assert.Equal(2, model.SelectedIndex);
        Assert.Equal(20, model.SelectedItem);
    }

    [Fact]
    public void LongLivedSourceDoesNotKeepModelAliveAndNextEventRemovesSubscription()
    {
        var source = new TrackingList<int>([1, 2]);
        var weakModel = CreateModel(source);
        Assert.Equal(1, source.SubscriberCount);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weakModel.TryGetTarget(out _));
        Assert.Equal(1, source.SubscriberCount);
        source.Items.Add(3);
        Assert.Equal(0, source.SubscriberCount);
        GC.KeepAlive(source);
    }

    [Fact]
    public void SwitchingSourceDoesNotRetainOldList()
    {
        var weakSource = CreateModelWithReplacedSource(out var model);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weakSource.TryGetTarget(out _));
        Assert.Equal(9, model.Source![0]);
        GC.KeepAlive(model);
    }

    [Fact]
    public void ModelsSharingSourceMaintainIndependentSelectionsAndSubscriptions()
    {
        var source = new TrackingList<int>([10, 20, 30]);
        var first = new SingleSelectionModel<int>(source) { SelectedIndex = 0 };
        var second = new SingleSelectionModel<int>(source) { SelectedIndex = 2 };
        Assert.Equal(2, source.SubscriberCount);

        source.Items.RemoveAt(0);
        Assert.Equal(-1, first.SelectedIndex);
        Assert.Equal(1, second.SelectedIndex);
        Assert.Equal(30, second.SelectedItem);

        first.Source = null;
        Assert.Equal(1, source.SubscriberCount);
        source.Items.Insert(0, 5);
        Assert.Equal(-1, first.SelectedIndex);
        Assert.Equal(2, second.SelectedIndex);
        Assert.Equal(30, second.SelectedItem);
        GC.KeepAlive(first);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<SingleSelectionModel<int>> CreateModel(TrackingList<int> source)
    {
        var model = new SingleSelectionModel<int>(source) { SelectedIndex = 1 };
        return new WeakReference<SingleSelectionModel<int>>(model);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<TrackingList<int>> CreateModelWithReplacedSource(out SingleSelectionModel<int> model)
    {
        var source = new TrackingList<int>([1, 2]);
        model = new SingleSelectionModel<int>(source);
        model.Source = new TrackingList<int>([9]);
        return new WeakReference<TrackingList<int>>(source);
    }

    private sealed class TrackingList<T> : IReadOnlyObservableList<T>
    {
        private EventHandler<ListChangedEventArgs<T>>? _changed;

        internal TrackingList(IEnumerable<T> items)
        {
            Items = new ObservableList<T>(items);
            Items.Changed += (_, args) => _changed?.Invoke(this, args);
        }

        internal ObservableList<T> Items { get; }
        internal int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public int Count => Items.Count;
        public T this[int index] => Items[index];

        public event EventHandler<ListChangedEventArgs<T>>? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
