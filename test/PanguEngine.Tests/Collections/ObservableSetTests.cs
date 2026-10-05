using System.Collections;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class ObservableSetTests
{
    [Fact]
    public void ConstructorsCopyAndDeduplicateUsingTheSelectedComparer()
    {
        var source = new List<string> { "Primary", "primary", "wide" };
        var set = new ObservableSet<string>(source, StringComparer.OrdinalIgnoreCase);
        source.Clear();

        Assert.Equal(2, set.Count);
        Assert.Same(StringComparer.OrdinalIgnoreCase, set.Comparer);
        Assert.True(set.SetEquals(["PRIMARY", "WIDE"]));
        Assert.Empty(new ObservableSet<int>());
        Assert.Same(EqualityComparer<int>.Default, new ObservableSet<int>().Comparer);
        Assert.Same(EqualityComparer<int>.Default, new ObservableSet<int>((IEqualityComparer<int>?)null).Comparer);
        Assert.Same(EqualityComparer<int>.Default, new ObservableSet<int>([1], null).Comparer);
        Assert.True(new ObservableSet<int>([1, 1, 2]).SetEquals([1, 2]));
        Assert.Empty(new ObservableSet<string>(StringComparer.Ordinal));
    }

    [Fact]
    public void InterfacesExposeMatchingRelationsAndIndependentStorage()
    {
        IObservableSet<string> mutable = new ObservableSet<string>(["Primary", "wide"], StringComparer.OrdinalIgnoreCase);
        IReadOnlyObservableSet<string> readOnly = mutable;
        ISet<string> standard = mutable;
        ICollection<string> collection = mutable;

        Assert.False(mutable.IsReadOnly);
        Assert.Equal(readOnly.Count, mutable.Count);
        Assert.True(mutable.Contains("PRIMARY"));
        Assert.True(readOnly.Contains("PRIMARY"));
        Assert.True(mutable.IsSubsetOf(["primary", "wide", "extra"]));
        Assert.True(mutable.IsSupersetOf(["primary"]));
        Assert.True(mutable.IsProperSubsetOf(["primary", "wide", "extra"]));
        Assert.True(mutable.IsProperSupersetOf(["primary"]));
        Assert.True(mutable.Overlaps(["other", "WIDE"]));
        Assert.True(mutable.SetEquals(["wide", "PRIMARY", "primary"]));
        Assert.False(mutable.IsSubsetOf(["primary"]));
        Assert.False(mutable.IsSupersetOf(["extra"]));
        Assert.False(mutable.IsProperSubsetOf(["primary", "wide"]));
        Assert.False(mutable.IsProperSupersetOf(["primary", "wide"]));
        Assert.False(mutable.Overlaps(["other"]));
        Assert.False(mutable.SetEquals(["primary"]));
        Assert.True(readOnly.IsProperSubsetOf(["primary", "wide", "extra"]));
        Assert.True(readOnly.IsProperSupersetOf(["wide"]));
        Assert.True(readOnly.IsSubsetOf(["primary", "wide"]));
        Assert.True(readOnly.IsSupersetOf(["wide"]));
        Assert.True(readOnly.Overlaps(["wide"]));
        Assert.True(readOnly.SetEquals(["wide", "primary"]));

        var destination = new string[4];
        collection.CopyTo(destination, 1);
        Assert.Null(destination[0]);
        Assert.Null(destination[3]);
        Assert.True(new HashSet<string>(destination.Skip(1).Take(2)).SetEquals(["Primary", "wide"]));
        Assert.True(new HashSet<string>(((IEnumerable)mutable).Cast<string>()).SetEquals(["Primary", "wide"]));

        var changes = Observe(mutable);
        collection.Add("extra");
        Assert.False(standard.Add("EXTRA"));
        Assert.True(standard.Remove("PRIMARY"));
        standard.Clear();
        Assert.Equal(3, changes.Count);
        Assert.Empty(readOnly);
    }

    [Fact]
    public void SingleMutationsPublishActualDeltasAndCommittedState()
    {
        var set = new ObservableSet<int>();
        var changes = Observe(set);
        set.Changed += (sender, change) =>
        {
            Assert.Same(set, sender);
            foreach (var item in change.AddedItems)
                Assert.True(set.Contains(item));
            foreach (var item in change.RemovedItems)
                Assert.False(set.Contains(item));
        };

        Assert.True(set.Add(1));
        AssertDelta(changes[^1], [1], []);
        Assert.True(set.Add(2));
        AssertDelta(changes[^1], [2], []);
        Assert.True(set.Remove(1));
        AssertDelta(changes[^1], [], [1]);
        set.Clear();
        AssertDelta(changes[^1], [], [2]);
        Assert.Equal(4, changes.Count);
    }

    [Fact]
    public void NoOpMutationsDoNotNotify()
    {
        var set = new ObservableSet<int>([1, 2]);
        var changes = Observe(set);

        Assert.False(set.Add(1));
        Assert.False(set.Remove(3));
        set.UnionWith([1, 1, 2]);
        set.IntersectWith([2, 1, 1, 3]);
        set.ExceptWith([3, 3]);
        set.SymmetricExceptWith([]);
        Assert.Empty(changes);
        set.Clear();
        set.Clear();
        Assert.Single(changes);
    }

    [Theory]
    [InlineData(0, new[] { 1, 2, 3 }, new[] { 3 }, new int[] { })]
    [InlineData(1, new[] { 2 }, new int[] { }, new[] { 1 })]
    [InlineData(2, new[] { 1 }, new int[] { }, new[] { 2 })]
    [InlineData(3, new[] { 1, 3 }, new[] { 3 }, new[] { 2 })]
    public void BulkOperationsNotifyOnce(int operation, int[] expected, int[] added, int[] removed)
    {
        var set = new ObservableSet<int>([1, 2]);
        var changes = Observe(set);

        ApplyOperation(set, operation, [2, 2, 3, 3]);

        Assert.True(set.SetEquals(expected));
        AssertDelta(Assert.Single(changes), added, removed);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void BulkOperationsCanUseTheSetOrItsReadOnlyView(int operation, bool useView)
    {
        var set = new ObservableSet<int>([1, 2]);
        var changes = Observe(set);
        IEnumerable<int> input = useView ? set.AsReadOnly() : set;

        ApplyOperation(set, operation, input);

        if (operation < 2)
        {
            Assert.True(set.SetEquals([1, 2]));
            Assert.Empty(changes);
        }
        else
        {
            Assert.Empty(set);
            AssertDelta(Assert.Single(changes), [], [1, 2]);
        }
    }

    [Fact]
    public void EquivalentRemovalReportsTheStoredInstance()
    {
        var stored = new Member("primary");
        var equivalent = new Member("primary");
        var set = new ObservableSet<Member>([stored]);
        var changes = Observe(set);

        Assert.False(set.Add(equivalent));
        set.UnionWith([equivalent]);
        set.IntersectWith([equivalent]);
        Assert.Same(stored, Assert.Single(set));
        Assert.True(set.Remove(equivalent));
        Assert.Same(stored, Assert.Single(Assert.Single(changes).RemovedItems));
        Assert.Empty(changes[0].AddedItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BulkRemovalReportsTheStoredInstance(int operation)
    {
        var stored = new Member("primary");
        var retained = new Member("retained");
        var equivalent = new Member("primary");
        var set = new ObservableSet<Member>([stored, retained]);
        var changes = Observe(set);

        switch (operation)
        {
            case 0:
                set.IntersectWith([new Member("retained")]);
                break;
            case 1:
                set.ExceptWith([equivalent, equivalent]);
                break;
            case 2:
                set.SymmetricExceptWith([equivalent, equivalent]);
                break;
        }

        var change = Assert.Single(changes);
        Assert.Same(stored, Assert.Single(change.RemovedItems));
        Assert.Empty(change.AddedItems);
        Assert.Same(retained, Assert.Single(set));
    }

    [Fact]
    public void BulkOperationsUseTheComparerAndPreserveExistingValues()
    {
        var set = new ObservableSet<string>(["Primary", "Wide"], StringComparer.OrdinalIgnoreCase);
        var changes = Observe(set);

        set.UnionWith(["PRIMARY", "extra", "EXTRA"]);
        Assert.Equal("extra", Assert.Single(changes[0].AddedItems));
        set.IntersectWith(["PRIMARY", "EXTRA"]);
        Assert.Equal("Wide", Assert.Single(changes[1].RemovedItems));
        Assert.Contains("Primary", set.ToArray());
        set.SymmetricExceptWith(["PRIMARY", "primary", "next", "NEXT"]);
        AssertDelta(changes[2], ["next"], ["Primary"]);
        set.ExceptWith(["EXTRA"]);
        Assert.Equal("extra", Assert.Single(changes[3].RemovedItems));
        Assert.True(set.SetEquals(["NEXT"]));
    }

    [Fact]
    public void NullableElementsFollowNormalSetSemantics()
    {
        var set = new ObservableSet<string?>([null, "primary", null]);
        var changes = Observe(set);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(null));
        Assert.False(set.Add(null));
        Assert.True(set.Remove(null));
        Assert.Null(Assert.Single(Assert.Single(changes).RemovedItems));
        Assert.True(set.Add(null));
        Assert.Null(Assert.Single(changes[1].AddedItems));
    }

    [Fact]
    public void NullInitialSequencesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ObservableSet<int>((IEnumerable<int>)null!));
        Assert.Throws<ArgumentNullException>(() => new ObservableSet<int>(null!, EqualityComparer<int>.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NullBulkInputsAreRejectedWithoutChangingMembers(int operation)
    {
        var set = new ObservableSet<int>([1]);
        var changes = Observe(set);

        Assert.Throws<ArgumentNullException>(() => ApplyOperation(set, operation, null!));

        Assert.True(set.SetEquals([1]));
        Assert.Empty(changes);
        Assert.True(set.Add(2));
    }

    [Fact]
    public void SnapshotsAreReadOnlyAndIndependentOfLaterMutations()
    {
        var set = new ObservableSet<int>([1, 2]);
        var changes = Observe(set);
        set.SymmetricExceptWith([2, 3]);
        var change = Assert.Single(changes);
        set.Clear();
        set.Add(4);

        AssertDelta(change, [3], [2]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)change.AddedItems)[0] = 99);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)change.RemovedItems).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<int>)changes[1].AddedItems).Add(99));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FailingBulkInputDoesNotCommitPartialChanges(int operation)
    {
        var expected = new InvalidOperationException("input failed");
        var set = new ObservableSet<int>([1, 2]);
        var changes = Observe(set);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            ApplyOperation(set, operation, FailingInput(expected)));

        Assert.Same(expected, actual);
        Assert.True(set.SetEquals([1, 2]));
        Assert.Empty(changes);
        Assert.True(set.Add(4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddComparisonFailureDoesNotChangeMembersAndReleasesTheMutationScope(bool failEquality)
    {
        var expected = new InvalidOperationException("comparison failed");
        var comparer = new CallbackComparer();
        var set = new ObservableSet<int>([1], comparer);
        var changes = Observe(set);
        if (failEquality)
            comparer.OnEquals = () => throw expected;
        else
            comparer.OnHash = _ => throw expected;

        var actual = Assert.Throws<InvalidOperationException>(() => set.Add(2));

        Assert.Same(expected, actual);
        comparer.OnEquals = null;
        comparer.OnHash = null;
        Assert.True(set.SetEquals([1]));
        Assert.Empty(changes);
        Assert.True(set.Add(2));
    }

    [Theory]
    [InlineData(0, new[] { 3, 4 }, new[] { 3, 4 })]
    [InlineData(1, new int[] { }, new[] { 1, 2 })]
    [InlineData(2, new[] { 1, 2 }, new[] { 1, 2 })]
    [InlineData(3, new[] { 1, 3 }, new[] { 1, 3 })]
    public void BulkComparisonFailureKeepsCompletedChangesWithoutNotificationAndReleasesTheMutationScope(
        int operation, int[] input, int[] changedCandidates)
    {
        var expected = new InvalidOperationException("bulk comparison failed");
        var comparer = new CallbackComparer();
        var set = new ObservableSet<int>([1, 2], comparer);
        var view = set.AsReadOnly();
        var changes = Observe(set);
        var viewChanges = Observe(view);
        comparer.OnHash = _ =>
        {
            if (set.Count != 2)
                throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => ApplyOperation(set, operation, input)));

        comparer.OnHash = null;
        var membershipChanges = new HashSet<int>([1, 2]);
        membershipChanges.SymmetricExceptWith(set);
        Assert.Contains(Assert.Single(membershipChanges), changedCandidates);
        Assert.True(view.SetEquals(set));
        Assert.Empty(changes);
        Assert.Empty(viewChanges);
        Assert.True(set.Add(5));
        AssertDelta(Assert.Single(changes), [5], []);
        Assert.Same(changes[0], Assert.Single(viewChanges));
    }

    [Fact]
    public void InputEnumerationCannotReenterTheSourceSet()
    {
        var set = new ObservableSet<int>([1]);
        var changes = Observe(set);

        Assert.Throws<InvalidOperationException>(() => set.UnionWith(ReentrantInput(set)));

        Assert.True(set.SetEquals([1]));
        Assert.Empty(changes);
        Assert.True(set.Add(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComparerCallbacksCannotReenterTheSourceSet(bool duringEquality)
    {
        var comparer = new CallbackComparer();
        var set = new ObservableSet<int>([1], comparer);
        if (duringEquality)
            comparer.OnEquals = set.Clear;
        else
            comparer.OnHash = _ => set.Clear();

        Assert.Throws<InvalidOperationException>(() => set.Add(2));

        comparer.OnEquals = null;
        comparer.OnHash = null;
        Assert.True(set.SetEquals([1]));
        Assert.True(set.Add(2));
    }

    [Fact]
    public void AllMutationEntrypointsRejectReentrancyEvenForNoOps()
    {
        var set = new ObservableSet<int>();
        Action[] mutations =
        [
            () => set.Add(1),
            () => set.Remove(99),
            set.Clear,
            () => set.UnionWith([1]),
            () => set.IntersectWith([1]),
            () => set.ExceptWith([]),
            () => set.SymmetricExceptWith([]),
            () => ((ICollection<int>)set).Add(1)
        ];
        var calls = 0;
        set.Changed += (_, _) =>
        {
            Assert.True(set.Contains(1));
            foreach (var mutation in mutations)
            {
                Assert.Throws<InvalidOperationException>(mutation);
                calls++;
            }
        };

        set.Add(1);

        Assert.Equal(mutations.Length, calls);
        Assert.True(set.SetEquals([1]));
    }

    [Fact]
    public void CallbackCanReadThisSetAndModifyAnotherSet()
    {
        var set = new ObservableSet<int>();
        var other = new ObservableSet<int>();
        set.Changed += (_, _) =>
        {
            Assert.True(set.SetEquals([1]));
            Assert.Single(set);
            other.Add(2);
        };

        set.Add(1);

        Assert.True(other.SetEquals([2]));
    }

    [Fact]
    public void ListenerFailureKeepsMembersStopsNotificationAndReleasesTheMutationScope()
    {
        var expected = new InvalidOperationException("listener failed");
        var set = new ObservableSet<int>();
        var laterCalls = 0;
        EventHandler<SetChangedEventArgs<int>> failing = (_, _) => throw expected;
        set.Changed += failing;
        set.Changed += (_, _) => laterCalls++;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => set.Add(1)));

        Assert.True(set.SetEquals([1]));
        Assert.Equal(0, laterCalls);
        set.Changed -= failing;
        set.Add(2);
        Assert.Equal(1, laterCalls);
    }

    [Fact]
    public void SubscriptionChangesDuringInputPreparationAffectTheCurrentEvent()
    {
        var set = new ObservableSet<int>([1]);
        var removedCalls = 0;
        var addedCalls = 0;
        EventHandler<SetChangedEventArgs<int>> removed = (_, _) => removedCalls++;
        EventHandler<SetChangedEventArgs<int>> added = (_, _) => addedCalls++;
        set.Changed += removed;
        IEnumerable<int> Input()
        {
            set.Changed -= removed;
            set.Changed += added;
            yield return 2;
        }

        set.UnionWith(Input());

        Assert.Equal(0, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    [Fact]
    public void SubscriptionChangesDuringPublicationAffectSubsequentEvents()
    {
        var set = new ObservableSet<int>();
        var removedCalls = 0;
        var addedCalls = 0;
        EventHandler<SetChangedEventArgs<int>> removed = (_, _) => removedCalls++;
        EventHandler<SetChangedEventArgs<int>> added = (_, _) => addedCalls++;
        EventHandler<SetChangedEventArgs<int>> first = (_, _) =>
        {
            set.Changed -= removed;
            set.Changed += added;
        };
        set.Changed += first;
        set.Changed += removed;

        set.Add(1);
        Assert.Equal(1, removedCalls);
        Assert.Equal(0, addedCalls);
        set.Changed -= first;
        set.Add(2);
        Assert.Equal(1, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    private static List<SetChangedEventArgs<T>> Observe<T>(IReadOnlyObservableSet<T> set)
    {
        var changes = new List<SetChangedEventArgs<T>>();
        set.Changed += (_, change) => changes.Add(change);
        return changes;
    }

    private static void AssertDelta<T>(SetChangedEventArgs<T> change, T[] added, T[] removed)
    {
        Assert.Equal(added.Length, change.AddedItems.Count);
        Assert.Equal(removed.Length, change.RemovedItems.Count);
        Assert.True(new HashSet<T>(change.AddedItems).SetEquals(added));
        Assert.True(new HashSet<T>(change.RemovedItems).SetEquals(removed));
    }

    private static void ApplyOperation(IObservableSet<int> set, int operation, IEnumerable<int> input)
    {
        Action<IEnumerable<int>>[] operations =
            [set.UnionWith, set.IntersectWith, set.ExceptWith, set.SymmetricExceptWith];
        operations[operation](input);
    }

    private static IEnumerable<int> FailingInput(Exception expected)
    {
        yield return 2;
        yield return 3;
        throw expected;
    }

    private static IEnumerable<int> ReentrantInput(ObservableSet<int> set)
    {
        yield return 2;
        set.Clear();
        yield return 3;
    }

    private sealed record Member(string Name);

    private sealed class CallbackComparer : IEqualityComparer<int>
    {
        internal Action<int>? OnHash { get; set; }
        internal Action? OnEquals { get; set; }

        public bool Equals(int left, int right)
        {
            OnEquals?.Invoke();
            return left == right;
        }

        public int GetHashCode(int value)
        {
            OnHash?.Invoke(value);
            return 0;
        }
    }
}
