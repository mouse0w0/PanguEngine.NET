using PanguEngine.Collections;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TreeViewTests
{
    [Fact]
    public void ChildrenAreObtainedSynchronouslyOnceWithoutVisitingUnexpandedDescendants()
    {
        using var text = new TextTestContext();
        var grandchild = new Node("grandchild");
        var child = Branch("child", grandchild);
        var root = Branch("root", child);
        var calls = new List<Node>();
        var tree = Create([root], node =>
        {
            calls.Add(node);
            return node.Children;
        });
        var scene = Open(tree);
        tree.Expand(root);
        tree.Selection.SelectOnly(child);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        tree.Expand(root);
        tree.Collapse(root);
        Frame(scene);
        tree.Expand(root);
        tree.ScrollIntoView(child);
        Frame(scene);
        tree.ItemTemplate = new FuncDataTemplate<Node>(node => new Text { Content = node.Name });
        Frame(scene);
        Assert.Same(root, Assert.Single(calls));
        tree.Expand(child);
        tree.Selection.SelectOnly(grandchild);
        Assert.Same(grandchild, Assert.Single(tree.Selection.SelectedItems));
        Assert.Collection(calls, item => Assert.Same(root, item), item => Assert.Same(child, item));
        scene.Close();
    }

    [Fact]
    public void ChildAccessorExceptionsPropagateBeforeExpansionIsCommitted()
    {
        using var text = new TextTestContext();
        var root = new Node("root") { MayHaveChildren = true };
        var error = new InvalidOperationException("access failed");
        var tree = Create([root], _ => throw error);
        var notifications = 0;
        tree.ExpansionChanged += (_, _) => notifications++;
        Assert.Same(error, Record.Exception(() => tree.Expand(root)));
        Assert.False(tree.IsExpanded(root));
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void RefreshingAnUnvisitedNodeReevaluatesItsHintWithoutObtainingChildren()
    {
        using var text = new TextTestContext();
        var root = new Node("root");
        var calls = 0;
        var tree = Create([root], node =>
        {
            calls++;
            return node.Children;
        });
        tree.Expand(root);
        root.Children.Add(new Node("child"));
        tree.Expand(root);
        Assert.Equal(0, calls);
        tree.RefreshNode(root);
        Assert.Equal(0, calls);
        tree.Expand(root);
        Assert.True(tree.IsExpanded(root));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AnInitiallyEmptyChildCollectionRemainsLiveWithoutImplicitExpansion()
    {
        using var text = new TextTestContext();
        var root = new Node("root") { MayHaveChildren = true };
        var calls = 0;
        var tree = Create([root], node =>
        {
            calls++;
            return node.Children;
        });
        var changes = new List<bool>();
        tree.ExpansionChanged += (_, args) => changes.Add(args.IsExpanded);
        tree.Expand(root);
        Assert.False(tree.IsExpanded(root));
        Assert.Empty(changes);
        var child = new Node("child");
        root.Children.Add(child);
        tree.Selection.SelectOnly(child);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        Assert.False(tree.IsExpanded(root));
        tree.Expand(root);
        root.Children.Clear();
        Assert.Empty(tree.Selection.SelectedItems);
        Assert.Equal(new[] { true, false }, changes);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReaddedNodeObtainsItsCurrentCollectionAndDetachesTheOldCollection(bool replaceSource)
    {
        using var text = new TextTestContext();
        var root = new Node("root") { MayHaveChildren = true };
        var previousChild = new Node("previous");
        var previous = new ObservableList<Node>([previousChild]);
        var current = previous;
        var calls = 0;
        var tree = Create([root], _ =>
        {
            calls++;
            return current;
        });
        var scene = Open(tree);
        tree.Expand(root);
        tree.Selection.SelectOnly(previousChild);
        if (replaceSource)
            tree.ItemsSource = new ObservableList<Node>([root]);
        else
        {
            var roots = (ObservableList<Node>)tree.ItemsSource!;
            roots.RemoveAt(0);
            roots.Add(root);
        }

        Assert.Empty(tree.Selection.SelectedItems);
        var child = new Node("current");
        current = new ObservableList<Node>([child]);
        tree.Expand(root);
        tree.Selection.SelectOnly(child);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        var obsoleteChild = new Node("obsolete");
        previous.Add(obsoleteChild);
        tree.Selection.SelectOnly(obsoleteChild);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        Assert.Equal(2, calls);
        scene.Close();
    }

    [Fact]
    public void RemovingHiddenChildrenCommitsSelectionOnceAndNormalizesTheEmptyParent()
    {
        using var text = new TextTestContext();
        var a = new Node("same");
        var b = new Node("same");
        var root = Branch("root", a, b);
        var tree = Create([root]);
        tree.SelectionMode = SelectionMode.Multiple;
        var scene = Open(tree);
        tree.Expand(root);
        Frame(scene);
        tree.Selection.Select(a);
        tree.Selection.Select(b);
        Assert.Equal(2, tree.Selection.SelectedItems.Count);
        tree.Collapse(root);
        var changes = 0;
        tree.Selection.SelectedItems.Changed += (_, _) => changes++;
        root.Children.Clear();
        Assert.Empty(tree.Selection.SelectedItems);
        Assert.Equal(1, changes);
        tree.Expand(root);
        Assert.False(tree.IsExpanded(root));
        root.Children.Add(new Node("later"));
        Assert.False(tree.IsExpanded(root));
        tree.Expand(root);
        Assert.True(tree.IsExpanded(root));
        scene.Close();
    }

    [Fact]
    public void ReplacingTheSameBusinessObjectEndsItsOldSelectionAssociation()
    {
        using var text = new TextTestContext();
        var node = new Node("node");
        var tree = Create([node]);
        var scene = Open(tree);
        tree.Selection.SelectOnly(node);
        var notifications = 0;
        tree.Selection.SelectedItems.Changed += (_, _) => notifications++;
        ((ObservableList<Node>)tree.ItemsSource!)[0] = node;
        Assert.Empty(tree.Selection.SelectedItems);
        Assert.Equal(1, notifications);
        scene.Close();
    }

    [Fact]
    public void NaturalWidthsGrowWithoutShrinkingWhileTheRealizedWindowRemainsBounded()
    {
        using var text = new TextTestContext();
        var nodes = Enumerable.Range(0, 2000).Select(index => new Node($"node {index}")).ToArray();
        nodes[0].Children.Add(new Node("nested"));
        nodes[^1].ContentWidth = 700;
        var template = new RecordingTemplate();
        var tree = Create(nodes);
        tree.Width = double.NaN;
        tree.HorizontalAlignment = HorizontalAlignment.Left;
        tree.ItemTemplate = template;
        var scene = Open(tree);
        Assert.True(tree.LayoutBounds.Width > 0);
        Assert.InRange(template.Active.Count, 1, 30);
        Assert.True(tree.Focus());
        scene.ProcessKeyDown(Key.Home, KeyModifiers.None);
        tree.Expand(nodes[0]);
        Frame(scene);
        tree.ScrollIntoView(nodes[^1]);
        Frame(scene);
        Assert.Contains(nodes[^1], template.Active.Keys);
        Assert.InRange(template.Active.Count, 1, 30);
        Assert.True(template.Clears > 0);
        Assert.True(template.Updates > 0);
        foreach (var row in Descendants(tree).OfType<TreeItemContainer>())
        {
            Assert.False(row.HasPseudoClass(PseudoClass.Selected));
            Assert.False(row.HasPseudoClass(PseudoClass.Current));
            Assert.False(row.HasPseudoClass(PseudoClass.Expanded));
        }

        var horizontal = Descendants(tree).OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Horizontal);
        Assert.True(horizontal.Maximum > 0);
        var maximum = horizontal.Maximum;
        tree.ScrollIntoView(nodes[0]);
        Frame(scene);
        Assert.Equal(maximum, horizontal.Maximum);
        tree.Selection.Clear();
        Frame(scene);
        var builds = template.Builds;
        var clears = template.Clears;
        var updates = template.Updates;
        var retainedRoot = template.Active[nodes[0]];
        tree.Selection.SelectOnly(nodes[0]);
        Frame(scene);
        Assert.Equal(builds, template.Builds);
        Assert.Equal(clears, template.Clears);
        Assert.Equal(updates, template.Updates);
        Assert.Same(retainedRoot, template.Active[nodes[0]]);
        tree.FontSize += 2;
        Frame(scene);
        Assert.True(horizontal.Maximum < maximum);
        tree.ScrollIntoView(nodes[^1]);
        Frame(scene);
        tree.ScrollIntoView(nodes[0]);
        Frame(scene);
        tree.ItemTemplate = new RecordingTemplate();
        Frame(scene);
        Assert.True(horizontal.Maximum < maximum);
        scene.Close();
    }

    [Theory]
    [InlineData(0, 120, false)]
    [InlineData(8, 120, false)]
    [InlineData(120, 0, true)]
    [InlineData(120, 8, true)]
    public void ScrollBarsStayInsideViewportsSmallerThanTheirNominalThickness(double width, double height,
        bool establishWidth)
    {
        using var text = new TextTestContext();
        var tree = Create(Enumerable.Range(0, 100).Select(index => new Node($"node {index}") { ContentWidth = 400 }));
        tree.Width = width;
        tree.Height = establishWidth ? 120 : height;
        tree.Padding = Thickness.Zero;
        tree.BorderThickness = Thickness.Zero;
        tree.ItemTemplate = new RecordingTemplate();
        var scene = Open(tree);
        if (establishWidth)
        {
            tree.Height = height;
            Frame(scene);
            Assert.Contains(Descendants(tree).OfType<ScrollBar>(),
                bar => bar.Orientation == Orientation.Horizontal && bar.Visibility == Visibility.Visible);
        }

        foreach (var bar in Descendants(tree).OfType<ScrollBar>().Where(bar => bar.Visibility == Visibility.Visible))
        {
            Assert.InRange(bar.LayoutBounds.X, 0, tree.LayoutBounds.Width);
            Assert.InRange(bar.LayoutBounds.Y, 0, tree.LayoutBounds.Height);
            Assert.InRange(bar.LayoutBounds.X + bar.LayoutBounds.Width, 0, tree.LayoutBounds.Width);
            Assert.InRange(bar.LayoutBounds.Y + bar.LayoutBounds.Height, 0, tree.LayoutBounds.Height);
        }

        scene.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneScaleAndRoundingChangesResetTheObservedWidthHighWatermark(bool changeRounding)
    {
        using var text = new TextTestContext();
        var nodes = Enumerable.Range(0, 200).Select(index => new Node($"node {index}")).ToArray();
        nodes[^1].ContentWidth = 700;
        var tree = Create(nodes);
        tree.ItemTemplate = new RecordingTemplate();
        var scene = Open(tree);
        tree.ScrollIntoView(nodes[^1]);
        Frame(scene);
        var horizontal = Descendants(tree).OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Horizontal);
        var previous = horizontal.Maximum;
        Assert.True(previous > 0);
        tree.ScrollIntoView(nodes[0]);
        Frame(scene);
        Assert.Equal(previous, horizontal.Maximum);
        if (changeRounding)
            scene.UseLayoutRounding = false;
        else
            scene.Scale = 2;
        Frame(scene);
        Assert.True(horizontal.Maximum < previous);
        scene.Close();
    }

    [Fact]
    public void LocationRequestedBeforeFirstLayoutUsesTheFinalViewportWithoutSelecting()
    {
        using var text = new TextTestContext();
        var nodes = Enumerable.Range(0, 300).Select(index => new Node($"node {index}")).ToArray();
        var template = new RecordingTemplate();
        var tree = Create(nodes);
        tree.ItemTemplate = template;
        tree.ScrollIntoView(nodes[^1]);
        var scene = Open(tree);
        Assert.Contains(nodes[^1], template.Active.Keys);
        Assert.Empty(tree.Selection.SelectedItems);
        scene.Close();
    }

    [Fact]
    public void ReorderingChildrenPreservesSelectionAndDynamicEmptyingCollapsesTheParentOnce()
    {
        using var text = new TextTestContext();
        var a = new Node("a");
        var b = new Node("b");
        var root = Branch("root", a, b);
        var tree = Create([root]);
        var scene = Open(tree);
        tree.Expand(root);
        Frame(scene);
        tree.Selection.SelectOnly(a);
        root.Children.Reverse();
        Assert.Same(a, Assert.Single(tree.Selection.SelectedItems));
        var collapses = 0;
        tree.ExpansionChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Item, root) && !args.IsExpanded)
                collapses++;
        };
        root.Children.Clear();
        Assert.False(tree.IsExpanded(root));
        Assert.Equal(1, collapses);
        root.Children.Add(a);
        Assert.False(tree.IsExpanded(root));
        scene.Close();
    }

    internal static IEnumerable<UIElement> Descendants(UIElement element)
    {
        yield return element;
        if (element is Container container)
            foreach (var child in container.ReadOnlyChildren)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }

    private sealed class RecordingTemplate : IRecyclingDataTemplate
    {
        internal Dictionary<Node, UIElement> Active { get; } = new(ReferenceEqualityComparer.Instance);
        internal int Builds { get; private set; }
        internal int Updates { get; private set; }
        internal int Clears { get; private set; }

        public UIElement Build(object data)
        {
            Builds++;
            return Apply((Node)data, new Panel { Height = 16 });
        }

        public UIElement Update(object data, UIElement root)
        {
            Updates++;
            return Apply((Node)data, root);
        }

        public void Clear(object data, UIElement root)
        {
            Clears++;
            Active.Remove((Node)data);
        }

        private UIElement Apply(Node node, UIElement root)
        {
            root.Width = node.ContentWidth;
            Active[node] = root;
            return root;
        }
    }

    internal static TreeView<Node> Create(IEnumerable<Node> nodes,
        Func<Node, IReadOnlyObservableList<Node>>? childrenSelector = null) => new(
        node => node.MayHaveChildren || node.Children.Count > 0,
        childrenSelector ?? (node => node.Children))
    {
        ItemsSource = new ObservableList<Node>(nodes), Width = 240, Height = 120
    };

    internal static UIScene Open(TreeView<Node> tree)
    {
        var scene = new UIScene(tree);
        scene.Open();
        Frame(scene);
        return scene;
    }

    internal static void Frame(UIScene scene) => scene.PrepareFrame(new Size(300, 300));

    internal static Node Branch(string name, params Node[] children)
    {
        var node = new Node(name);
        foreach (var child in children)
            node.Children.Add(child);
        return node;
    }

    internal sealed class Node(string name)
    {
        internal string Name { get; } = name;
        internal ObservableList<Node> Children { get; } = new();
        internal bool MayHaveChildren { get; set; }
        internal double ContentWidth { get; set; } = 80;
        public override bool Equals(object? obj) => obj is Node other && other.Name == Name;
        public override int GetHashCode() => Name.GetHashCode();
        public override string ToString() => Name;
    }
}