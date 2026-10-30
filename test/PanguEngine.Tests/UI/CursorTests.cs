using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;
using PanguEngine.Windowing;

namespace PanguEngine.Tests.UI;

public sealed class CursorTests
{
    [Fact]
    public void PendingPointerSynchronizationDoesNotOutliveTheFailedInputRoute()
    {
        var root = new Canvas();
        var first = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Hand };
        var second = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Crosshair };
        var third = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Wait };
        Canvas.SetLeft(second, 30);
        Canvas.SetLeft(third, 60);
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        var expected = new InvalidOperationException("pointer routing failed");
        first.PointerMoved += (_, _) =>
        {
            scene.SynchronizePointerPosition(new Point(35, 5));
            throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            scene.ProcessPointerMoved(new Point(5, 5))));
        scene.ProcessPointerMoved(new Point(65, 5));

        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        Assert.True(third.IsHovered);
        Assert.False(second.IsHovered);
        scene.Close();
    }

    [Fact]
    public void CssCursorNotificationCanCloseTheSceneAfterLayout()
    {
        var root = new Panel();
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { cursor: pointer; }")]);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));
        scene.PointerCursorChanged += (_, _) =>
        {
            if (scene.PointerCursor == CursorShape.Crosshair)
                scene.Close();
        };

        scene.SetStyleSheets([StyleSheet.Parse("Panel { cursor: crosshair; }")]);
        scene.PrepareFrame(new Size(100, 100));

        Assert.False(scene.IsOpen());
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
    }

    [Fact]
    public void ClosingFromADeepPointerExitClearsTheCursorWithAnEmptyHoverPath()
    {
        var root = new Canvas();
        var child = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Hand };
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));
        child.PointerExited += (_, _) => scene.Close();

        scene.ProcessPointerMoved(new Point(150, 150));

        Assert.False(scene.IsOpen());
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CursorNotificationRechecksTheTargetsHitTestEligibility(bool changeAncestor)
    {
        var root = new Canvas();
        var child = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Hand };
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));
        scene.PointerCursorChanged += (_, _) =>
        {
            if (scene.PointerCursor == CursorShape.Wait)
                (changeAncestor ? (UIElement)root : child).IsHitTestVisible = false;
        };

        child.Cursor = CursorShape.Wait;

        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
        scene.Close();
    }

    [Fact]
    public void StationaryPointerTracksInheritedOverridesAndClearedValues()
    {
        var root = new Canvas { Cursor = CursorShape.Hand };
        var child = new Panel { Width = 20, Height = 20 };
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(CursorShape.Hand, scene.PointerCursor);
        child.Cursor = CursorShape.Crosshair;
        root.Cursor = CursorShape.Wait;
        Assert.Equal(CursorShape.Crosshair, scene.PointerCursor);
        child.ClearValue(UIElement.CursorProperty);
        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        root.Cursor = CursorShape.IBeam;
        Assert.Equal(CursorShape.IBeam, scene.PointerCursor);

        scene.Close();
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
        child.Cursor = CursorShape.Hand;
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
    }

    [Fact]
    public void StylesAndLayoutRefreshTheCursorWithoutAnotherMouseMove()
    {
        var root = new Canvas { Cursor = CursorShape.Wait };
        var child = new Panel { Width = 20, Height = 20 };
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { cursor: pointer; }")]);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));
        Assert.Equal(CursorShape.Hand, scene.PointerCursor);

        scene.SetStyleSheets([StyleSheet.Parse("Panel { cursor: crosshair; }")]);
        scene.PrepareFrame(new Size(100, 100));
        Assert.Equal(CursorShape.Crosshair, scene.PointerCursor);

        Canvas.SetLeft(child, 30);
        scene.PrepareFrame(new Size(100, 100));
        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        child.Cursor = CursorShape.IBeam;
        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        scene.Close();
    }

    [Fact]
    public void DraggingAndDisabledSubtreesUseTheActualHoverTarget()
    {
        var root = new Canvas();
        var background = new Panel { Cursor = CursorShape.Crosshair, Width = 100, Height = 100 };
        var foreground = new Panel { Cursor = CursorShape.Hand, Width = 20, Height = 20 };
        root.Children.Add(background);
        root.Children.Add(foreground);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(CursorShape.Hand, scene.PointerCursor);

        scene.ProcessPointerMoved(new Point(30, 5));
        Assert.Equal(CursorShape.Crosshair, scene.PointerCursor);
        scene.ProcessPointerReleased(new Point(30, 5), MouseButton.Left, KeyModifiers.None);
        scene.ProcessPointerMoved(new Point(5, 5));
        foreground.IsEnabled = false;
        scene.PrepareFrame(new Size(100, 100));
        Assert.Equal(CursorShape.Crosshair, scene.PointerCursor);

        root.Children.Remove(background);
        background.Cursor = CursorShape.Wait;
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
        root.Cursor = CursorShape.Wait;
        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        scene.Root = null;
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
        scene.Close();
    }

    [Fact]
    public void CursorNotificationsPreserveReentrantChangesAndSuppressEqualValues()
    {
        var root = new Panel { Cursor = CursorShape.Hand };
        var scene = new UIScene(root);
        var notifications = 0;
        scene.PointerCursorChanged += (sender, _) =>
        {
            Assert.Same(scene, sender);
            notifications++;
            if (scene.PointerCursor == CursorShape.Hand)
                root.Cursor = CursorShape.Wait;
        };
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(CursorShape.Wait, scene.PointerCursor);
        var notificationsBeforeEqualUpdates = notifications;
        scene.ProcessPointerMoved(new Point(6, 5));
        root.Cursor = CursorShape.Wait;
        Assert.Equal(notificationsBeforeEqualUpdates, notifications);

        scene.PointerCursorChanged += (_, _) =>
        {
            if (scene.PointerCursor == CursorShape.Crosshair)
                scene.Close();
        };
        root.Cursor = CursorShape.Crosshair;
        Assert.False(scene.IsOpen());
        Assert.Equal(CursorShape.Arrow, scene.PointerCursor);
    }
}
