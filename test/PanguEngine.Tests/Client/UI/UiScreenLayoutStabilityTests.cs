using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class UiScreenLayoutStabilityTests
{
    [Fact]
    public void HoverTextChangeSettlesBeforeDrawingInSameFrame()
    {
        using var context = new UiTextTestContext();
        var label = new Text { Content = "before", IsHitTestVisible = false };
        var root = new Panel { Background = new SolidColorBrush(12, 14, 18) };
        root.Children.Add(label);
        var entered = 0;
        root.PointerEntered += (_, _) =>
        {
            entered++;
            label.Content = "after hover";
        };
        var screen = new CountingScreen(root);
        screen.Open();
        try
        {
            screen.ProcessPointerMoved(new Point(10, 10));
            var posts = 0;
            screen.Post(() => posts++);
            screen.PrepareFrame(new Size(400, 300), 0);

            Assert.Equal("after hover", label.Content);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
            Assert.Equal(1, screen.Updates);
            Assert.Equal(1, posts);
            Assert.Equal(1, entered);
            Assert.Contains(screen.CreateDrawCommandList(), command => command is UiFillRectangleCommand);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(15)]
    public void FiniteMeasureInvalidationSettles(int invalidations)
    {
        var root = new InvalidatingNode(invalidations);
        var screen = new CountingScreen(root);
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(400, 300), 0);
            Assert.Equal(invalidations + 1, root.Measures);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
            Assert.Equal(1, screen.Updates);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void EndlessInvalidationThrowsAfterSixteenPasses()
    {
        var root = new InvalidatingNode(int.MaxValue);
        var screen = new CountingScreen(root);
        screen.Open();
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                screen.PrepareFrame(new Size(400, 300), 0));
            Assert.Equal(16, root.Measures);
            Assert.Contains(nameof(CountingScreen), error.Message);
            Assert.Contains("16", error.Message);
            Assert.Contains("Measure", error.Message);
            Assert.Equal(1, screen.Updates);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void HoverCanReplaceRootAndScaleBeforeFrameCompletes()
    {
        var root = new Panel();
        var replacement = new Panel();
        var screen = new CountingScreen(root);
        root.PointerEntered += (_, _) =>
        {
            screen.Root = replacement;
            screen.Scale = 2;
        };
        screen.Open();
        try
        {
            screen.ProcessPointerMoved(new Point(10, 10));
            screen.PrepareFrame(new Size(400, 300), 0);
            Assert.Same(replacement, screen.Root);
            Assert.True(replacement.IsArrangeValid);
            Assert.Equal(200, replacement.LayoutBounds.Width);
            Assert.Equal(150, replacement.LayoutBounds.Height);
            Assert.True(replacement.IsHovered);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void HoverDrivenPositionOscillationStopsAtLimit()
    {
        var root = new Canvas();
        var target = new Panel { Width = 40, Height = 30 };
        root.Children.Add(target);
        var transitions = 0;
        target.PointerEntered += (_, _) =>
        {
            transitions++;
            Canvas.SetLeft(target, 100);
        };
        target.PointerExited += (_, _) =>
        {
            transitions++;
            Canvas.SetLeft(target, 0);
        };
        var screen = new CountingScreen(root) { Scale = 1 };
        screen.Open();
        try
        {
            screen.ProcessPointerMoved(new Point(10, 10));
            var error = Assert.Throws<InvalidOperationException>(() =>
                screen.PrepareFrame(new Size(400, 300), 0));
            Assert.Equal(16, transitions);
            Assert.Contains("16", error.Message);
            Assert.Contains("Arrange", error.Message);
            Assert.False(target.IsHovered);
            Assert.False(root.IsArrangeValid);
            Assert.Equal(1, screen.Updates);
        }
        finally
        {
            screen.Close();
        }
    }

    private sealed class CountingScreen(UiNode root) : UiScreen(root)
    {
        internal int Updates { get; private set; }

        protected override void OnFrameUpdate(double alpha) => Updates++;
    }

    private sealed class InvalidatingNode(int invalidations) : UiNode
    {
        internal int Measures { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            if (Measures++ < invalidations)
                InvalidateMeasure();
            return new Size(40, 30);
        }
    }
}
