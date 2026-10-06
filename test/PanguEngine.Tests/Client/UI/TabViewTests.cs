using System.ComponentModel;
using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Selection;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class TabViewTests
{
    [Fact]
    public void FirstAvailableItemIsSelectedAndLaterAddsDoNotStealIt()
    {
        var view = new TabView();
        var first = new TabItem();
        var second = new TabItem();

        view.Items.Add(first);

        Assert.Same(first, view.Selection.SelectedItem);
        Assert.Equal(0, view.Selection.SelectedIndex);
        Assert.True(first.IsSelected);

        view.Items.Add(second);

        Assert.Same(first, view.Selection.SelectedItem);
        Assert.Equal(0, view.Selection.SelectedIndex);
        Assert.False(second.IsSelected);
    }

    [Fact]
    public void UnavailableFirstItemDoesNotBecomeTheSelection()
    {
        var view = new TabView();

        view.Items.Add(new TabItem { IsEnabled = false });

        Assert.Null(view.Selection.SelectedItem);
        Assert.Equal(-1, view.Selection.SelectedIndex);
    }

    [Fact]
    public void AddingAfterExplicitClearSelectsTheNewItem()
    {
        var view = new TabView();
        var first = new TabItem();
        view.Items.Add(first);
        view.Selection.SelectedItem = null;
        Assert.Null(view.Selection.SelectedItem);

        var second = new TabItem();
        view.Items.Add(second);

        Assert.Same(second, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);
    }

    [Fact]
    public void RemovingSelectedItemSelectsForwardThenBackwardThenClears()
    {
        var (view, _, b, c) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = b;

        view.Items.Remove(b);

        Assert.Same(c, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);

        var (lastView, _, lastMiddle, last) = CreateViewWithThreeItems();
        lastView.Selection.SelectedItem = last;

        lastView.Items.Remove(last);

        Assert.Same(lastMiddle, lastView.Selection.SelectedItem);
        Assert.Equal(1, lastView.Selection.SelectedIndex);

        var onlyView = new TabView();
        var only = new TabItem();
        onlyView.Items.Add(only);

        onlyView.Items.Remove(only);

        Assert.Null(onlyView.Selection.SelectedItem);
        Assert.Equal(-1, onlyView.Selection.SelectedIndex);
    }

    [Fact]
    public void ReplacingSelectedItemUsesTheReplacementAsTheFirstCandidate()
    {
        var (view, _, b, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = b;
        var replacement = new TabItem();

        view.Items[1] = replacement;

        Assert.Same(replacement, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);

        var (disabledView, _, disabledTarget, third) = CreateViewWithThreeItems();
        disabledView.Selection.SelectedItem = disabledTarget;

        disabledView.Items[1] = new TabItem { IsEnabled = false };

        Assert.Same(third, disabledView.Selection.SelectedItem);
        Assert.Equal(2, disabledView.Selection.SelectedIndex);
    }

    [Fact]
    public void DirectSelectionAllowsDisabledAndHiddenItems()
    {
        var (view, _, _, _) = CreateViewWithThreeItems();
        var disabled = new TabItem { IsEnabled = false };
        view.Items.Add(disabled);

        view.Selection.SelectedItem = disabled;
        Assert.Same(disabled, view.Selection.SelectedItem);

        var hidden = new TabItem { Visibility = Visibility.Hidden };
        view.Items.Add(hidden);

        view.Selection.SelectedItem = hidden;
        Assert.Same(hidden, view.Selection.SelectedItem);
    }

    [Fact]
    public void MovePreservesSelectionIdentityWithoutRaisingDragReordered()
    {
        var (view, a, b, c) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = b;
        var reordered = new List<TabReorderedEventArgs>();
        view.TabReordered += (_, args) => reordered.Add(args);
        var selectionChanges = 0;
        view.SelectionChanged += (_, _) => selectionChanges++;

        view.Items.Move(1, 2);

        Assert.Same(a, view.Items[0]);
        Assert.Same(c, view.Items[1]);
        Assert.Same(b, view.Items[2]);
        Assert.Same(b, view.Selection.SelectedItem);
        Assert.Equal(2, view.Selection.SelectedIndex);
        Assert.Empty(reordered);
        Assert.Equal(0, selectionChanges);
    }

    [Fact]
    public void ReversePreservesSelectionIdentityWithoutSelectionNotification()
    {
        var (view, _, selected, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = selected;
        var changes = 0;
        view.SelectionChanged += (_, _) => changes++;

        view.Items.Reverse();

        Assert.Same(selected, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ReplaceAllSelectsTheFirstEnabledReplacement()
    {
        var (view, _, selected, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = selected;
        var disabled = new TabItem { IsEnabled = false };
        var replacement = new TabItem();

        view.Items.ReplaceAll([disabled, replacement]);

        Assert.Same(replacement, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);
    }

    [Fact]
    public void ClearRaisesASingleSelectionChanged()
    {
        var (view, _, b, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = b;
        var changes = new List<TabSelectionChangedEventArgs>();
        view.SelectionChanged += (_, args) => changes.Add(args);

        view.Items.Clear();

        Assert.Empty(view.Items);
        Assert.Null(view.Selection.SelectedItem);
        Assert.Equal(-1, view.Selection.SelectedIndex);
        var args = Assert.Single(changes);
        Assert.Same(b, args.OldItem);
        Assert.Null(args.NewItem);
        Assert.Equal(1, args.OldIndex);
        Assert.Equal(-1, args.NewIndex);
    }

    [Fact]
    public void SelectionChangedReportsOldAndNewIdentityAndIndices()
    {
        var view = new TabView();
        var a = new TabItem();
        var b = new TabItem();
        var changes = new List<TabSelectionChangedEventArgs>();
        view.SelectionChanged += (_, args) => changes.Add(args);

        view.Items.Add(a);
        view.Selection.SelectedItem = a;
        view.Items.Add(b);
        view.Selection.SelectedItem = b;
        view.Items.Remove(a);

        Assert.Equal(2, changes.Count);
        Assert.Null(changes[0].OldItem);
        Assert.Same(a, changes[0].NewItem);
        Assert.Equal(-1, changes[0].OldIndex);
        Assert.Equal(0, changes[0].NewIndex);
        Assert.Same(a, changes[1].OldItem);
        Assert.Same(b, changes[1].NewItem);
        Assert.Equal(0, changes[1].OldIndex);
        Assert.Equal(1, changes[1].NewIndex);
    }

    [Fact]
    public void InsertionAndRemovalOfOtherItemsKeepSelectionWithoutNotification()
    {
        var (view, a, b, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = b;
        var changes = 0;
        view.SelectionChanged += (_, _) => changes++;
        var inserted = new TabItem();

        view.Items.Insert(0, inserted);

        Assert.Same(b, view.Selection.SelectedItem);
        Assert.Equal(2, view.Selection.SelectedIndex);

        view.Items.Remove(a);

        Assert.Same(b, view.Selection.SelectedItem);
        Assert.Equal(1, view.Selection.SelectedIndex);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void DuplicateOrCrossViewMembershipIsRejected()
    {
        var firstView = new TabView();
        var secondView = new TabView();
        var item = new TabItem();
        firstView.Items.Add(item);

        Assert.Throws<InvalidOperationException>(() => secondView.Items.Add(item));
        Assert.Empty(secondView.Items);
        Assert.Single(firstView.Items);
        Assert.Same(firstView, item.OwnerView);

        Assert.Throws<InvalidOperationException>(() => firstView.Items.Add(item));
        Assert.Single(firstView.Items);
    }

    [Fact]
    public void CollectionMutationDuringSelectionNotificationIsRejected()
    {
        var (view, a, b, _) = CreateViewWithThreeItems();
        Exception? addError = null;
        Exception? selectError = null;
        view.SelectionChanged += (_, _) =>
        {
            addError = Record.Exception(() => view.Items.Add(new TabItem()));
            selectError = Record.Exception(() => view.Selection.SelectedItem = a);
        };

        view.Selection.SelectedItem = b;

        Assert.IsType<InvalidOperationException>(addError);
        Assert.IsType<InvalidOperationException>(selectError);
        Assert.Equal(3, view.Items.Count);
        Assert.Same(b, view.Selection.SelectedItem);
    }

    [Fact]
    public void TwoWayBindingWritesBackAfterSelectionNotification()
    {
        var model = new SelectionModel();
        var (view, a, b, _) = CreateViewWithThreeItems();
        model.Selected = a;
        view.Selection.BindTwoWay(SingleSelectionModel<TabItem>.SelectedItemProperty, model, value => value.Selected);
        TabItem? selectedDuringNotification = null;
        view.SelectionChanged += (_, _) => selectedDuringNotification = model.Selected;

        view.Selection.SelectedItem = b;

        Assert.Same(a, selectedDuringNotification);
        Assert.Same(b, model.Selected);
        Assert.Same(b, view.Selection.SelectedItem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingOrReplacingReportsTheOriginalSelectionIndex(bool replace)
    {
        var (view, _, second, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = second;
        TabSelectionChangedEventArgs? change = null;
        view.SelectionChanged += (_, args) => change = args;

        if (replace)
            view.Items[1] = new TabItem();
        else
            view.Items.RemoveAt(1);

        Assert.NotNull(change);
        Assert.Same(second, change.OldItem);
        Assert.Equal(1, change.OldIndex);
    }

    [Fact]
    public void CloseRequestDoesNotSelectOrRemoveAndAllowsHandlerRemoval()
    {
        var view = new TabView();
        var closable = new TabItem { IsClosable = true };
        var other = new TabItem();
        view.Items.Add(closable);
        view.Items.Add(other);
        view.Selection.SelectedItem = other;
        TabItem? requested = null;
        view.TabCloseRequested += (_, args) =>
        {
            requested = args.Item;
            view.Items.Remove(args.Item);
        };

        view.RequestClose(closable);

        Assert.Same(closable, requested);
        Assert.DoesNotContain(closable, view.Items);
        Assert.Same(other, view.Selection.SelectedItem);
    }

    [Fact]
    public void CloseRequestIsOnlyRaisedForClosableAvailableOwnedItems()
    {
        var view = new TabView();
        var item = new TabItem();
        view.Items.Add(item);
        var count = 0;
        view.TabCloseRequested += (_, _) => count++;

        view.RequestClose(item);
        Assert.Equal(0, count);

        item.IsClosable = true;
        item.IsEnabled = false;
        view.RequestClose(item);
        Assert.Equal(0, count);

        item.IsEnabled = true;
        view.RequestClose(item);
        Assert.Equal(1, count);

        view.RequestClose(new TabItem { IsClosable = true });
        Assert.Equal(1, count);
    }

    [Fact]
    public void SelectionDrivesContentHostVisibility()
    {
        var (view, a, b, _) = CreateViewWithThreeItems();

        Assert.Equal(Visibility.Visible, a.ContentHost!.Visibility);
        Assert.Equal(Visibility.Collapsed, b.ContentHost!.Visibility);

        view.Selection.SelectedItem = b;

        Assert.Equal(Visibility.Collapsed, a.ContentHost!.Visibility);
        Assert.Equal(Visibility.Visible, b.ContentHost!.Visibility);
    }

    [Fact]
    public void NamedSelectedPseudoClassCanBeTargetedByCss()
    {
        var view = new TabView();
        var item = new TabItem();
        view.Items.Add(item);
        var screen = new UiScreen(view);

        screen.SetStyleSheets([UiStyleSheet.Parse("TabItem:selected { background-color: #010203; }")]);
        view.UpdateStyles();

        Assert.True(item.HasPseudoClass(UiPseudoClass.Get("selected")));
        Assert.Equal(new SolidColorBrush(1, 2, 3), item.Background);
    }

    [Fact]
    public void BatchStyleSheetsHideTheSelectedTabWithoutChangingSelection()
    {
        var (view, first, second, _) = CreateViewWithThreeItems();
        var screen = new UiScreen(view);
        first.Classes.Add("tab-hidden");

        screen.SetStyleSheets([UiStyleSheet.Parse(".tab-hidden { visibility: hidden; }")]);
        view.UpdateStyles();

        Assert.Equal(Visibility.Hidden, first.Visibility);
        Assert.Same(first, view.Selection.SelectedItem);
        Assert.True(first.IsSelected);
        Assert.False(second.IsSelected);
        Assert.True(first.HasPseudoClass(UiPseudoClass.Get("selected")));
        Assert.False(second.HasPseudoClass(UiPseudoClass.Get("selected")));
    }

    [Fact]
    public void BatchStyleSheetsHideAnUnselectedTabWithoutChangingSelection()
    {
        var (view, first, second, _) = CreateViewWithThreeItems();
        view.Selection.SelectedItem = second;
        var screen = new UiScreen(view);
        first.Classes.Add("tab-hidden");

        screen.SetStyleSheets([UiStyleSheet.Parse(".tab-hidden { visibility: hidden; }")]);
        view.UpdateStyles();

        Assert.Equal(Visibility.Hidden, first.Visibility);
        Assert.Same(second, view.Selection.SelectedItem);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
    }

    [Fact]
    public void AttachingAHiddenItemSelectsIt()
    {
        var view = new TabView();
        var screen = new UiScreen(view);
        screen.SetStyleSheets([UiStyleSheet.Parse(".tab-hidden { visibility: hidden; }")]);
        var item = new TabItem();
        item.Classes.Add("tab-hidden");

        view.Items.Add(item);
        view.UpdateStyles();

        Assert.Equal(Visibility.Hidden, item.Visibility);
        Assert.Same(item, view.Selection.SelectedItem);
        Assert.Same(item, Assert.Single(view.Items));
    }

    [Fact]
    public void AttachingACollapsedItemSelectsIt()
    {
        var view = new TabView();
        var screen = new UiScreen(view);
        screen.SetStyleSheets([UiStyleSheet.Parse(".tab-collapsed { visibility: collapsed; }")]);
        var item = new TabItem();
        item.Classes.Add("tab-collapsed");

        view.Items.Add(item);
        view.UpdateStyles();

        Assert.Equal(Visibility.Collapsed, item.Visibility);
        Assert.Same(item, view.Selection.SelectedItem);
        Assert.Same(item, Assert.Single(view.Items));
    }

    private static (TabView View, TabItem First, TabItem Second, TabItem Third) CreateViewWithThreeItems()
    {
        var view = new TabView();
        var first = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        var second = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        var third = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        view.Items.Add(first);
        view.Items.Add(second);
        view.Items.Add(third);
        return (view, first, second, third);
    }

    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private TabItem? _selected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TabItem? Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value))
                    return;
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }
    }
}
