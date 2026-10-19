using PanguEngine.Client.UI;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class UISceneLayoutStabilityTests
{
    [Fact]
    public void HoverTextChangeSettlesBeforeDrawingInSameFrame()
    {
        using var context = new TextTestContext();
        var label = new Text { Content = "before", IsHitTestVisible = false };
        var root = new Panel { Background = new SolidColorBrush(12, 14, 18) };
        root.Children.Add(label);
        var entered = 0;
        root.PointerEntered += (_, _) =>
        {
            entered++;
            label.Content = "after hover";
        };
        var scene = new CountingScene(root);
        scene.Open();
        try
        {
            scene.ProcessPointerMoved(new Point(10, 10));
            var posts = 0;
            scene.Post(() => posts++);
            scene.UpdateFrame(0);
            scene.PrepareFrame(new Size(400, 300));

            Assert.Equal("after hover", label.Content);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
            Assert.Equal(1, scene.Updates);
            Assert.Equal(1, posts);
            Assert.Equal(1, entered);
            Assert.Contains(scene.CreateDrawCommandList(), command => command is FillRectangleCommand);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(15)]
    public void FiniteMeasureInvalidationSettles(int invalidations)
    {
        var root = new InvalidatingNode(invalidations);
        var scene = new CountingScene(root);
        scene.Open();
        try
        {
            scene.UpdateFrame(0);
            scene.PrepareFrame(new Size(400, 300));
            Assert.Equal(invalidations + 1, root.Measures);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
            Assert.Equal(1, scene.Updates);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void EndlessInvalidationThrowsAfterSixteenPasses()
    {
        var root = new InvalidatingNode(int.MaxValue);
        var scene = new CountingScene(root);
        scene.Open();
        try
        {
            scene.UpdateFrame(0);
            var error = Assert.Throws<InvalidOperationException>(() =>
                scene.PrepareFrame(new Size(400, 300)));
            Assert.Equal(16, root.Measures);
            Assert.Contains(nameof(CountingScene), error.Message);
            Assert.Contains("16", error.Message);
            Assert.Contains("Measure", error.Message);
            Assert.Equal(1, scene.Updates);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void HoverCanReplaceRootAndScaleBeforeFrameCompletes()
    {
        var root = new Panel();
        var replacement = new Panel();
        var scene = new CountingScene(root);
        root.PointerEntered += (_, _) =>
        {
            scene.Root = replacement;
            scene.Scale = 2;
        };
        scene.Open();
        try
        {
            scene.ProcessPointerMoved(new Point(10, 10));
            scene.UpdateFrame(0);
            scene.PrepareFrame(new Size(400, 300));
            Assert.Same(replacement, scene.Root);
            Assert.True(replacement.IsArrangeValid);
            Assert.Equal(200, replacement.LayoutBounds.Width);
            Assert.Equal(150, replacement.LayoutBounds.Height);
            Assert.True(replacement.IsHovered);
        }
        finally
        {
            scene.Close();
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
        var scene = new CountingScene(root) { Scale = 1 };
        scene.Open();
        try
        {
            scene.ProcessPointerMoved(new Point(10, 10));
            scene.UpdateFrame(0);
            var error = Assert.Throws<InvalidOperationException>(() =>
                scene.PrepareFrame(new Size(400, 300)));
            Assert.Equal(16, transitions);
            Assert.Contains("16", error.Message);
            Assert.Contains("Style", error.Message);
            Assert.False(target.IsHovered);
            Assert.False(root.IsStyleSubtreeValid);
            Assert.False(root.IsArrangeValid);
            Assert.Equal(1, scene.Updates);
        }
        finally
        {
            scene.Close();
        }
    }

    private sealed class CountingScene(UIElement root) : GameUIScene(root)
    {
        internal int Updates { get; private set; }

        protected override void OnFrameUpdate(double alpha) => Updates++;
    }

    private sealed class InvalidatingNode(int invalidations) : UIElement
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