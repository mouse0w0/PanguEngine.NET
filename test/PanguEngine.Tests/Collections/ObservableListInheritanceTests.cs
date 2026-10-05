using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class ObservableListInheritanceTests
{
    [Fact]
    public void BaseAndInterfaceMutationsUseDerivedCoreWithoutSubscribers()
    {
        var derived = new TrackingList();
        ObservableList<int> list = derived;
        IObservableList<int> items = list;
        items.Add(3);
        items.Insert(0, 1);
        items[0] = 2;
        items.AddRange([4, 5]);
        items.InsertRange(1, [6]);
        items.Move(0, 1);
        list.Sort();
        list.Reverse();
        items.RemoveRange(0, 1);
        items.RemoveAt(0);
        Assert.True(items.Remove(3));
        items.ReplaceAll([9]);
        items.Clear();
        Assert.Equal(new[]
        {
            "insert", "insert", "set", "insert-range", "insert-range", "move",
            "reorder", "reorder", "remove-range", "remove", "remove", "replace-all", "clear"
        }, derived.Changes);
        Assert.Empty(list);
    }

    [Fact]
    public void DerivedPreparationRejectsChangeBeforeStorageAndReleasesScope()
    {
        var list = new TrackingList { Reject = true };
        Assert.Throws<InvalidOperationException>(() => list.Add(1));
        Assert.Empty(list);
        list.Reject = false;
        list.Add(2);
        Assert.Equal([2], list);
    }

    [Fact]
    public void NotificationRunsAfterDerivedOperationReturns()
    {
        var list = new TrackingList();
        list.Changed += (_, _) => Assert.True(list.OperationFinished);
        list.Add(1);
    }

    private sealed class TrackingList : ObservableList<int>
    {
        internal List<string> Changes { get; } = [];
        internal bool Reject { get; set; }
        internal bool OperationFinished { get; private set; }

        private void Before(string operation)
        {
            OperationFinished = false;
            Assert.Throws<InvalidOperationException>(() => Add(99));
            if (Reject)
                throw new InvalidOperationException();
            Changes.Add(operation);
        }

        protected override void InsertItem(int index, int item)
        {
            Before("insert");
            base.InsertItem(index, item);
            OperationFinished = true;
        }

        protected override void SetItem(int index, int value)
        {
            Before("set");
            base.SetItem(index, value);
        }

        protected override void RemoveItem(int index)
        {
            Before("remove");
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            Before("clear");
            base.ClearItems();
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            Before("move");
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void InsertItems(int index, IReadOnlyList<int> items)
        {
            Before("insert-range");
            base.InsertItems(index, items);
        }

        protected override void RemoveItems(int index, int count)
        {
            Before("remove-range");
            base.RemoveItems(index, count);
        }

        protected override void ReplaceItems(IReadOnlyList<int> items)
        {
            Before("replace-all");
            base.ReplaceItems(items);
        }

        protected override void ReorderItems(IReadOnlyList<int> permutation)
        {
            Before("reorder");
            base.ReorderItems(permutation);
        }
    }
}