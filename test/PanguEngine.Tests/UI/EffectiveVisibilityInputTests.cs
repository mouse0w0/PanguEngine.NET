using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class EffectiveVisibilityInputTests
{
    [Fact]
    [UIThreadTest]
    public void FocusCleanupPrecedesEffectiveVisibilityUpdates()
    {
        var root = new Panel();
        var child = new Panel { Focusable = true };
        root.Children.Add(child);
        var scene = new UIScene(root);
        var lostFocusCalls = 0;
        var effectiveCalls = 0;
        child.LostFocus += (_, _) =>
        {
            lostFocusCalls++;
            Assert.True(root.IsEffectivelyVisible);
            Assert.True(child.IsEffectivelyVisible);
        };
        using var subscription = root.Subscribe(UIElement.IsEffectivelyVisibleProperty, (_, args) =>
        {
            effectiveCalls++;
            if (!args.NewValue)
            {
                Assert.Equal(1, lostFocusCalls);
                Assert.Null(scene.FocusedNode);
                root.Visibility = Visibility.Visible;
            }
        });
        scene.Open();
        try
        {
            Assert.True(child.Focus());

            root.Visibility = Visibility.Hidden;

            Assert.Equal(1, lostFocusCalls);
            Assert.True(child.IsEffectivelyVisible);
            Assert.False(child.IsFocused);
            Assert.Equal(2, effectiveCalls);
        }
        finally
        {
            scene.Close();
        }

        Assert.True(child.IsEffectivelyVisible);
        Assert.Equal(2, effectiveCalls);
    }

    [Fact]
    [UIThreadTest]
    public void RestoringVisibilityDuringFocusLossDoesNotCreateAnEffectiveTransition()
    {
        var root = new Panel();
        var child = new Panel { Focusable = true };
        root.Children.Add(child);
        var scene = new UIScene(root);
        var lostFocusCalls = 0;
        var effectiveCalls = 0;
        child.LostFocus += (_, _) =>
        {
            lostFocusCalls++;
            Assert.Equal(Visibility.Hidden, root.Visibility);
            Assert.True(root.IsEffectivelyVisible);
            root.Visibility = Visibility.Visible;
        };
        using var subscription = root.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => effectiveCalls++);
        scene.Open();
        try
        {
            Assert.True(child.Focus());

            root.Visibility = Visibility.Hidden;

            Assert.Equal(1, lostFocusCalls);
            Assert.Equal(0, effectiveCalls);
            Assert.True(child.IsEffectivelyVisible);
            Assert.False(child.IsFocused);
            Assert.Null(scene.FocusedNode);
        }
        finally
        {
            scene.Close();
        }
    }
}
