using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Templates;
using static PanguEngine.Tests.UI.TreeViewTests;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TreeViewInputTests
{
    [Fact]
    public void VisibleRangesPreserveExistingSelectionOrderAndCollapseKeepsHiddenSelections()
    {
        using var text = new TextTestContext();
        var a = new Node("a");
        var b = new Node("b");
        var c = new Node("c");
        var root = Branch("root", a, b, c);
        var tree = Create([root]);
        tree.SelectionMode = SelectionMode.Multiple;
        var scene = Open(tree);
        tree.Expand(root);
        Frame(scene);
        Assert.True(tree.Focus());
        scene.ProcessKeyDown(Key.End, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Up, KeyModifiers.Shift);
        scene.ProcessKeyDown(Key.Up, KeyModifiers.Shift);
        Assert.Collection(tree.Selection.SelectedItems,
            item => Assert.Same(c, item), item => Assert.Same(b, item), item => Assert.Same(a, item));
        tree.Collapse(root);
        Assert.Equal(3, tree.Selection.SelectedItems.Count);
        scene.ProcessKeyDown(Key.A, KeyModifiers.Control);
        Assert.Equal(4, tree.Selection.SelectedItems.Count);
        tree.SelectionMode = SelectionMode.Single;
        Assert.Same(c, Assert.Single(tree.Selection.SelectedItems));
        scene.Close();
    }

    [Fact]
    public void CollapsingAnAncestorMovesHiddenNavigationAndRangeAnchorToVisibleNodes()
    {
        using var text = new TextTestContext();
        var root = Branch("root", new Node("a"), new Node("b"), new Node("c"));
        var sibling = new Node("sibling");
        var tree = Create([root, sibling]);
        tree.SelectionMode = SelectionMode.Multiple;
        var scene = Open(tree);
        tree.Expand(root);
        Frame(scene);
        Assert.True(tree.Focus());
        scene.ProcessKeyDown(Key.Home, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Up, KeyModifiers.Shift);
        scene.ProcessKeyDown(Key.Up, KeyModifiers.Shift);
        tree.Collapse(root);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.Shift);
        Assert.Collection(tree.Selection.SelectedItems,
            item => Assert.Same(root, item), item => Assert.Same(sibling, item));
        scene.Close();
    }

    [Fact]
    public void LeftAndRightNavigationSelectTargetsButExpansionDoesNotChangeSelection()
    {
        using var text = new TextTestContext();
        var child = new Node("child");
        var root = Branch("root", child);
        var tree = Create([root]);
        var scene = Open(tree);
        Assert.True(tree.Focus());
        scene.ProcessKeyDown(Key.Home, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Same(root, Assert.Single(tree.Selection.SelectedItems));
        Frame(scene);
        scene.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        scene.ProcessKeyDown(Key.Left, KeyModifiers.None);
        Assert.Same(root, Assert.Single(tree.Selection.SelectedItems));
        scene.ProcessKeyDown(Key.Left, KeyModifiers.None);
        Assert.False(tree.IsExpanded(root));
        Assert.Same(root, Assert.Single(tree.Selection.SelectedItems));
        scene.Close();
    }

    [Fact]
    public void BusinessTextBoxFocusDoesNotNavigateOrSelectTheTree()
    {
        using var text = new TextTestContext();
        var editor = new TextBox { Text = "editable" };
        var node = new Node("node");
        var tree = Create([node]);
        tree.ItemTemplate = new FuncDataTemplate<Node>(_ => editor);
        var scene = Open(tree);
        Assert.True(editor.Focus());
        scene.ProcessKeyDown(Key.Home, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Right, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.Empty(tree.Selection.SelectedItems);
        Assert.Same(editor, scene.FocusedNode);
        scene.Close();
    }

    [Fact]
    public void TheDisclosureButtonExpandsSynchronouslyWithoutChangingSelection()
    {
        using var text = new TextTestContext();
        var leaf = new Node("leaf");
        var child = new Node("child");
        var root = Branch("root", child);
        var tree = Create([root, leaf]);
        var scene = Open(tree);
        tree.Selection.SelectOnly(leaf);
        var row = Descendants(tree).OfType<TreeItemContainer>().Single(item => ReferenceEquals(item.Data, root));
        var disclosure = Descendants(row).OfType<Button>().Single(button => button.Classes.Contains("tree-disclosure"));
        Click(scene, disclosure);
        Assert.Same(leaf, Assert.Single(tree.Selection.SelectedItems));
        Assert.True(tree.IsExpanded(root));
        tree.Selection.SelectOnly(child);
        Assert.Same(child, Assert.Single(tree.Selection.SelectedItems));
        scene.Close();
    }

    [Fact]
    public void SpaceSelectionMakesTheCurrentNodeTheNextRangeAnchor()
    {
        using var text = new TextTestContext();
        var nodes = new[] { new Node("a"), new Node("b"), new Node("c"), new Node("d") };
        var tree = Create(nodes);
        tree.SelectionMode = SelectionMode.Multiple;
        var scene = Open(tree);
        Assert.True(tree.Focus());
        scene.ProcessKeyDown(Key.Home, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.Control);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.Control);
        scene.ProcessKeyDown(Key.Space, KeyModifiers.None);
        scene.ProcessKeyDown(Key.Down, KeyModifiers.Shift);
        Assert.Collection(tree.Selection.SelectedItems,
            item => Assert.Same(nodes[2], item), item => Assert.Same(nodes[3], item));
        scene.Close();
    }

    [Theory]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control, false)]
    public void SinglePointerSelectionLetsShiftChooseTheTargetAndControlAloneToggleIt(KeyModifiers modifiers, bool selected)
    {
        using var text = new TextTestContext();
        var node = new Node("node");
        var tree = Create([node]);
        Panel? content = null;
        tree.ItemTemplate = new FuncDataTemplate<Node>(_ => content = new Panel { Width = 60, Height = 16 });
        var scene = Open(tree);
        tree.Selection.SelectOnly(node);
        Frame(scene);
        Click(scene, content!, modifiers);
        Assert.Equal(selected, tree.Selection.IsSelected(node));
        scene.Close();
    }

    private static void Click(UIScene scene, UIElement target, KeyModifiers modifiers = KeyModifiers.None)
    {
        var x = target.LayoutBounds.Width / 2;
        var y = target.LayoutBounds.Height / 2;
        for (UIElement? node = target; node is not null; node = node.Parent)
        {
            x += node.LayoutBounds.X;
            y += node.LayoutBounds.Y;
        }
        var point = new Point(x, y);
        scene.ProcessPointerPressed(point, MouseButton.Left, modifiers);
        scene.ProcessPointerReleased(point, MouseButton.Left, modifiers);
    }
}
