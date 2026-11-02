using PanguEngine.Client.UI;
using PanguEngine.Collections;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
[UIThreadTest]
public sealed class ItemsViewTests
{
    [Fact]
    public void ScrollingRealizesABoundedWindowAndReusesClearedContent()
    {
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 10000)),
            ItemTemplate = template,
            ItemExtent = 24,
            Width = 240,
            Height = 144
        };
        var (manager, _) = Open(view, new Size(240, 144));
        Assert.InRange(template.Active.Count, 1, 40);
        var initialBuilds = template.Builds;

        view.ScrollIntoView(9000);
        manager.UpdateFrame(new Size(240, 144), 0);

        Assert.Contains(9000, template.Active.Keys);
        Assert.DoesNotContain(0, template.Active.Keys);
        Assert.InRange(template.Active.Count, 1, 40);
        Assert.True(template.Clears > 0);
        Assert.True(template.Updates > 0);
        Assert.True(template.Builds < initialBuilds + 40);
        view.ItemsSource = null;
        Assert.Empty(template.Active);
        manager.Close();
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void GridFlowUsesVisualAxesAndLocatesAnOffscreenItem(Orientation flow)
    {
        var template = new RecordingTemplate();
        var view = new GridView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 10000)),
            ItemTemplate = template,
            Flow = flow,
            ItemWidth = 50,
            ItemHeight = 40,
            Width = 260,
            Height = 220
        };
        var (manager, _) = Open(view, new Size(260, 220));
        var first = ScenePosition(template.Active[0]);
        var second = ScenePosition(template.Active[1]);
        if (flow == Orientation.Horizontal)
        {
            Assert.True(second.X > first.X);
            Assert.Equal(first.Y, second.Y);
        }
        else
        {
            Assert.Equal(first.X, second.X);
            Assert.True(second.Y > first.Y);
        }

        view.ScrollIntoView(9998);
        manager.UpdateFrame(new Size(260, 220), 0);

        var target = ScenePosition(template.Active[9998]);
        Assert.InRange(target.X, 0, 259);
        Assert.InRange(target.Y, 0, 219);
        Assert.Empty(view.Selection.SelectedIndices);
        view.ItemsSource = null;
        manager.Close();
    }

    [Fact]
    public void HiddenViewsClearRemovedDataWithoutWaitingForAnotherLayout()
    {
        var source = new ObservableList<int>(Enumerable.Range(0, 100));
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = source,
            ItemTemplate = template,
            Width = 240,
            Height = 120
        };
        var (manager, _) = Open(view, new Size(240, 120));
        view.Visibility = Visibility.Collapsed;
        var clears = template.Clears;

        source.RemoveAt(0);

        Assert.Equal(clears + 1, template.Clears);
        Assert.DoesNotContain(0, template.Active.Keys);
        view.ItemsSource = null;
        Assert.Empty(template.Active);
        var cleared = template.Clears;
        view.ItemTemplate = null;
        Assert.Equal(cleared, template.Clears);
        manager.Close();
    }

    [Fact]
    public void ARecyclingTemplateCanReplaceItsRootWithoutClearingTheOldRootTwice()
    {
        var template = new RecordingTemplate { ReplaceOnUpdate = true };
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 1000)),
            ItemTemplate = template,
            Width = 200,
            Height = 120
        };
        var (manager, _) = Open(view, new Size(200, 120));
        view.ScrollIntoView(800);
        manager.UpdateFrame(new Size(200, 120), 0);

        Assert.True(template.Updates > 0);
        Assert.Contains(800, template.Active.Keys);
        view.ItemsSource = null;
        Assert.Empty(template.Active);
        manager.Close();
    }

    [Fact]
    [UIThreadTest(false)]
    public void UnconstrainedVirtualViewsRejectAnInfiniteViewport()
    {
        using var context = new TextTestContext();
        var view = new ListView<int> { ItemsSource = new ObservableList<int>([1, 2]) };
        Assert.Throws<InvalidOperationException>(() => view.Measure(Size.Infinite));
        view.Width = 100;
        view.Height = 100;
        var (manager, _) = Open(view, new Size(100, 100));
        Assert.True(view.IsMeasureValid);
        manager.Close();
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void ListOrientationAndDeferredLocationUseTheFinalViewport(Orientation orientation)
    {
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 500)),
            ItemTemplate = template,
            Orientation = orientation,
            ItemExtent = 60,
            Spacing = 4,
            Width = 240,
            Height = 180
        };
        view.ScrollIntoView(480);
        view.Selection.SelectOnly(4);
        var (manager, _) = Open(view, new Size(240, 180));
        var target = ScenePosition(template.Active[480]);
        Assert.InRange(target.X, 0, 239);
        Assert.InRange(target.Y, 0, 179);
        Assert.Equal(new[] { 4 }, view.Selection.SelectedIndices);
        view.Orientation = orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        manager.UpdateFrame(new Size(240, 180), 0);
        Assert.Contains(0, template.Active.Keys);
        Assert.Equal(new[] { 4 }, view.Selection.SelectedIndices);
        view.ItemsSource = null;
        manager.Close();
    }

    [Fact]
    [UIThreadTest(false)]
    public void ClosedGenericCssAndSelectedStylesFollowRecycledContainers()
    {
        using var context = new TextTestContext();
        var first = new ListView<int> { ItemsSource = new ObservableList<int>(Enumerable.Range(0, 200)), Height = 100 };
        var second = new ListView<string> { ItemsSource = new ObservableList<string>(["a", "b"]), Height = 100 };
        var grid = new GridView<int> { ItemsSource = new ObservableList<int>([1]), Height = 100 };
        var root = new StackPanel();
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(grid);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("""
            ListView { item-extent: 25px; }
            GridView { item-width: 45px; item-height: 35px; }
            ItemContainer { opacity: 0.8; }
            ItemContainer:selected { opacity: 0.5; }
            """)]);
        manager.Open(scene);
        manager.UpdateFrame(new Size(240, 400), 0);
        Assert.Equal(25, first.ItemExtent);
        Assert.Equal(25, second.ItemExtent);
        Assert.Equal(45, grid.ItemWidth);
        first.Selection.SelectOnly(0);
        manager.UpdateFrame(new Size(240, 400), 0);
        Assert.Equal(0.5, FindContainer(first, 0).Opacity);
        first.ScrollIntoView(180);
        manager.UpdateFrame(new Size(240, 400), 0);
        Assert.Equal(0.8, FindContainer(first, 180).Opacity);
        Assert.Equal(new[] { 0 }, first.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    [UIThreadTest(false)]
    public void SelectionModeNotifiesBothEntrypointsWithoutDuplicatingState()
    {
        var view = new ListView<int> { ItemsSource = new ObservableList<int>([1, 2, 3]) };
        var model = new MultiSelectionModel<int> { SelectionMode = SelectionMode.Single };
        view.BindTwoWay(ItemsView<int>.SelectionModeProperty, model, MultiSelectionModel<int>.SelectionModeProperty);
        var changes = 0;
        view.Subscribe(ListView<int>.SelectionModeProperty, (_, args) =>
        {
            changes++;
            Assert.Equal(args.NewValue, view.Selection.SelectionMode);
            if (args.NewValue == SelectionMode.Single)
                Assert.Single(view.Selection.SelectedIndices);
        });
        view.Selection.SelectionMode = SelectionMode.Multiple;
        Assert.Equal(SelectionMode.Multiple, model.SelectionMode);
        view.Selection.SelectAll();
        view.SelectionMode = SelectionMode.Single;
        view.SelectionMode = SelectionMode.Single;
        Assert.Equal(2, changes);
        Assert.Equal(new[] { 0 }, view.Selection.SelectedIndices);
        Assert.Equal(SelectionMode.Single, model.SelectionMode);
        model.SelectionMode = SelectionMode.None;
        Assert.Equal(3, changes);
        Assert.Equal(SelectionMode.None, view.SelectionMode);
        Assert.Empty(view.Selection.SelectedIndices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InheritedPropertyBindingsCoordinateChangesBeforeNotifyingAndIgnoreSameReferences(bool grid)
    {
        ItemsView<int> view = grid ? new GridView<int>() : new ListView<int>();
        var source = new ObservableList<int>([0, 1, 2]);
        var template = new RecordingTemplate();
        var bindingSource = new ListView<int> { ItemsSource = source, ItemTemplate = template };
        view.BindTwoWay(ItemsView<int>.ItemsSourceProperty, bindingSource, ListView<int>.ItemsSourceProperty);
        view.BindTwoWay(ItemsView<int>.ItemTemplateProperty, bindingSource, ListView<int>.ItemTemplateProperty);
        view.Width = 240;
        view.Height = 120;
        var (manager, _) = Open(view, new Size(240, 120));
        view.Selection.SelectOnly(1);
        var builds = template.Builds;
        var changes = 0;
        var nextSource = new ObservableList<int>([3, 4]);
        using var sourceSubscription = view.Subscribe(ItemsView<int>.ItemsSourceProperty, (_, args) =>
        {
            changes++;
            Assert.Same(nextSource, args.NewValue);
            Assert.Same(nextSource, view.Selection.Source);
            Assert.Empty(view.Selection.SelectedIndices);
            Assert.Empty(template.Active);
            Assert.False(view.IsMeasureValid);
        });
        view.SetValue(ItemsView<int>.ItemsSourceProperty, source);
        view.SetValue(ItemsView<int>.ItemTemplateProperty, template);
        manager.UpdateFrame(new Size(240, 120), 0);
        Assert.Equal(0, changes);
        Assert.Equal(0, template.Clears);
        Assert.Equal(builds, template.Builds);
        Assert.Equal(new[] { 1 }, view.Selection.SelectedIndices);

        bindingSource.ItemsSource = nextSource;
        Assert.Equal(1, changes);
        manager.UpdateFrame(new Size(240, 120), 0);
        Assert.Contains(3, template.Active.Keys);
        var replacement = new RecordingTemplate();
        var templateChanges = 0;
        using var templateSubscription = view.Subscribe(ItemsView<int>.ItemTemplateProperty, (_, args) =>
        {
            templateChanges++;
            Assert.Same(replacement, args.NewValue);
            Assert.Empty(template.Active);
            Assert.False(view.IsMeasureValid);
        });
        view.ItemTemplate = replacement;
        Assert.Same(replacement, bindingSource.ItemTemplate);
        Assert.Equal(1, templateChanges);
        manager.UpdateFrame(new Size(240, 120), 0);
        Assert.Contains(3, replacement.Active.Keys);
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearFailuresKeepTheNewPropertyValueWithoutPublishingItsNotification(bool changeTemplate)
    {
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>([0, 1]),
            ItemTemplate = template,
            Width = 240,
            Height = 120
        };
        var (manager, _) = Open(view, new Size(240, 120));
        var changes = 0;
        using var sourceSubscription = view.Subscribe(ItemsView<int>.ItemsSourceProperty, (_, _) => changes++);
        using var templateSubscription = view.Subscribe(ItemsView<int>.ItemTemplateProperty, (_, _) => changes++);
        var failure = new InvalidOperationException("Clear failed.");
        template.ClearFailure = failure;
        var nextSource = new ObservableList<int>([2, 3]);
        var nextTemplate = new RecordingTemplate();
        var thrown = Assert.Throws<InvalidOperationException>(() =>
        {
            if (changeTemplate)
                view.ItemTemplate = nextTemplate;
            else
                view.ItemsSource = nextSource;
        });
        Assert.Same(failure, thrown);
        Assert.Equal(0, changes);
        if (changeTemplate)
            Assert.Same(nextTemplate, view.ItemTemplate);
        else
        {
            Assert.Same(nextSource, view.ItemsSource);
            Assert.Same(nextSource, view.Selection.Source);
        }
        template.ClearFailure = null;
        view.ItemsSource = null;
        Assert.Empty(template.Active);
        manager.Close();
    }

    [Fact]
    public void EmptyRecyclingResultsStillOccupySelectableSlots()
    {
        var template = new RecordingTemplate { EmptyOnUpdate = true };
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 1000)),
            ItemTemplate = template,
            Width = 200,
            Height = 120
        };
        var (manager, _) = Open(view, new Size(200, 120));
        view.ScrollIntoView(800);
        manager.UpdateFrame(new Size(200, 120), 0);
        Assert.True(template.Updates > 0);
        var emptyIndex = template.EmptyResults.Min();
        view.Selection.SelectOnly(emptyIndex);
        manager.UpdateFrame(new Size(200, 120), 0);
        Assert.True(FindContainer(view, emptyIndex).HasPseudoClass(PseudoClass.Selected));
        view.ItemsSource = null;
        Assert.Empty(template.Active);
        manager.Close();
    }

    [Fact]
    public void LocatingANearbyItemRearrangesContentWithoutRebuildingTheWindow()
    {
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 20)),
            ItemTemplate = template,
            ItemExtent = 60,
            Width = 240,
            Height = 100,
            Padding = Thickness.Zero,
            BorderThickness = Thickness.Zero
        };
        var (manager, _) = Open(view, new Size(240, 100));
        var first = ScenePosition(template.Active[1]).Y;
        var builds = template.Builds;
        var clears = template.Clears;
        view.ScrollIntoView(1);
        manager.UpdateFrame(new Size(240, 100), 0);
        Assert.Equal(first - 20, ScenePosition(template.Active[1]).Y);
        Assert.Equal(builds, template.Builds);
        Assert.Equal(clears, template.Clears);
        view.ItemsSource = null;
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualWidthsLargerThanMeasureConstraintsConverge(bool grid)
    {
        var template = new RecordingTemplate();
        Control view = grid
            ? new GridView<int>
            {
                ItemsSource = new ObservableList<int>(Enumerable.Range(0, 100)),
                ItemTemplate = template, ItemWidth = 60, ItemHeight = 40
            }
            : new ListView<int>
            {
                ItemsSource = new ObservableList<int>(Enumerable.Range(0, 100)),
                ItemTemplate = template
            };
        view.Width = 240;
        view.Height = 160;
        var (manager, _) = Open(view, new Size(120, 160));
        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);
        Assert.Equal(240, view.LayoutBounds.Width);
        var builds = template.Builds;
        manager.UpdateFrame(new Size(100, 160), 0);
        Assert.True(view.IsArrangeValid);
        Assert.Equal(builds, template.Builds);
        manager.Close();
    }

    [Fact]
    public void ContentSizedGridsCanGrowAfterTheirSourceChanges()
    {
        var source = new ObservableList<int>([0]);
        var view = new GridView<int>
        {
            ItemsSource = source,
            ItemTemplate = new FuncDataTemplate<int>(_ => new Panel()),
            HorizontalAlignment = HorizontalAlignment.Left,
            Height = 160,
            ItemWidth = 60,
            ItemHeight = 40
        };
        var (manager, _) = Open(view, new Size(240, 160));
        var initialWidth = view.LayoutBounds.Width;
        source.AddRange(Enumerable.Range(1, 99));
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.True(view.LayoutBounds.Width > initialWidth);
        manager.Close();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1.25, true)]
    [InlineData(1.25, false)]
    public void FractionalItemsUseTheSameGeometryForArrangementAndLocation(double scale, bool rounding)
    {
        var template = new RecordingTemplate();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 100)),
            ItemTemplate = template,
            ItemExtent = 32.5,
            Width = 240,
            Height = 160,
            BorderThickness = Thickness.Zero,
            Padding = Thickness.Zero
        };
        var manager = new UIManager();
        var scene = new GameUIScene(view) { Scale = scale, UseLayoutRounding = rounding };
        manager.Open(scene);
        var size = new Size(240 * scale, 160 * scale);
        manager.UpdateFrame(size, 0);
        var first = FindContainer(view, 0);
        var second = FindContainer(view, 1);
        Assert.Equal(first.LayoutBounds.Height, second.LayoutBounds.Y);
        view.ScrollIntoView(99);
        manager.UpdateFrame(size, 0);
        var last = FindContainer(view, 99);
        var bottom = ScenePosition(last).Y + last.LayoutBounds.Height;
        Assert.Equal(160, bottom, 8);
        view.ItemsSource = null;
        manager.Close();
    }

    [Fact]
    public void GridColumnCountUsesRoundedTileWidths()
    {
        var template = new RecordingTemplate();
        var view = new GridView<int>
        {
            ItemsSource = new ObservableList<int>([0, 1]),
            ItemTemplate = template,
            ItemWidth = 32.5,
            ItemHeight = 32.5,
            Width = 65,
            Height = 100,
            Padding = Thickness.Zero,
            BorderThickness = Thickness.Zero
        };
        var (manager, _) = Open(view, new Size(65, 100));
        Assert.True(ScenePosition(template.Active[1]).Y > ScenePosition(template.Active[0]).Y);
        view.ItemsSource = null;
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest(false)]
    public void DefaultTextRootsAreReusedAndDisplayTheirNewItems(bool grid)
    {
        using var context = new TextTestContext();
        var source = new ObservableList<string>(Enumerable.Range(0, 1000).Select(index => $"item {index}"));
        Control view;
        Action<int> scrollIntoView;
        if (grid)
        {
            var tiles = new GridView<string>
            {
                ItemsSource = source, ItemWidth = 80, ItemHeight = 24, Width = 240, Height = 144
            };
            view = tiles;
            scrollIntoView = tiles.ScrollIntoView;
        }
        else
        {
            var list = new ListView<string>
            {
                ItemsSource = source, ItemExtent = 24, Width = 240, Height = 144
            };
            view = list;
            scrollIntoView = list.ScrollIntoView;
        }
        var (manager, _) = Open(view, new Size(240, 144));
        var previous = RealizedItems(view).Select(item => Assert.IsType<Text>(item.ContentRoot)).ToArray();
        Assert.NotEmpty(previous);
        scrollIntoView(900);
        manager.UpdateFrame(new Size(240, 144), 0);
        var current = RealizedItems(view).Select(item => Assert.IsType<Text>(item.ContentRoot)).ToArray();
        Assert.Contains(previous, root => current.Contains(root));
        Assert.Contains(current, root => root.Content == source[900]);
        foreach (var item in RealizedItems(view))
        {
            var root = Assert.IsType<Text>(item.ContentRoot);
            Assert.Equal(source[item.Index], root.Content);
            Assert.False(root.IsHitTestVisible);
        }
        foreach (var root in previous.Except(current))
            Assert.Equal(string.Empty, root.Content);
        manager.Close();
    }

    [Fact]
    [UIThreadTest(false)]
    public void DirectTextDataIsNotClearedOrReusedAsDefaultContent()
    {
        using var context = new TextTestContext();
        var businessRoot = new Text { Content = "business" };
        var source = new ObservableList<object>(Enumerable.Range(0, 1000).Select(index => (object)$"item {index}"));
        source[0] = businessRoot;
        var view = new ListView<object> { ItemsSource = source, ItemExtent = 24, Width = 240, Height = 144 };
        var (manager, _) = Open(view, new Size(240, 144));
        Assert.Same(businessRoot, FindContainer(view, 0).ContentRoot);
        view.ScrollIntoView(900);
        manager.UpdateFrame(new Size(240, 144), 0);
        Assert.Null(businessRoot.Parent);
        Assert.Equal("business", businessRoot.Content);
        Assert.DoesNotContain(RealizedItems(view), item => ReferenceEquals(item.ContentRoot, businessRoot));
        view.ScrollIntoView(0);
        manager.UpdateFrame(new Size(240, 144), 0);
        Assert.Same(businessRoot, FindContainer(view, 0).ContentRoot);
        Assert.Null(view.ItemTemplate);
        manager.Close();
    }

    [Fact]
    [UIThreadTest(false)]
    public void ExplicitOrdinaryTextTemplatesRemainBuildOnly()
    {
        using var context = new TextTestContext();
        var view = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 1000)),
            ItemExtent = 24, Width = 240, Height = 144
        };
        var (manager, _) = Open(view, new Size(240, 144));
        var defaults = RealizedItems(view).Select(item => Assert.IsType<Text>(item.ContentRoot)).ToArray();
        var builds = 0;
        view.ItemTemplate = new FuncDataTemplate<int>(value =>
        {
            builds++;
            return new Text { Content = $"custom {value}" };
        });
        manager.UpdateFrame(new Size(240, 144), 0);
        var custom = Assert.IsType<Text>(FindContainer(view, 0).ContentRoot);
        Assert.DoesNotContain(custom, defaults);
        var initialBuilds = builds;
        view.ScrollIntoView(900);
        manager.UpdateFrame(new Size(240, 144), 0);
        Assert.True(builds > initialBuilds);
        Assert.Null(custom.Parent);
        Assert.Equal("custom 0", custom.Content);
        view.ItemTemplate = null;
        manager.UpdateFrame(new Size(240, 144), 0);
        var root = Assert.IsType<Text>(FindContainer(view, 900).ContentRoot);
        Assert.Equal("900", root.Content);
        Assert.DoesNotContain(root, defaults);
        manager.Close();
    }

    private static IEnumerable<ItemContainer> RealizedItems(Container root)
    {
        foreach (var child in root.Children)
        {
            if (child is ItemContainer item)
                yield return item;
            else if (child is Container container)
                foreach (var descendant in RealizedItems(container))
                    yield return descendant;
        }
    }

    private static ItemContainer FindContainer(Container root, int index)
    {
        var children = new Stack<UIElement>();
        children.Push(root);
        while (children.Count > 0)
        {
            var child = children.Pop();
            if (child is ItemContainer item && item.Index == index)
                return item;
            if (child is Container container)
                foreach (var descendant in container.Children)
                    children.Push(descendant);
        }
        throw new InvalidOperationException("The requested item was not realized.");
    }

    private static (UIManager Manager, GameUIScene Scene) Open(UIElement root, Size size)
    {
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(size, 0);
        return (manager, scene);
    }

    private static Point ScenePosition(UIElement element)
    {
        var x = 0d;
        var y = 0d;
        for (UIElement? node = element; node is not null; node = node.Parent)
        {
            x += node.LayoutBounds.X;
            y += node.LayoutBounds.Y;
        }
        return new Point(x, y);
    }

    private sealed class RecordingTemplate : IRecyclingDataTemplate
    {
        internal Dictionary<int, UIElement> Active { get; } = [];
        internal HashSet<int> EmptyResults { get; } = [];
        internal int Builds { get; private set; }
        internal int Updates { get; private set; }
        internal int Clears { get; private set; }
        internal bool ReplaceOnUpdate { get; init; }
        internal bool EmptyOnUpdate { get; init; }
        internal Exception? ClearFailure { get; set; }

        public UIElement Build(object data)
        {
            Builds++;
            var root = new Panel();
            Active.Add((int)data, root);
            return root;
        }

        public UIElement? Update(object data, UIElement root)
        {
            Updates++;
            Assert.DoesNotContain(root, Active.Values);
            if (EmptyOnUpdate)
            {
                EmptyResults.Add((int)data);
                return null;
            }
            var newRoot = ReplaceOnUpdate ? new Panel() : root;
            Active.Add((int)data, newRoot);
            return newRoot;
        }

        public void Clear(object data, UIElement root)
        {
            if (ClearFailure is { } failure)
                throw failure;
            Assert.Same(root, Active[(int)data]);
            Active.Remove((int)data);
            Clears++;
        }
    }
}
