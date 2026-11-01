using PanguEngine.Client.UI;
using PanguEngine.Collections;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class ItemsViewInputTests
{
    [Fact]
    public void KeyboardNavigationKeepsControlMovementSeparateFromRangeSelection()
    {
        using var context = new TextTestContext();
        var view = CreateList();
        var (manager, _) = Open(view);
        Assert.True(view.Focus());
        manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Down, KeyModifiers.Control);
        Assert.Equal(new[] { 0 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(Key.Down, KeyModifiers.Shift);
        Assert.Equal(new[] { 0, 1, 2 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, true);
        Assert.Equal(new[] { 0, 1 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(Key.End, KeyModifiers.Control);
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Equal(new[] { 0, 1 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.True(view.Selection.IsSelected(99));
        manager.ProcessKeyDown(Key.Up, KeyModifiers.Control);
        manager.ProcessKeyDown(Key.Up, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal(Enumerable.Range(0, 98).Append(99), view.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    public void NoneModeKeepsPointerAndKeyboardNavigationWithoutSelecting()
    {
        using var context = new TextTestContext();
        var roots = new Dictionary<int, UIElement>();
        var view = CreateList();
        view.SelectionMode = SelectionMode.None;
        view.ItemTemplate = new FuncDataTemplate<int>(index =>
        {
            var root = new Panel();
            roots[index] = root;
            return root;
        });
        var (manager, _) = Open(view);
        Click(manager, Center(roots[2]), KeyModifiers.None);
        manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.A, KeyModifiers.Control);
        Assert.Empty(view.Selection.SelectedIndices);
        view.SelectionMode = SelectionMode.Single;
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.Equal(new[] { 3 }, view.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    public void SourceEditsRemapRealizedContentSelectionAndNavigationWithoutJumpingTheOffset()
    {
        using var context = new TextTestContext();
        var view = CreateList();
        var source = (ObservableList<int>)view.ItemsSource!;
        var template = new RecyclingInputTemplate();
        view.ItemTemplate = template;
        var (manager, _) = Open(view);
        Assert.True(view.Focus());
        manager.ProcessKeyDown(Key.End, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Up, KeyModifiers.Control);
        manager.UpdateFrame(new Size(240, 160), 0);
        var currentRoot = template.Active[98];
        var bar = Descendants(view).OfType<ScrollBar>().Single();
        var offset = bar.Value;

        source.Insert(0, -1);
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Equal(offset, bar.Value);
        Assert.Equal(new[] { 100 }, view.Selection.SelectedIndices);
        Assert.Equal(new[] { 99 }, view.Selection.SelectedItems);
        var current = Descendants(view).OfType<ItemContainer>().Single(item => item.Index == 99);
        Assert.Same(currentRoot, current.ContentRoot);
        Assert.True(current.HasPseudoClass(PseudoClass.Get("current")));

        source.Move(100, 98);
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Equal(offset, bar.Value);
        Assert.Equal(new[] { 98 }, view.Selection.SelectedIndices);
        Assert.Equal(new[] { 99 }, view.Selection.SelectedItems);
        current = Descendants(view).OfType<ItemContainer>().Single(item => item.Index == 100);
        Assert.Same(currentRoot, current.ContentRoot);
        Assert.True(current.HasPseudoClass(PseudoClass.Get("current")));
        manager.ProcessKeyDown(Key.Up, KeyModifiers.Shift);
        Assert.Equal(new[] { 98, 99 }, view.Selection.SelectedIndices);

        source.RemoveRange(0, 90);
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.True(bar.Value < offset);
        Assert.Equal(new[] { 8, 9 }, view.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    public void SingleModeShiftNavigationSelectsTheTargetInsteadOfTogglingItOff()
    {
        using var context = new TextTestContext();
        var view = CreateList();
        view.SelectionMode = SelectionMode.Single;
        var (manager, _) = Open(view);
        Assert.True(view.Focus());
        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Home, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal(new[] { 0 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(Key.Down, KeyModifiers.Control);
        Assert.Equal(new[] { 0 }, view.Selection.SelectedIndices);
        manager.Close();
    }

    [Theory]
    [InlineData(Orientation.Horizontal, Key.Right, Key.Down)]
    [InlineData(Orientation.Vertical, Key.Down, Key.Right)]
    public void GridNavigationRespectsVisualEdgesAndPartialLastLines(Orientation flow, Key crossKey, Key mainKey)
    {
        using var context = new TextTestContext();
        var view = new GridView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 5)),
            Flow = flow,
            ItemWidth = 100,
            ItemHeight = 100,
            Width = 240,
            Height = 240
        };
        var manager = new UIManager();
        manager.Open(new GameUIScene(view));
        manager.UpdateFrame(new Size(240, 240), 0);
        Assert.True(view.Focus());
        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
        manager.ProcessKeyDown(crossKey, KeyModifiers.None);
        manager.ProcessKeyDown(crossKey, KeyModifiers.None);
        Assert.Equal(new[] { 1 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(mainKey, KeyModifiers.None);
        Assert.Equal(new[] { 3 }, view.Selection.SelectedIndices);
        manager.ProcessKeyDown(mainKey, KeyModifiers.None);
        Assert.Equal(new[] { 4 }, view.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    public void PointerModifiersSelectLinearRangesButChildButtonsKeepTheirClicks()
    {
        using var context = new TextTestContext();
        var roots = new Dictionary<int, UIElement>();
        var clicks = 0;
        var view = CreateList();
        view.ItemTemplate = new FuncDataTemplate<int>(index =>
        {
            UIElement root = index == 3 ? new Button { Content = "action" } : new Panel();
            if (root is Button button)
                button.Click += (_, _) => clicks++;
            roots[index] = root;
            return root;
        });
        var (manager, _) = Open(view);
        Click(manager, Center(roots[0]), KeyModifiers.None);
        manager.UpdateFrame(new Size(240, 160), 0);
        Click(manager, Center(roots[2]), KeyModifiers.Control);
        manager.UpdateFrame(new Size(240, 160), 0);
        Click(manager, Center(roots[1]), KeyModifiers.Shift);
        Assert.Equal(new[] { 1, 2 }, view.Selection.SelectedIndices);
        manager.UpdateFrame(new Size(240, 160), 0);
        Click(manager, Center(roots[3]), KeyModifiers.None);
        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
        Assert.Equal(1, clicks);
        Assert.Equal(new[] { 1, 2 }, view.Selection.SelectedIndices);
        manager.Close();
    }

    [Fact]
    public void RecyclingAPressedRootDoesNotDeliverOldReleaseOrClickToItsNewItem()
    {
        using var context = new TextTestContext();
        var template = new RecyclingInputTemplate();
        var view = CreateList();
        view.ItemsSource = new ObservableList<int>([0, 1]);
        view.ItemTemplate = template;
        view.Spacing = 400;
        var (manager, _) = Open(view);
        var original = template.Active[0];
        manager.ProcessPointerPressed(Center(original), MouseButton.Left, KeyModifiers.None);
        view.ScrollIntoView(1);
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.DoesNotContain(0, template.Active.Keys);
        Assert.Same(original, template.Active[1]);
        manager.ProcessPointerReleased(Center(template.Active[1]), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, template.Releases);
        Assert.Equal(0, template.Clicks);
        manager.Close();
    }

    [Fact]
    public void WheelScrollingRecyclesOffscreenFocusedContentAndClearsFocus()
    {
        using var context = new TextTestContext();
        var roots = new Dictionary<int, Button>();
        var view = CreateList();
        view.ItemTemplate = new FuncDataTemplate<int>(index =>
        {
            var root = new Button();
            roots[index] = root;
            return root;
        });
        var (manager, scene) = Open(view);
        var original = roots[0];
        Assert.True(original.Focus());
        Assert.True(manager.ProcessPointerWheel(new Point(60, 60), 0, -50, KeyModifiers.None));
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Contains(roots.Keys, index => index > 20);
        Assert.Null(original.Parent);
        Assert.Null(scene.FocusedNode);
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AxisChangesReconnectBarValuesAndShiftWheelScrolling(bool grid)
    {
        using var context = new TextTestContext();
        var roots = new Dictionary<int, UIElement>();
        var source = new ObservableList<int>(Enumerable.Range(0, 1000));
        var template = new FuncDataTemplate<int>(index =>
        {
            var root = new Panel();
            roots[index] = root;
            return root;
        });
        Control view = grid
            ? new GridView<int>
            {
                ItemsSource = source, ItemTemplate = template,
                ItemWidth = 32, ItemHeight = 32
            }
            : new ListView<int>
            {
                ItemsSource = source, ItemTemplate = template, ItemExtent = 32
            };
        view.Width = 240;
        view.Height = 160;
        view.Padding = Thickness.Zero;
        view.BorderThickness = Thickness.Zero;
        var (manager, _) = Open(view);
        var verticalBar = Descendants(view).OfType<ScrollBar>()
            .Single(bar => bar.IsHitTestVisible && bar.Orientation == Orientation.Vertical);
        var before = Center(roots[0]);
        verticalBar.Value = 64;
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Equal(before.Y - 64, Center(roots[0]).Y, 8);
        if (view is GridView<int> tiles)
            tiles.Flow = Orientation.Vertical;
        else
            ((ListView<int>)view).Orientation = Orientation.Horizontal;
        manager.UpdateFrame(new Size(240, 160), 0);
        var horizontalBar = Descendants(view).OfType<ScrollBar>()
            .Single(bar => bar.IsHitTestVisible && bar.Orientation == Orientation.Horizontal);
        Assert.Equal(0, horizontalBar.Value);
        var horizontal = Center(roots[0]);
        horizontalBar.Value = 32;
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.Equal(horizontal.X - 32, Center(roots[0]).X, 8);
        var afterBar = Center(roots[0]);
        Assert.True(manager.ProcessPointerWheel(new Point(60, 60), 0, -1, KeyModifiers.Shift));
        manager.UpdateFrame(new Size(240, 160), 0);
        Assert.True(Center(roots[0]).X < afterBar.X);
        Assert.Equal(afterBar.Y, Center(roots[0]).Y, 8);
        manager.Close();
    }

    [Fact]
    public void WheelMovesTheOuterScrollViewAfterTheItemViewReachesItsBoundary()
    {
        using var context = new TextTestContext();
        var inner = new ListView<int>
        {
            ItemsSource = new ObservableList<int>(Enumerable.Range(0, 1000)),
            ItemTemplate = new FuncDataTemplate<int>(_ => new Panel()),
            ItemExtent = 32, Width = 200, Height = 160,
            Padding = Thickness.Zero, BorderThickness = Thickness.Zero
        };
        var content = new Canvas { Width = 240, Height = 1000 };
        content.Children.Add(inner);
        var outer = new ScrollView
        {
            Content = content, Width = 240, Height = 200,
            Padding = Thickness.Zero, BorderThickness = Thickness.Zero
        };
        var manager = new UIManager();
        manager.Open(new GameUIScene(outer));
        manager.UpdateFrame(new Size(240, 200), 0);
        var point = new Point(60, 60);
        Assert.True(manager.ProcessPointerWheel(point, 0, -1, KeyModifiers.None));
        Assert.Equal(0, outer.Offset.Y);
        manager.UpdateFrame(new Size(240, 200), 0);
        inner.ScrollIntoView(999);
        manager.UpdateFrame(new Size(240, 200), 0);
        Assert.True(manager.ProcessPointerWheel(point, 0, -1, KeyModifiers.None));
        Assert.True(outer.Offset.Y > 0);
        manager.Close();
    }

    private sealed class RecyclingInputTemplate : IRecyclingDataTemplate
    {
        internal Dictionary<int, UIElement> Active { get; } = [];
        internal int Releases { get; private set; }
        internal int Clicks { get; private set; }

        public UIElement Build(object data)
        {
            var root = new Panel();
            root.PointerReleased += (_, args) =>
            {
                Releases++;
                args.Handled = true;
            };
            root.PointerClicked += (_, _) => Clicks++;
            Active.Add((int)data, root);
            return root;
        }

        public UIElement Update(object data, UIElement root)
        {
            Active.Add((int)data, root);
            return root;
        }

        public void Clear(object data, UIElement root) => Active.Remove((int)data);
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        if (root is not Container parent)
            yield break;
        foreach (var child in parent.ReadOnlyChildren)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static ListView<int> CreateList() => new()
    {
        ItemsSource = new ObservableList<int>(Enumerable.Range(0, 100)),
        SelectionMode = SelectionMode.Multiple,
        Width = 240,
        Height = 160,
        ItemExtent = 32
    };

    private static (UIManager Manager, GameUIScene Scene) Open(UIElement root)
    {
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(240, 160), 0);
        return (manager, scene);
    }

    private static void Click(UIManager manager, Point point, KeyModifiers modifiers)
    {
        manager.ProcessPointerPressed(point, MouseButton.Left, modifiers);
        manager.ProcessPointerReleased(point, MouseButton.Left, modifiers);
    }

    private static Point Center(UIElement root)
    {
        var x = root.LayoutBounds.Width / 2;
        var y = root.LayoutBounds.Height / 2;
        for (UIElement? node = root; node is not null; node = node.Parent)
        {
            x += node.LayoutBounds.X;
            y += node.LayoutBounds.Y;
        }
        return new Point(x, y);
    }
}
