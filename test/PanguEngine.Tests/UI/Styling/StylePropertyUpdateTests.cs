using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

[Collection(UIToolkitCollection.Name)]
public sealed class StylePropertyUpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachPropertyNotifiesBeforeTheNextValueAndSourceAreApplied(bool removeStyles)
    {
        var node = new StyleNode();
        var scene = new UIScene(node);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([Sheet(1, "old")]);
        node.UpdateStyles();
        node.Changes.Clear();
        var observed = new List<Property>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property != StyleNode.FirstProperty && args.Property != StyleNode.SecondProperty)
                return;
            var property = (Property<int>)args.Property;
            var other = property == StyleNode.FirstProperty
                ? StyleNode.SecondProperty : StyleNode.FirstProperty;
            Assert.Equal(removeStyles ? 0 : 2, node.GetValue(property));
            if (removeStyles)
                Assert.Empty(node.GetStyleValueSources(property));
            else
                Assert.Equal("new", Assert.Single(node.GetStyleValueSources(property)).SheetSourceName);
            if (observed.Count == 0)
            {
                Assert.Equal(1, node.GetValue(other));
                Assert.Equal("old", Assert.Single(node.GetStyleValueSources(other)).SheetSourceName);
            }
            observed.Add(property);
        };

        scene.SetStyleSheets(removeStyles ? [] : [Sheet(2, "new")]);
        node.UpdateStyles();

        Assert.Equal(2, observed.Count);
        Assert.Equal(2, node.Changes.Count);
        Assert.All(node.Changes, change =>
            Assert.Equal((1, removeStyles ? 0 : 2), (change.Old, change.New)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EarlierNotificationCanSetALocalValueBeforeTheLaterStyleUpdate(int localValue)
    {
        var node = new StyleNode();
        var scene = new UIScene(node);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([Sheet(1, "style")]);
        var observed = new List<Property>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property != StyleNode.FirstProperty && args.Property != StyleNode.SecondProperty)
                return;
            observed.Add(args.Property);
            if (observed.Count == 1)
                node.SetValue(args.Property == StyleNode.FirstProperty
                    ? StyleNode.SecondProperty : StyleNode.FirstProperty, localValue);
        };

        node.UpdateStyles();

        Assert.Equal(2, observed.Count);
        Assert.Equal(2, node.Changes.Count);
        var localProperty = (Property<int>)observed[1];
        Assert.Equal((0, localValue), node.Changes[1]);
        Assert.Equal(localValue, node.GetValue(localProperty));
        Assert.True(Assert.Single(node.GetStyleValueSources(localProperty)).IsMaskedByLocalValue);
        node.ClearValue(localProperty);
        Assert.Equal(1, node.GetValue(localProperty));
        Assert.False(Assert.Single(node.GetStyleValueSources(localProperty)).IsMaskedByLocalValue);
    }

    [Fact]
    public void RootChangeFinishesTheCurrentSnapshotButSkipsMovedChildren()
    {
        var oldRoot = new Panel();
        var newRoot = new Panel();
        var group = new Panel();
        var node = new StyleNode();
        var child = new StyleNode();
        node.Children.Add(child);
        group.Children.Add(node);
        oldRoot.Children.Add(group);
        var oldScene = new UIScene(oldRoot);
        var newScene = new UIScene(newRoot);
        oldScene.SetBaseStyleSheets([]);
        newScene.SetBaseStyleSheets([]);
        oldScene.SetStyleSheets([Sheet(1, "old")]);
        newScene.SetStyleSheets([Sheet(2, "new")]);
        var moved = false;
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property != StyleNode.FirstProperty && args.Property != StyleNode.SecondProperty)
                return;
            if (!moved)
            {
                moved = true;
                newRoot.Children.Add(group);
            }
        };

        oldRoot.UpdateStyles();

        Assert.Same(newRoot, group.Parent);
        Assert.Equal(2, node.Changes.Count);
        Assert.All(node.Changes, change => Assert.Equal((0, 1), change));
        Assert.Empty(child.Changes);
        Assert.Equal(0, child.GetValue(StyleNode.FirstProperty));
        Assert.False(node.IsStyleValid);
        Assert.False(node.IsStyleSubtreeValid);

        newRoot.UpdateStyles();

        Assert.Equal(2, node.GetValue(StyleNode.FirstProperty));
        Assert.Equal(2, node.GetValue(StyleNode.SecondProperty));
        Assert.Equal(4, node.Changes.Count);
        Assert.Equal(2, child.GetValue(StyleNode.FirstProperty));
        Assert.True(node.IsStyleSubtreeValid);
    }

    private static StyleSheet Sheet(int value, string sourceName) => new([
        new StyleRule(StyleSelector.For<StyleNode>(), [
            StyleSetter.Create(StyleNode.FirstProperty, value),
            StyleSetter.Create(StyleNode.SecondProperty, value)
        ])
    ], sourceName);

    private sealed class StyleNode : Panel
    {
        internal static readonly Property<int> FirstProperty =
            Property.Register<StyleNode, int>("First",
                onChanged: static (node, oldValue, newValue) => node.Changes.Add((oldValue, newValue)));

        internal static readonly Property<int> SecondProperty =
            Property.Register<StyleNode, int>("Second",
                onChanged: static (node, oldValue, newValue) => node.Changes.Add((oldValue, newValue)));

        internal List<(int Old, int New)> Changes { get; } = [];
    }
}
