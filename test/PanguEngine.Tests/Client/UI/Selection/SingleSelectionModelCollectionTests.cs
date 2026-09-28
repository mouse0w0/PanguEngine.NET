using PanguEngine.Client.UI.Selection;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Selection;

public sealed class SingleSelectionModelCollectionTests
{
    [Fact]
    public void InsertAndRemoveRangesBeforeSelectionOnlyChangeIndex()
    {
        var items = new ObservableList<string>(["a", "b", "c", "d"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly()) { SelectedIndex = 2 };
        var indexes = new List<int>();
        model.PropertyChanged += (_, args) =>
        {
            Assert.Same(SingleSelectionModel<string>.SelectedIndexProperty, args.Property);
            Assert.Equal("c", model.SelectedItem);
            Assert.Equal(items[model.SelectedIndex], model.SelectedItem);
            indexes.Add(model.SelectedIndex);
        };

        items.InsertRange(2, ["x", "y"]);
        items.RemoveRange(0, 2);
        items.AddRange(["e", "f"]);
        items.RemoveAt(items.Count - 1);

        Assert.Equal(new[] { 4, 2 }, indexes);
        Assert.Equal(2, model.SelectedIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RemovingSelectedOccurrenceClearsWithoutChoosingAnother(int operation)
    {
        var items = new ObservableList<string>(["a", "b", "c", "b"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<(string Name, object? Old, object? New)>();
        model.PropertyChanged += (_, args) =>
        {
            Assert.Equal(-1, model.SelectedIndex);
            Assert.Null(model.SelectedItem);
            changes.Add((args.Property.Name, args.OldValue, args.NewValue));
        };

        switch (operation)
        {
            case 0: items.RemoveAt(1); break;
            case 1: items.RemoveRange(0, 3); break;
            case 2: items.Clear(); break;
            case 3: items.ReplaceAll(["b", "a"]); break;
        }

        Assert.Equal(new (string, object?, object?)[]
        {
            ("SelectedIndex", 1, -1), ("SelectedItem", "b", null)
        }, changes);
    }

    [Fact]
    public void ReplacingSelectedOccurrenceWithSameReferenceStillClears()
    {
        var item = new object();
        var items = new ObservableList<object>([item, new object()]);
        var model = new SingleSelectionModel<object>(items.AsReadOnly()) { SelectedIndex = 0 };
        var changes = new List<(object? Old, object? New)>();
        using var subscription = model.Subscribe(SingleSelectionModel<object>.SelectedItemProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        items[0] = item;

        Assert.Equal(-1, model.SelectedIndex);
        var change = Assert.Single(changes);
        Assert.Same(item, change.Old);
        Assert.Null(change.New);
    }

    [Fact]
    public void ReplacingOtherItemsDoesNotChangeSelectionButReplaceAllDoes()
    {
        var items = new ObservableList<int>([1, 2, 3]);
        var model = new SingleSelectionModel<int>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        items[0] = 9;
        items[2] = 8;
        Assert.Empty(changes);
        Assert.Equal(2, model.SelectedItem);

        items.ReplaceAll(items);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(new[] { "SelectedIndex", "SelectedItem" }, changes);
    }

    [Theory]
    [InlineData(3, 0, 3, 0)]
    [InlineData(3, 0, 2, 3)]
    [InlineData(0, 3, 2, 1)]
    public void MovePreservesSelectedOccurrence(int from, int to, int selected, int expected)
    {
        var item = new object();
        var items = new ObservableList<object>([item, item, item, item]);
        var model = new SingleSelectionModel<object>(items.AsReadOnly()) { SelectedIndex = selected };
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        items.Move(from, to);

        Assert.Equal(expected, model.SelectedIndex);
        Assert.Same(item, model.SelectedItem);
        Assert.Equal(new[] { "SelectedIndex" }, changes);
    }

    [Fact]
    public void SortAndReversePreserveDuplicateOccurrenceRatherThanFirstEqualItem()
    {
        var shared = new Item(2);
        var items = new ObservableList<Item>([shared, new Item(1), shared, new Item(2)]);
        var model = new SingleSelectionModel<Item>(items.AsReadOnly()) { SelectedIndex = 2 };
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        items.Sort((left, right) => left.Key.CompareTo(right.Key));
        Assert.Equal(2, model.SelectedIndex);
        Assert.Same(shared, model.SelectedItem);
        Assert.Empty(changes);

        items.Reverse();
        Assert.Equal(1, model.SelectedIndex);
        Assert.Same(shared, model.SelectedItem);
        Assert.Equal(new[] { "SelectedIndex" }, changes);
    }

    [Fact]
    public void ChangesToUnselectedListDoNotAutomaticallySelectItems()
    {
        var items = new ObservableList<int>();
        var model = new SingleSelectionModel<int>(items.AsReadOnly());
        var notifications = 0;
        model.PropertyChanged += (_, _) => notifications++;

        items.AddRange([3, 1, 2]);
        items.Sort();
        items.Reverse();
        items.ReplaceAll([9]);
        items.Clear();

        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void CollectionNotificationRejectsNestedMutationAndLaterListenersSeeFinalSelection()
    {
        var items = new ObservableList<string>(["a", "b", "c"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly()) { SelectedIndex = 1 };
        var trace = new List<string>();
        model.PropertyChanged += (_, args) =>
        {
            Assert.Throws<InvalidOperationException>(() => items.Add("nested"));
            trace.Add(args.Property.Name);
        };
        items.Changed += (_, _) =>
        {
            Assert.Equal(-1, model.SelectedIndex);
            Assert.Null(model.SelectedItem);
            trace.Add("collection");
        };

        items.RemoveAt(1);

        Assert.Equal(new[] { "SelectedIndex", "SelectedItem", "collection" }, trace);
        Assert.Equal(new[] { "a", "c" }, items);
    }

    [Fact]
    public void SelectionListenerFailurePreservesListMutationAndAllowsLaterOperations()
    {
        var items = new ObservableList<int>([1, 2, 3]);
        var model = new SingleSelectionModel<int>(items.AsReadOnly()) { SelectedIndex = 1 };
        var failure = new InvalidOperationException("selection listener");
        EventHandler<PropertyChangedEventArgs> handler = (_, _) => throw failure;
        model.PropertyChanged += handler;
        var laterCollectionCalls = 0;
        items.Changed += (_, _) => laterCollectionCalls++;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => items.RemoveAt(1)));
        Assert.Equal(new[] { 1, 3 }, items);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(0, laterCollectionCalls);

        model.PropertyChanged -= handler;
        model.Select(0);
        items.Insert(0, 9);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(1, model.SelectedItem);
        Assert.Equal(1, laterCollectionCalls);
    }

    [Fact]
    public void ReplaceAllClearsEvenWhenSelectedInstanceRemainsAtDifferentIndex()
    {
        var item = new object();
        var items = new ObservableList<object>([new object(), item]);
        var model = new SingleSelectionModel<object>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<(object? Old, object? New)>();
        using var subscription = model.Subscribe(SingleSelectionModel<object>.SelectedItemProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        items.ReplaceAll([item]);

        Assert.Same(item, Assert.Single(items));
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        var change = Assert.Single(changes);
        Assert.Same(item, change.Old);
        Assert.Null(change.New);
    }

    private sealed record Item(int Key);
}
