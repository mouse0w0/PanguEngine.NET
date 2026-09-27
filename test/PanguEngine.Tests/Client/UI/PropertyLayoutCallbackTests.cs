using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class PropertyLayoutCallbackTests
{
    [Fact]
    public void MeasureCallbackInvalidatesAncestorsBeforeNotifications()
    {
        var root = new Canvas();
        var child = new TestNode();
        root.Children.Add(child);
        Layout(root);
        var notified = false;
        using var subscription = child.Subscribe(UiNode.WidthProperty, (_, _) =>
        {
            Assert.False(child.IsMeasureValid);
            Assert.False(root.IsMeasureValid);
            Assert.False(root.IsArrangeValid);
            notified = true;
        });

        child.Width = 20;

        Assert.True(notified);
    }

    [Fact]
    public void AttachedCallbackInvalidatesTargetArrangementAndAncestors()
    {
        var root = new Canvas();
        var child = new TestNode();
        root.Children.Add(child);
        Layout(root);

        Canvas.SetLeft(child, 10);

        Assert.True(child.IsMeasureValid);
        Assert.True(root.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void EqualLayoutValueAndDrawingOnlyValuePreserveLayout()
    {
        var node = new TestNode { Width = 20 };
        Layout(node);

        node.Width = 20;
        node.Opacity = 0.5;

        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);
    }

    [Fact]
    public void StyledLayoutValueInvokesTheSameInvalidationCallback()
    {
        var node = new TestNode();
        var screen = new UiScreen(node);
        Layout(node);

        screen.SetStyleSheets([new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<TestNode>(), [UiStyleSetter.Create(UiNode.WidthProperty, 25d)])
        ])]);

        Assert.Equal(25, node.Width);
        Assert.False(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    private static void Layout(UiNode node)
    {
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 100, 100));
    }

    private sealed class TestNode : UiNode;
}
