using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class FocusTargetTests
{
    [Fact]
    public void DefaultFocusTargetIsTheRequestedNode()
    {
        var root = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(child);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(child.Focus());
            Assert.Same(child, screen.FocusedNode);
            Assert.True(child.IsFocused);
            Assert.True(child.IsFocusWithin);
            Assert.True(root.IsFocusWithin);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void DirectFocusResolvesOnceAndUsesTheActualTargetsAncestorPath()
    {
        var root = new Canvas();
        var requestedGroup = new Canvas();
        var targetGroup = new Canvas();
        var old = new Canvas { Focusable = true };
        var target = new Canvas { Focusable = true };
        var proxy = new FocusProxy(target) { Focusable = true };
        root.Children.Add(old);
        root.Children.Add(requestedGroup);
        root.Children.Add(targetGroup);
        requestedGroup.Children.Add(proxy);
        targetGroup.Children.Add(target);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(old.Focus());
            Assert.True(proxy.Focus());
            Assert.Equal(1, proxy.ResolveCalls);
            Assert.Same(target, screen.FocusedNode);
            Assert.True(target.IsFocused);
            Assert.False(proxy.IsFocused);
            Assert.False(old.IsFocused);
            Assert.False(proxy.IsFocusWithin);
            Assert.False(requestedGroup.IsFocusWithin);
            Assert.True(target.IsFocusWithin);
            Assert.True(targetGroup.IsFocusWithin);
            Assert.True(root.IsFocusWithin);
            Assert.False(requestedGroup.HasPseudoClass(PseudoClass.FocusWithin));
            Assert.True(targetGroup.HasPseudoClass(PseudoClass.FocusWithin));
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("collapsed")]
    [InlineData("disabled")]
    [InlineData("not-focusable")]
    [InlineData("ancestor-hidden")]
    [InlineData("foreign")]
    public void UnavailableOrForeignTargetsKeepTheExistingFocus(string state)
    {
        var root = new Canvas();
        var old = new Canvas { Focusable = true };
        var targetGroup = new Canvas();
        var target = new Canvas { Focusable = true };
        var proxy = new FocusProxy(target) { Focusable = true };
        root.Children.Add(old);
        root.Children.Add(proxy);
        root.Children.Add(targetGroup);
        targetGroup.Children.Add(target);
        var otherRoot = new Canvas();
        var screen = new Screen(root);
        var otherScreen = new Screen(otherRoot);
        switch (state)
        {
            case "hidden": target.Visibility = Visibility.Hidden; break;
            case "collapsed": target.Visibility = Visibility.Collapsed; break;
            case "disabled": target.IsEnabled = false; break;
            case "not-focusable": target.Focusable = false; break;
            case "ancestor-hidden": targetGroup.Visibility = Visibility.Hidden; break;
            case "foreign": otherRoot.Children.Add(targetGroup); break;
        }

        screen.Open();
        otherScreen.Open();
        try
        {
            Assert.True(old.Focus());
            Assert.False(proxy.Focus());
            Assert.Equal(1, proxy.ResolveCalls);
            Assert.Same(old, screen.FocusedNode);
            Assert.True(old.IsFocused);
            Assert.True(old.IsFocusWithin);
            Assert.False(target.IsFocused);
            Assert.False(target.IsFocusWithin);
            Assert.Null(otherScreen.FocusedNode);
        }
        finally
        {
            screen.Close();
            otherScreen.Close();
        }
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("not-focusable")]
    [InlineData("ancestor-hidden")]
    public void UnavailableRequestsDoNotResolveTheirTarget(string state)
    {
        var root = new Canvas();
        var group = new Canvas();
        var old = new Canvas { Focusable = true };
        var target = new Canvas { Focusable = true };
        var proxy = new FocusProxy(target) { Focusable = true };
        root.Children.Add(old);
        root.Children.Add(target);
        root.Children.Add(group);
        group.Children.Add(proxy);
        switch (state)
        {
            case "hidden": proxy.Visibility = Visibility.Hidden; break;
            case "disabled": proxy.IsEnabled = false; break;
            case "not-focusable": proxy.Focusable = false; break;
            case "ancestor-hidden": group.Visibility = Visibility.Hidden; break;
        }

        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(old.Focus());
            Assert.False(proxy.Focus());
            Assert.Equal(0, proxy.ResolveCalls);
            Assert.Same(old, screen.FocusedNode);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ReturnedTargetsAreNotResolvedRecursively()
    {
        var root = new Canvas();
        var leaf = new Canvas { Focusable = true };
        var inner = new FocusProxy(leaf) { Focusable = true };
        var outer = new FocusProxy(inner) { Focusable = true };
        root.Children.Add(outer);
        root.Children.Add(inner);
        root.Children.Add(leaf);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(outer.Focus());
            Assert.Same(inner, screen.FocusedNode);
            Assert.Equal(1, outer.ResolveCalls);
            Assert.Equal(0, inner.ResolveCalls);
            Assert.True(inner.IsFocused);
            Assert.False(leaf.IsFocused);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PointerFocusFallsBackToAnAncestorOrClearsFocusWhenATargetIsUnavailable(bool ancestorFocusable)
    {
        var root = new Canvas { Focusable = ancestorFocusable };
        var target = new Canvas { Focusable = true, Visibility = Visibility.Hidden };
        var proxy = new FocusProxy(target) { Focusable = true, Width = 80, Height = 40 };
        var old = new Canvas { Focusable = true, Width = 80, Height = 40 };
        Canvas.SetTop(old, 50);
        root.Children.Add(old);
        root.Children.Add(proxy);
        proxy.Children.Add(target);
        var screen = new GameScreen(root);
        var manager = new UiManager();
        manager.Open(screen);
        try
        {
            manager.UpdateFrame(new Size(100, 100), 0, TimeSpan.Zero);
            Assert.True(old.Focus());
            manager.ProcessPointerPressed(new Point(10, 10), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(1, proxy.ResolveCalls);
            Assert.False(old.IsFocused);
            Assert.False(old.IsFocusWithin);
            Assert.False(proxy.IsFocusWithin);
            Assert.Equal(ancestorFocusable, root.IsFocusWithin);
            if (ancestorFocusable)
                Assert.Same(root, screen.FocusedNode);
            else
                Assert.Null(screen.FocusedNode);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void TargetResolutionExceptionsPropagateWithoutChangingFocus()
    {
        var root = new Canvas();
        var old = new Canvas { Focusable = true };
        var failure = new InvalidOperationException("target resolution");
        var proxy = new ThrowingFocusProxy(failure) { Focusable = true };
        root.Children.Add(old);
        root.Children.Add(proxy);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(old.Focus());
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => proxy.Focus()));
            Assert.Same(old, screen.FocusedNode);
            Assert.True(old.IsFocused);
            Assert.True(old.IsFocusWithin);
            Assert.False(proxy.IsFocusWithin);
        }
        finally
        {
            screen.Close();
        }
    }

    private sealed class FocusProxy(UIElement target) : Control
    {
        internal int ResolveCalls { get; private set; }

        protected internal override UIElement GetFocusTarget()
        {
            ResolveCalls++;
            return target;
        }
    }

    private sealed class ThrowingFocusProxy(Exception failure) : Control
    {
        protected internal override UIElement GetFocusTarget() => throw failure;
    }
}