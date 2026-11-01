using PanguEngine.Collections;
using PanguEngine.UI.Selection;

namespace PanguEngine.Tests.UI.Selection;

public sealed class MultiSelectionModelTests
{
    [Fact]
    public void ReplacingRangePublishesBothCommittedViewsOnce()
    {
        var source = new ObservableList<int>([10, 20, 30, 40, 50]);
        var model = new MultiSelectionModel<int>(source);
        model.Select(0);
        model.Select(4);
        var indicesCalls = 0;
        var itemsCalls = 0;
        model.SelectedIndices.Changed += (_, change) =>
        {
            indicesCalls++;
            Assert.Equal(ListChangeKind.Replace, change.Kind);
            Assert.Equal(new[] { 0, 4 }, change.OldItems);
            Assert.Equal(new[] { 1, 2, 3 }, change.NewItems);
            Assert.Equal(new[] { 1, 2, 3 }, model.SelectedIndices);
            Assert.Equal(new[] { 20, 30, 40 }, model.SelectedItems);
        };
        model.SelectedItems.Changed += (_, _) => itemsCalls++;

        model.SelectRangeOnly(3, 1);
        model.SelectRange(1, 3);

        Assert.Equal(1, indicesCalls);
        Assert.Equal(1, itemsCalls);
    }

    [Fact]
    public void ModeChangesConstrainProgrammaticOperations()
    {
        var model = new MultiSelectionModel<int>(new ObservableList<int>([1, 2, 3, 4]));
        model.SelectRange(1, 3);

        model.SelectionMode = SelectionMode.Single;
        Assert.Equal(new[] { 1 }, model.SelectedIndices);
        model.Select(3);
        Assert.Equal(new[] { 3 }, model.SelectedIndices);
        Assert.Throws<InvalidOperationException>(() => model.SelectAll());
        Assert.Throws<InvalidOperationException>(() => model.SelectRange(2, 2));
        Assert.Throws<InvalidOperationException>(() => model.SelectRangeOnly(0, 1));
        model.Toggle(3);
        Assert.Empty(model.SelectedIndices);

        model.SelectionMode = SelectionMode.None;
        model.SelectAll();
        model.SelectRangeOnly(0, 3);
        model.Select(2);
        model.SelectionMode = SelectionMode.Multiple;
        Assert.Empty(model.SelectedItems);
    }

    [Fact]
    public void SourceChangesPreserveOccurrencesAndReplaceCancelsEvenTheSameReference()
    {
        var shared = new object();
        var source = new ObservableList<object>([shared, new object(), shared, new object()]);
        var model = new MultiSelectionModel<object>(source);
        model.Select(0);
        model.Select(2);

        source.Insert(1, new object());
        Assert.Equal(new[] { 0, 3 }, model.SelectedIndices);
        source.Move(3, 1);
        Assert.Equal(new[] { 0, 1 }, model.SelectedIndices);
        source.RemoveAt(0);
        Assert.Equal(new[] { 0 }, model.SelectedIndices);
        Assert.Same(shared, Assert.Single(model.SelectedItems));
        source[0] = shared;
        Assert.Empty(model.SelectedIndices);

        model.SelectAll();
        source.Add(new object());
        Assert.False(model.IsSelected(source.Count - 1));
        source.ReplaceAll(source);
        Assert.Empty(model.SelectedItems);
    }

    [Fact]
    public void ReorderCanChangeSelectedItemsWithoutChangingSelectedIndices()
    {
        var source = new ObservableList<int>([1, 2, 3]);
        var model = new MultiSelectionModel<int>(source);
        model.Select(0);
        model.Select(2);
        var indexCalls = 0;
        var itemCalls = 0;
        model.SelectedIndices.Changed += (_, _) => indexCalls++;
        model.SelectedItems.Changed += (_, change) =>
        {
            itemCalls++;
            Assert.Equal(new[] { 1, 3 }, change.OldItems);
            Assert.Equal(new[] { 3, 1 }, change.NewItems);
        };

        source.Reverse();

        Assert.Equal(0, indexCalls);
        Assert.Equal(1, itemCalls);
        Assert.Equal(new[] { 0, 2 }, model.SelectedIndices);
    }

    [Fact]
    public void InvalidRequestsDoNotClearAndNullItemsAreDistinctOccurrences()
    {
        var source = new ObservableList<string?>([null, "a", null]);
        var model = new MultiSelectionModel<string?>(source);
        model.Select(0);
        model.Select(2);
        model.SelectOnly(-1);
        model.SelectRangeOnly(0, 3);

        Assert.Equal(new[] { 0, 2 }, model.SelectedIndices);
        Assert.Equal(2, model.SelectedItems.Count);
        Assert.All(model.SelectedItems, Assert.Null);
        model.Toggle(0);
        Assert.Equal(new[] { 2 }, model.SelectedIndices);
        var indices = model.SelectedIndices;
        var items = model.SelectedItems;
        model.Source = new ObservableList<string?>([null]);
        Assert.Same(indices, model.SelectedIndices);
        Assert.Same(items, model.SelectedItems);
        Assert.Empty(items);
    }

    [Fact]
    public void NotificationRejectsRealReentryButAllowsConfirmation()
    {
        var source = new ObservableList<int>([1, 2, 3]);
        var model = new MultiSelectionModel<int>(source);
        model.SelectedIndices.Changed += (_, _) =>
        {
            model.Source = source;
            model.SelectionMode = SelectionMode.Multiple;
            model.SelectOnly(1);
            Assert.Throws<InvalidOperationException>(() => model.Select(2));
            Assert.Throws<InvalidOperationException>(() => model.Clear());
            Assert.Throws<InvalidOperationException>(() => model.Source = null);
            Assert.Throws<InvalidOperationException>(() => model.SelectionMode = SelectionMode.Single);
        };

        model.SelectOnly(1);
        Assert.Equal(new[] { 1 }, model.SelectedIndices);
    }

    [Fact]
    public void ListenerFailureKeepsCommittedViewsAndReleasesNotificationGuard()
    {
        var model = new MultiSelectionModel<int>(new ObservableList<int>([1, 2, 3]));
        var failure = new InvalidOperationException("listener");
        EventHandler<ListChangedEventArgs<int>> handler = (_, _) => throw failure;
        model.SelectedIndices.Changed += handler;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => model.SelectAll()));
        Assert.Equal(new[] { 0, 1, 2 }, model.SelectedIndices);
        Assert.Equal(new[] { 1, 2, 3 }, model.SelectedItems);
        model.SelectedIndices.Changed -= handler;
        model.SelectOnly(1);
        Assert.Equal(new[] { 2 }, model.SelectedItems);
    }
}
