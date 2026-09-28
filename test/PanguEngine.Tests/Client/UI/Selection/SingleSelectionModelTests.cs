using PanguEngine.Client.UI.Selection;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Selection;

public sealed class SingleSelectionModelTests
{
    [Fact]
    public void EmptyModelHasNoSelectionAndIgnoresSelectionRequests()
    {
        var model = new SingleSelectionModel<string>();
        var changes = new List<Property>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property);

        model.SelectedIndex = 0;
        model.SelectedItem = "missing";
        model.Select(0);
        model.Clear();

        Assert.Null(model.Source);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void InvalidAssignmentClearsButInvalidSelectKeepsSelection(int index)
    {
        var items = new ObservableList<string>(["a", "b", "c"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<(int Old, int New)>();
        using var subscription = model.Subscribe(SingleSelectionModel<string>.SelectedIndexProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        model.Select(index);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal("b", model.SelectedItem);
        Assert.Empty(changes);

        model.SetValue(SingleSelectionModel<string>.SelectedIndexProperty, index);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        Assert.Equal(new[] { (1, -1) }, changes);
    }

    [Fact]
    public void SelectionNotificationsExposeCommittedIndexAndItemInOrder()
    {
        var items = new ObservableList<string>(["a", "b"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly());
        var changes = new List<(string Name, object? Old, object? New)>();
        model.PropertyChanged += (sender, args) =>
        {
            Assert.Same(model, sender);
            Assert.Equal(1, model.SelectedIndex);
            Assert.Equal("b", model.SelectedItem);
            changes.Add((args.Property.Name, args.OldValue, args.NewValue));
        };

        model.SetValue(SingleSelectionModel<string>.SelectedItemProperty, "b");

        Assert.Equal(new (string, object?, object?)[]
        {
            ("SelectedIndex", -1, 1), ("SelectedItem", null, "b")
        }, changes);
    }

    [Fact]
    public void RepeatingSelectionAndClearingTwiceOnlyNotifyActualChanges()
    {
        var items = new ObservableList<string>(["a", "b"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        model.SelectedIndex = 1;
        model.Select(1);
        model.SelectedItem = "b";
        Assert.Empty(changes);
        model.Clear();
        model.Clear();

        Assert.Equal(new[] { "SelectedIndex", "SelectedItem" }, changes);
        Assert.Equal(-1, model.SelectedIndex);
    }

    [Fact]
    public void ItemAssignmentFindsFirstEqualInstanceAndMissingItemClears()
    {
        var first = new Item(1);
        var second = new Item(1);
        var items = new ObservableList<Item>([first, second]);
        var model = new SingleSelectionModel<Item>(items.AsReadOnly()) { SelectedIndex = 1 };
        var changes = new List<(Item? Old, Item? New)>();
        using var subscription = model.Subscribe(SingleSelectionModel<Item>.SelectedItemProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        model.SelectedItem = new Item(1);

        Assert.Equal(0, model.SelectedIndex);
        Assert.Same(first, model.SelectedItem);
        var change = Assert.Single(changes);
        Assert.Same(second, change.Old);
        Assert.Same(first, change.New);

        model.SelectedItem = new Item(2);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void AssigningCurrentDuplicateReferenceSelectsItsFirstOccurrence()
    {
        var item = new object();
        var items = new ObservableList<object>([item, new object(), item]);
        var model = new SingleSelectionModel<object>(items.AsReadOnly()) { SelectedIndex = 2 };
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        model.SelectedItem = item;

        Assert.Equal(0, model.SelectedIndex);
        Assert.Same(item, model.SelectedItem);
        Assert.Equal(new[] { "SelectedIndex" }, changes);
    }

    [Fact]
    public void NullItemIsSelectableAndClearIsDifferentFromAssigningNull()
    {
        var items = new ObservableList<string?>(["a", null, null]);
        var model = new SingleSelectionModel<string?>(items.AsReadOnly());
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        model.SelectedItem = null;
        Assert.Equal(1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        model.Clear();
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(new[] { "SelectedIndex", "SelectedIndex" }, changes);

        model.Source = new ObservableList<string?>(["a"]).AsReadOnly();
        model.SelectedIndex = 0;
        model.SelectedItem = null;
        Assert.Equal(-1, model.SelectedIndex);
    }

    [Fact]
    public void DefaultValueItemDoesNotMeanNoSelection()
    {
        var model = new SingleSelectionModel<int>(new ObservableList<int>([5, 0]).AsReadOnly());
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => changes.Add(args.Property.Name);

        model.SelectedItem = 0;
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(0, model.SelectedItem);
        model.Clear();
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(0, model.SelectedItem);
        Assert.Equal(new[] { "SelectedIndex", "SelectedIndex" }, changes);
    }

    [Fact]
    public void NotificationAllowsConfirmationButRejectsDifferentSelectionAndSource()
    {
        var items = new ObservableList<string>(["a", "b"]);
        var view = items.AsReadOnly();
        var model = new SingleSelectionModel<string>(view);
        var calls = 0;
        model.PropertyChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Property, SingleSelectionModel<string>.SelectedIndexProperty))
                return;
            calls++;
            model.SelectedIndex = 1;
            model.Select(1);
            model.SelectedItem = "b";
            model.Source = view;
            Assert.Throws<InvalidOperationException>(() => model.SelectedIndex = 0);
            Assert.Throws<InvalidOperationException>(() => model.Select(0));
            Assert.Throws<InvalidOperationException>(() => model.SelectedItem = "a");
            Assert.Throws<InvalidOperationException>(() => model.Clear());
            Assert.Throws<InvalidOperationException>(() => model.Source = null);
        };

        model.SelectedIndex = 1;

        Assert.Equal(1, calls);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal("b", model.SelectedItem);
        Assert.Same(view, model.Source);
    }

    [Fact]
    public void NotificationFailurePreservesSelectionStopsDispatchAndReleasesGuard()
    {
        var model = new SingleSelectionModel<string>(new ObservableList<string>(["a", "b"]).AsReadOnly());
        var failure = new InvalidOperationException("listener");
        EventHandler<PropertyChangedEventArgs> handler = (_, _) => throw failure;
        model.PropertyChanged += handler;
        var laterCalls = 0;
        using var subscription = model.Subscribe(SingleSelectionModel<string>.SelectedItemProperty,
            (_, _) => laterCalls++);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => model.SelectedIndex = 1));
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal("b", model.SelectedItem);
        Assert.Equal(0, laterCalls);

        model.PropertyChanged -= handler;
        model.SelectedIndex = 0;
        Assert.Equal("a", model.SelectedItem);
        Assert.Equal(1, laterCalls);
    }

    private sealed record Item(int Value);
}
