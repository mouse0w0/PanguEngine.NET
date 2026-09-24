using System.Collections;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class ObservableListTests
{
    [Fact]
    public void InterfacesSupportReadingAndCopyingIndependentStorage()
    {
        var source = new List<string?> { "a", null, "a" };
        var list = new ObservableList<string?>(source);
        source.Clear();
        IList<string?> mutable = list;
        IReadOnlyList<string?> readOnly = list;
        Assert.False(mutable.IsReadOnly);
        Assert.Equal(3, readOnly.Count);
        Assert.Null(readOnly[1]);
        Assert.True(list.Contains(null));
        Assert.Equal(0, list.IndexOf("a"));
        Assert.Equal(-1, list.IndexOf("missing"));
        var destination = new string?[5];
        list.CopyTo(destination, 1);
        Assert.Equal(new string?[] { null, "a", null, "a", null }, destination);
        Assert.Equal(new string?[] { "a", null, "a" }, ((IEnumerable)list).Cast<string?>());
    }

    [Fact]
    public void InterfaceMutationsReportExactChangesAndFinalState()
    {
        var list = new ObservableList<int>();
        var changes = Observe(list);
        IList<int> items = list;
        items.Add(2);
        AssertChange(changes[^1], ListChangeKind.Add, 0, [], [2]);
        items.Insert(0, 1);
        AssertChange(changes[^1], ListChangeKind.Add, 0, [], [1]);
        items[1] = 3;
        AssertChange(changes[^1], ListChangeKind.Replace, 1, [2], [3]);
        items[1] = 3;
        AssertChange(changes[^1], ListChangeKind.Replace, 1, [3], [3]);
        Assert.True(items.Remove(1));
        AssertChange(changes[^1], ListChangeKind.Remove, 0, [1], []);
        items.RemoveAt(0);
        AssertChange(changes[^1], ListChangeKind.Remove, 0, [3], []);
        Assert.Empty(list);
        Assert.Equal(6, changes.Count);
    }

    [Fact]
    public void RemoveDeletesFirstEqualOccurrenceAndAllowsNull()
    {
        var list = new ObservableList<string?>(["a", null, "a"]);
        var changes = Observe(list);
        Assert.True(list.Remove("a"));
        Assert.Equal(new string?[] { null, "a" }, list);
        Assert.True(list.Remove(null));
        Assert.False(list.Remove("absent"));
        Assert.Equal(2, changes.Count);
        Assert.Equal(new string?[] { "a" }, list);
    }

    [Fact]
    public void RangeOperationsSendOneEventEach()
    {
        var list = new ObservableList<int>([1, 4]);
        var changes = Observe(list);
        list.InsertRange(1, [2, 3]);
        AssertChange(Assert.Single(changes), ListChangeKind.Add, 1, [], [2, 3]);
        list.AddRange([5, 6]);
        AssertChange(changes[^1], ListChangeKind.Add, 4, [], [5, 6]);
        list.RemoveRange(1, 3);
        AssertChange(changes[^1], ListChangeKind.Remove, 1, [2, 3, 4], []);
        Assert.Equal([1, 5, 6], list);
        list.Clear();
        AssertChange(changes[^1], ListChangeKind.Remove, 0, [1, 5, 6], []);
        Assert.Equal(4, changes.Count);
    }

    [Fact]
    public void ReplaceAllClassifiesEmptyBoundariesAndUnequalLengths()
    {
        var list = new ObservableList<int>();
        var changes = Observe(list);
        list.ReplaceAll([1, 2]);
        AssertChange(changes[^1], ListChangeKind.Add, 0, [], [1, 2]);
        list.ReplaceAll([3]);
        AssertChange(changes[^1], ListChangeKind.Replace, 0, [1, 2], [3]);
        list.ReplaceAll(list);
        AssertChange(changes[^1], ListChangeKind.Replace, 0, [3], [3]);
        list.ReplaceAll([]);
        AssertChange(changes[^1], ListChangeKind.Remove, 0, [3], []);
        list.ReplaceAll([]);
        Assert.Equal(4, changes.Count);
    }

    [Fact]
    public void BulkInputsCanEnumerateTheListItself()
    {
        var list = new ObservableList<int>([1, 2]);
        list.AddRange(list);
        Assert.Equal([1, 2, 1, 2], list);
        list.InsertRange(2, list.Where(x => x == 2));
        Assert.Equal([1, 2, 2, 2, 1, 2], list);
        list.ReplaceAll(list.Where(x => x == 1));
        Assert.Equal([1, 1], list);
    }

    [Fact]
    public void EventsContainReadOnlySnapshotsIndependentOfLaterChanges()
    {
        var list = new ObservableList<int>([3, 1, 2]);
        var changes = Observe(list);
        list.AddRange([4, 5]);
        var add = changes[^1];
        list.Sort();
        var reorder = changes[^1];
        list.RemoveRange(1, 2);
        var rangeRemoval = changes[^1];
        list.Clear();
        var remove = changes[^1];
        list.Add(9);
        Assert.Equal([4, 5], add.NewItems);
        Assert.Equal([2, 0, 1, 3, 4], reorder.Permutation);
        Assert.Equal([2, 3], rangeRemoval.OldItems);
        Assert.Equal([1, 4, 5], remove.OldItems);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)add.NewItems)[0] = 99);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)remove.OldItems).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)rangeRemoval.OldItems)[0] = 99);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)reorder.Permutation)[0] = 99);
    }

    [Theory]
    [InlineData(0, 3, new[] { 1, 2, 3, 0 }, new[] { 3, 0, 1, 2 })]
    [InlineData(3, 0, new[] { 3, 0, 1, 2 }, new[] { 1, 2, 3, 0 })]
    [InlineData(1, 2, new[] { 0, 2, 1, 3 }, new[] { 0, 2, 1, 3 })]
    public void MoveUsesFinalIndex(int oldIndex, int newIndex, int[] expected, int[] permutation)
    {
        var list = new ObservableList<int>([0, 1, 2, 3]);
        var changes = Observe(list);
        list.Move(oldIndex, newIndex);
        Assert.Equal(expected, list);
        AssertReorder(Assert.Single(changes), permutation);
    }

    [Fact]
    public void SortPreservesEqualItemsAndDuplicateReferencesByOccurrence()
    {
        var first = new Item(2, "first");
        var second = new Item(2, "second");
        var smallest = new Item(1, "smallest");
        var list = new ObservableList<Item>([first, smallest, second, first]);
        var changes = Observe(list);
        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        Assert.Equal(new[] { smallest, first, second, first }, list);
        AssertReorder(Assert.Single(changes), [1, 0, 2, 3]);
        list.Add(new Item(0, "appended"));
        Assert.Equal(0, list[^1].Key);
    }

    [Fact]
    public void SortSupportsDefaultComparerCustomComparerAndNullElements()
    {
        var list = new ObservableList<int>([3, 1, 2]);
        list.Sort();
        Assert.Equal([1, 2, 3], list);
        list.Sort(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        Assert.Equal([3, 2, 1], list);
        list.Sort((IComparer<int>?)null);
        Assert.Equal([1, 2, 3], list);
        var strings = new ObservableList<string?>(["b", null, "a"]);
        var changes = Observe(strings);
        strings.Sort(StringComparer.Ordinal);
        Assert.Equal(new string?[] { null, "a", "b" }, strings);
        AssertReorder(Assert.Single(changes), [2, 0, 1]);
    }

    [Fact]
    public void AlreadySortedMultipleItemsDoNotNotifyOrInvalidateEnumeration()
    {
        var list = new ObservableList<int>([1, 2, 2, 3]);
        var changes = Observe(list);
        using var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var comparisons = 0;
        list.Sort((a, b) =>
        {
            comparisons++;
            return a.CompareTo(b);
        });
        Assert.True(comparisons > 0);
        Assert.Empty(changes);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
        Assert.Equal([1, 2, 2, 3], list);
    }

    [Fact]
    public void EveryListenerSeesCompletedRemoveReplaceAndReorderState()
    {
        var list = new ObservableList<int>([3, 1, 2]);
        int[] expected = [3, 2];
        var calls = 0;
        EventHandler<ListChangedEventArgs<int>> first = (_, _) =>
        {
            calls++;
            Assert.Throws<InvalidOperationException>(() => list.Add(99));
            Assert.Equal(expected, list);
        };
        list.Changed += first;
        list.Changed += (_, _) =>
        {
            calls++;
            Assert.Equal(expected, list);
        };
        list.RemoveAt(1);
        expected = [3, 4];
        list[1] = 4;
        expected = [4, 3];
        list.Reverse();
        expected = [3, 4];
        list.Sort();
        Assert.Equal(8, calls);
    }

    [Fact]
    public void ReverseReportsPositionChangesEvenForIdenticalReferences()
    {
        var item = new object();
        var list = new ObservableList<object>([item, item, item]);
        var changes = Observe(list);
        list.Reverse();
        AssertReorder(Assert.Single(changes), [2, 1, 0]);
    }

    [Fact]
    public void NoOpOperationsDoNotNotify()
    {
        var list = new ObservableList<int>();
        var changes = Observe(list);
        list.Clear();
        list.AddRange([]);
        list.InsertRange(0, []);
        list.RemoveRange(0, 0);
        list.ReplaceAll([]);
        list.Sort();
        list.Reverse();
        Assert.False(list.Remove(7));
        Assert.Empty(changes);
        list.Add(1);
        changes.Clear();
        list.Move(0, 0);
        list.Sort();
        list.Reverse();
        list.RemoveRange(list.Count, 0);
        Assert.Empty(changes);
    }

    [Fact]
    public void InvalidArgumentsLeaveStorageUnchangedAndReleaseMutationScope()
    {
        var list = new ObservableList<int>([1, 2]);
        var changes = Observe(list);
        Assert.Throws<ArgumentNullException>(() => new ObservableList<int>(null!));
        Assert.Throws<ArgumentNullException>(() => list.AddRange(null!));
        Assert.Throws<ArgumentNullException>(() => list.InsertRange(0, null!));
        Assert.Throws<ArgumentNullException>(() => list.ReplaceAll(null!));
        Assert.Throws<ArgumentNullException>(() => list.Sort((Comparison<int>)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => list[-1] = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRange(3, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveRange(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveRange(1, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveRange(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Move(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Move(-1, 0));
        Assert.Equal([1, 2], list);
        Assert.Empty(changes);
        list.Add(3);
        Assert.Single(changes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NotificationRejectsAllMutationEntrypointsIncludingNoOps(int listenerCount)
    {
        var list = new ObservableList<int>([1]);
        var calls = 0;
        EventHandler<ListChangedEventArgs<int>> listener = (sender, _) =>
        {
            Assert.Same(list, sender);
            calls++;
            Action[] mutations =
            [
                () => list.Add(2), () => list.Insert(0, 2), () => list[0] = 2,
                () => list.Remove(99), () => list.RemoveAt(0), () => list.Clear(),
                () => list.AddRange([]), () => list.InsertRange(0, []),
                () => list.RemoveRange(list.Count, 0), () => list.ReplaceAll([]),
                () => list.Move(0, 0), () => list.Sort(), () => list.Reverse(),
                () => list.Sort((IComparer<int>?)null), () => list.Sort((a, b) => a.CompareTo(b)),
                () => ((IList<int>)list)[0] = 2, () => ((ICollection<int>)list).Add(2),
                () => list.AddRange(null!), () => list.Insert(-1, 2),
                () => list.Sort((Comparison<int>)null!)
            ];
            foreach (var mutation in mutations)
                Assert.Throws<InvalidOperationException>(mutation);
            Assert.Equal([1, 3], list);
        };
        for (var i = 0; i < listenerCount; i++)
            list.Changed += listener;
        list.Add(3);
        Assert.Equal(listenerCount, calls);
    }

    [Fact]
    public void EmptyClearIsRejectedDuringClearNotification()
    {
        var list = new ObservableList<int>([1]);
        list.Changed += (_, _) => Assert.Throws<InvalidOperationException>(list.Clear);
        list.Clear();
        Assert.Empty(list);
    }

    [Fact]
    public void IndirectReentryIsRejectedWithoutRollingBackEitherList()
    {
        var first = new ObservableList<int>();
        var second = new ObservableList<int>();
        EventHandler<ListChangedEventArgs<int>> fromFirst = (_, _) => second.Add(2);
        EventHandler<ListChangedEventArgs<int>> fromSecond = (_, _) => first.Add(3);
        first.Changed += fromFirst;
        second.Changed += fromSecond;
        Assert.Throws<InvalidOperationException>(() => first.Add(1));
        Assert.Equal([1], first);
        Assert.Equal([2], second);
        first.Changed -= fromFirst;
        second.Changed -= fromSecond;
        first.Add(4);
        second.Add(5);
        Assert.Equal([1, 4], first);
        Assert.Equal([2, 5], second);
    }

    [Fact]
    public void ListenerExceptionStopsDispatchPreservesMutationAndReleasesScope()
    {
        var list = new ObservableList<int>();
        var failure = new Exception("listener failed");
        var laterCalls = 0;
        EventHandler<ListChangedEventArgs<int>> throwing = (_, _) => throw failure;
        list.Changed += throwing;
        list.Changed += (_, _) => laterCalls++;
        Assert.Same(failure, Assert.Throws<Exception>(() => list.Add(1)));
        Assert.Equal([1], list);
        Assert.Equal(0, laterCalls);
        list.Changed -= throwing;
        list.Add(2);
        Assert.Equal([1, 2], list);
        Assert.Equal(1, laterCalls);
    }

    [Fact]
    public void SubscriptionChangesOnlyAffectSubsequentNotifications()
    {
        var list = new ObservableList<int>();
        var calls = new List<string>();
        EventHandler<ListChangedEventArgs<int>> added = (_, _) => calls.Add("added");
        EventHandler<ListChangedEventArgs<int>> removed = (_, _) => calls.Add("removed");
        EventHandler<ListChangedEventArgs<int>>? first = null;
        first = (_, _) =>
        {
            calls.Add("first");
            list.Changed -= first;
            list.Changed -= removed;
            list.Changed += added;
        };
        list.Changed += first;
        list.Changed += removed;
        list.Add(1);
        Assert.Equal(["first", "removed"], calls);
        calls.Clear();
        list.Add(2);
        Assert.Equal(["added"], calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BulkEnumerationFailureIsAtomicAndPreservesOriginalException(int operation)
    {
        var list = new ObservableList<int>([1, 2]);
        var failure = new Exception("enumeration failed");
        var changes = Observe(list);
        IEnumerable<int> Input()
        {
            yield return 3;
            throw failure;
        }
        Action apply = operation switch
        {
            0 => () => list.AddRange(Input()),
            1 => () => list.InsertRange(1, Input()),
            _ => () => list.ReplaceAll(Input())
        };
        Assert.Same(failure, Assert.Throws<Exception>(apply));
        Assert.Equal([1, 2], list);
        Assert.Empty(changes);
        list.Add(3);
        Assert.Single(changes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InputEnumerationCannotMutateTheList(int operation)
    {
        var list = new ObservableList<int>([1]);
        IEnumerable<int> Input()
        {
            yield return 2;
            list.Clear();
        }
        Action apply = operation switch
        {
            0 => () => list.AddRange(Input()),
            1 => () => list.InsertRange(1, Input()),
            _ => () => list.ReplaceAll(Input())
        };
        Assert.Throws<InvalidOperationException>(apply);
        Assert.Equal([1], list);
        list.Add(3);
        Assert.Equal([1, 3], list);
    }

    [Fact]
    public void SortFailurePreservesOriginalExceptionAndList()
    {
        var list = new ObservableList<int>([4, 3, 2, 1]);
        var failure = new Exception("comparison failed");
        var changes = Observe(list);
        var comparisons = 0;
        Assert.Same(failure, Assert.Throws<Exception>(() => list.Sort((a, b) =>
        {
            if (++comparisons == 2)
                throw failure;
            return a.CompareTo(b);
        })));
        Assert.Equal([4, 3, 2, 1], list);
        Assert.Empty(changes);
        list.Sort();
        Assert.Equal([1, 2, 3, 4], list);
        Assert.Single(changes);
    }

    [Fact]
    public void ComparerCannotMutateTheList()
    {
        var list = new ObservableList<int>([2, 1]);
        Assert.Throws<InvalidOperationException>(() => list.Sort((a, b) =>
        {
            list.Add(3);
            return a.CompareTo(b);
        }));
        Assert.Equal([2, 1], list);
        list.Sort();
        Assert.Equal([1, 2], list);
    }

    [Fact]
    public void RemoveEqualityCallbackCannotInvalidateLocatedIndex()
    {
        var list = new ObservableList<ReentrantItem>();
        var item = new ReentrantItem { OnEquals = () => list.Clear() };
        list.Add(item);
        Assert.Throws<InvalidOperationException>(() => list.Remove(new ReentrantItem()));
        Assert.Same(item, Assert.Single(list));
        item.OnEquals = null;
        Assert.True(list.Remove(item));
        Assert.Empty(list);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BulkSnapshotsDoNotShareCallersArrayOrMutableListStorage(int operation)
    {
        var list = new ObservableList<int>([1]);
        var changes = Observe(list);
        int[] input = [2, 3];
        switch (operation)
        {
            case 0:
                list.AddRange(input);
                break;
            case 1:
                list.InsertRange(0, input);
                break;
            default:
                list.ReplaceAll(input);
                break;
        }

        var change = Assert.Single(changes);
        input[0] = 99;
        Assert.Equal([2, 3], change.NewItems);
        int[] expected = operation switch
        {
            0 => [1, 2, 3],
            1 => [2, 3, 1],
            _ => [2, 3]
        };
        Assert.Equal(expected, list);
        list[operation == 0 ? 1 : 0] = 42;
        list.Clear();
        Assert.Equal([2, 3], change.NewItems);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)change.NewItems)[0] = 7);
    }

    [Fact]
    public void UnobservedMutationsPreserveListSemanticsAndLaterSubscription()
    {
        var list = new ObservableList<int>([3, 1, 2]);
        list.Add(4);
        list.Insert(1, 5);
        list[1] = 6;
        Assert.True(list.Remove(6));
        list.RemoveAt(3);
        list.AddRange([7, 8]);
        list.InsertRange(1, [9]);
        list.RemoveRange(1, 1);
        list.Move(4, 0);
        list.Reverse();
        Assert.Equal([7, 2, 1, 3, 8], list);
        list.Sort();
        Assert.Equal([1, 2, 3, 7, 8], list);
        list.ReplaceAll(list.Where(x => x < 4));
        Assert.Equal([1, 2, 3], list);
        list.Clear();
        var changes = Observe(list);
        list.Add(10);
        AssertChange(Assert.Single(changes), ListChangeKind.Add, 0, [], [10]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void BulkEnumerationSubscriptionChangesAffectCurrentNotification(int operation, bool subscribe)
    {
        var list = new ObservableList<int>([1]);
        var changes = new List<ListChangedEventArgs<int>>();
        EventHandler<ListChangedEventArgs<int>> listener = (_, change) => changes.Add(change);
        if (!subscribe)
            list.Changed += listener;
        IEnumerable<int> Input()
        {
            yield return 2;
            if (subscribe)
                list.Changed += listener;
            else
                list.Changed -= listener;
            yield return 3;
        }

        switch (operation)
        {
            case 0:
                list.AddRange(Input());
                break;
            case 1:
                list.InsertRange(0, Input());
                break;
            default:
                list.ReplaceAll(Input());
                break;
        }

        if (subscribe)
            Assert.Equal([2, 3], Assert.Single(changes).NewItems);
        else
            Assert.Empty(changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SortComparisonSubscriptionChangesAffectCurrentNotification(bool subscribe)
    {
        var list = new ObservableList<int>([2, 1]);
        var changes = new List<ListChangedEventArgs<int>>();
        EventHandler<ListChangedEventArgs<int>> listener = (_, change) => changes.Add(change);
        if (!subscribe)
            list.Changed += listener;
        list.Sort((a, b) =>
        {
            if (subscribe)
                list.Changed += listener;
            else
                list.Changed -= listener;
            return a.CompareTo(b);
        });
        Assert.Equal([1, 2], list);
        if (subscribe)
            AssertReorder(Assert.Single(changes), [1, 0]);
        else
            Assert.Empty(changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemoveEqualitySubscriptionChangesAffectCurrentNotification(bool subscribe)
    {
        var list = new ObservableList<ReentrantItem>();
        var changes = new List<ListChangedEventArgs<ReentrantItem>>();
        EventHandler<ListChangedEventArgs<ReentrantItem>> listener = (_, change) => changes.Add(change);
        var item = new ReentrantItem
        {
            OnEquals = () =>
            {
                if (subscribe)
                    list.Changed += listener;
                else
                    list.Changed -= listener;
            }
        };
        list.Add(item);
        if (!subscribe)
            list.Changed += listener;
        Assert.True(list.Remove(item));
        Assert.Empty(list);
        if (subscribe)
        {
            var change = Assert.Single(changes);
            Assert.Equal(ListChangeKind.Remove, change.Kind);
            Assert.Same(item, Assert.Single(change.OldItems));
        }
        else
        {
            Assert.Empty(changes);
        }
    }

    private static List<ListChangedEventArgs<T>> Observe<T>(ObservableList<T> list)
    {
        var changes = new List<ListChangedEventArgs<T>>();
        list.Changed += (sender, change) =>
        {
            Assert.Same(list, sender);
            changes.Add(change);
        };
        return changes;
    }

    private static void AssertChange<T>(ListChangedEventArgs<T> change, ListChangeKind kind,
        int index, T[] oldItems, T[] newItems)
    {
        Assert.Equal(kind, change.Kind);
        Assert.Equal(index, change.Index);
        Assert.Equal(oldItems, change.OldItems);
        Assert.Equal(newItems, change.NewItems);
        Assert.Empty(change.Permutation);
    }

    private static void AssertReorder<T>(ListChangedEventArgs<T> change, int[] permutation)
    {
        Assert.Equal(ListChangeKind.Reorder, change.Kind);
        Assert.Equal(0, change.Index);
        Assert.Empty(change.OldItems);
        Assert.Empty(change.NewItems);
        Assert.Equal(permutation, change.Permutation);
    }

    private sealed record Item(int Key, string Name);

    private sealed class ReentrantItem : IEquatable<ReentrantItem>
    {
        public Action? OnEquals { get; set; }

        public bool Equals(ReentrantItem? other)
        {
            OnEquals?.Invoke();
            return ReferenceEquals(this, other);
        }
    }
}
