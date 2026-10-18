using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class FocusWithinTests
{
    [Fact]
    public void FocusWithinIsReadOnlyAndDefaultsToFalse()
    {
        var node = new Canvas();
        AssertFocusWithin(node, false);
        Assert.Throws<InvalidOperationException>(() => node.SetValue(Node.IsFocusWithinProperty, true));
        Assert.Throws<InvalidOperationException>(() =>
            node.Bind(Node.IsFocusWithinProperty, new Canvas(), Node.IsEnabledProperty));
        AssertFocusWithin(node, false);
    }

    [Fact]
    public void FocusWithinMatchesTheFocusedNodeAndAncestorsBeforeFocusEvents()
    {
        var root = new Canvas();
        var group = new Canvas { Focusable = true };
        var child = new Canvas { Focusable = true };
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        var gotFocusCalls = 0;
        child.GotFocus += (_, _) =>
        {
            gotFocusCalls++;
            Assert.Same(child, screen.FocusedNode);
            Assert.True(child.IsFocused);
            Assert.False(group.IsFocused);
            Assert.False(root.IsFocused);
            AssertFocusWithin(child, true);
            AssertFocusWithin(group, true);
            AssertFocusWithin(root, true);
        };
        screen.Open();
        try
        {
            AssertFocusWithin(root, false);
            Assert.True(group.Focus());
            AssertFocusWithin(group, true);
            AssertFocusWithin(child, false);

            Assert.True(child.Focus());
            Assert.True(child.Focus());

            Assert.Equal(1, gotFocusCalls);
            screen.ClearFocus();
            AssertFocusWithin(child, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(root, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void FocusWithinUpdatesGroupAndDescendantStylesAcrossFocusChanges()
    {
        var root = new Canvas();
        var group = new Canvas();
        var first = new Canvas { Focusable = true };
        var second = new Canvas { Focusable = true };
        var outside = new Canvas { Focusable = true };
        group.Classes.Add("group");
        first.Classes.Add("target");
        second.Classes.Add("target");
        root.Children.Add(group);
        root.Children.Add(outside);
        group.Children.Add(first);
        group.Children.Add(second);
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse(
            ".group:focus-within { opacity: 0.4; }" +
            ".group:focus-within > .target { opacity: 0.7; }")]);
        var internalTransitions = 0;
        first.LostFocus += (_, args) =>
        {
            if (!ReferenceEquals(args.NewFocus, second))
                return;
            internalTransitions++;
            AssertFocusWithin(group, true);
            AssertFocusWithin(root, true);
            AssertFocusWithin(first, false);
            AssertFocusWithin(second, true);
        };
        screen.Open();
        try
        {
            Assert.True(first.Focus());
            root.UpdateStyles();
            Assert.Equal(0.4, group.Opacity);
            Assert.Equal(0.7, first.Opacity);
            Assert.Equal(0.7, second.Opacity);

            Assert.True(second.Focus());
            root.UpdateStyles();
            Assert.Equal(1, internalTransitions);
            Assert.Equal(0.4, group.Opacity);

            Assert.True(outside.Focus());
            root.UpdateStyles();
            AssertFocusWithin(group, false);
            AssertFocusWithin(root, true);
            AssertFocusWithin(outside, true);
            Assert.Equal(1d, group.Opacity);
            Assert.Equal(1d, first.Opacity);
            Assert.Equal(1d, second.Opacity);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void RemovingFocusedBranchRefreshesFormerAncestorStyles()
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Classes.Add("outer");
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse(".outer:focus-within { opacity: 0.4; }")]);
        screen.Open();
        try
        {
            Assert.True(child.Focus());
            root.UpdateStyles();
            Assert.Equal(0.4, root.Opacity);

            Assert.True(root.Children.Remove(group));
            root.UpdateStyles();

            Assert.Equal(1d, root.Opacity);
            AssertFocusWithin(root, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(child, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("remove-child")]
    [InlineData("remove-group")]
    [InlineData("reparent")]
    [InlineData("other-screen")]
    [InlineData("transfer-root")]
    [InlineData("replace-root")]
    [InlineData("clear-root")]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("collapse")]
    [InlineData("window-focus")]
    [InlineData("close")]
    public void LosingFocusClearsTheOriginalAncestorPathBeforeLostFocus(string change)
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        var destination = new Canvas();
        root.Children.Add(group);
        root.Children.Add(destination);
        group.Children.Add(child);
        var screen = new Screen(root);
        var otherRoot = new Canvas();
        var otherScreen = new Screen(otherRoot);
        var lostFocusCalls = 0;
        child.LostFocus += (_, _) =>
        {
            lostFocusCalls++;
            Assert.Null(screen.FocusedNode);
            AssertFocusWithin(child, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(root, false);
        };
        screen.Open();
        otherScreen.Open();
        try
        {
            Assert.True(child.Focus());
            AssertFocusWithin(root, true);
            switch (change)
            {
                case "clear": screen.ClearFocus(); break;
                case "remove-child": group.Children.Remove(child); break;
                case "remove-group": root.Children.Remove(group); break;
                case "reparent": destination.Children.Add(group); break;
                case "other-screen": otherRoot.Children.Add(group); break;
                case "transfer-root": otherScreen.Root = root; break;
                case "replace-root": screen.Root = new Canvas(); break;
                case "clear-root": screen.Root = null; break;
                case "disable": group.IsEnabled = false; break;
                case "hide": group.Visibility = Visibility.Hidden; break;
                case "collapse": group.Visibility = Visibility.Collapsed; break;
                case "window-focus": screen.ProcessFocusChanged(false); break;
                case "close": screen.Close(); break;
            }

            Assert.Equal(1, lostFocusCalls);
            AssertFocusWithin(destination, false);
            AssertFocusWithin(otherRoot, false);
            Assert.False(child.IsFocused);
            Assert.Null(otherScreen.FocusedNode);
        }
        finally
        {
            screen.Close();
            otherScreen.Close();
        }
    }

    [Fact]
    public void MovingFocusedSubtreeToAnotherScreenPreservesDestinationFocusWithin()
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        var otherRoot = new Canvas();
        var otherChild = new Canvas { Focusable = true };
        otherRoot.Children.Add(otherChild);
        var otherScreen = new Screen(otherRoot);
        screen.Open();
        otherScreen.Open();
        try
        {
            Assert.True(child.Focus());
            Assert.True(otherChild.Focus());

            otherRoot.Children.Add(group);

            Assert.Null(screen.FocusedNode);
            AssertFocusWithin(root, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(child, false);
            Assert.Same(otherChild, otherScreen.FocusedNode);
            AssertFocusWithin(otherChild, true);
            AssertFocusWithin(otherRoot, true);
        }
        finally
        {
            screen.Close();
            otherScreen.Close();
        }
    }

    [Fact]
    public void FocusStateCallbackDisablingAncestorDoesNotRestoreFocusWithin()
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        using var subscription = child.Subscribe(Node.IsFocusedProperty, (_, args) =>
        {
            if (args.NewValue)
                group.IsEnabled = false;
        });
        screen.Open();
        try
        {
            Assert.True(child.Focus());

            Assert.Null(screen.FocusedNode);
            Assert.False(child.IsFocused);
            AssertFocusWithin(child, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(root, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void CommonAncestorsOnlyNotifyWhenEnteringAndLeavingTheirSubtree()
    {
        var root = new Canvas();
        var group = new Canvas();
        var first = new Canvas { Focusable = true };
        var second = new Canvas { Focusable = true };
        var outside = new Canvas { Focusable = true };
        root.Children.Add(group);
        root.Children.Add(outside);
        group.Children.Add(first);
        group.Children.Add(second);
        var values = new List<bool>();
        var propertyValues = new List<bool>();
        using var subscription = group.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            AssertFocusWithin(group, args.NewValue);
            values.Add(args.NewValue);
        });
        group.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, Node.IsFocusWithinProperty))
            {
                AssertFocusWithin(group, group.IsFocusWithin);
                propertyValues.Add(group.IsFocusWithin);
            }
        };
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(first.Focus());
            Assert.True(second.Focus());
            Assert.True(second.Focus());
            Assert.True(outside.Focus());
            Assert.Equal(new[] { true, false }, values);
            Assert.Equal(values, propertyValues);
            AssertFocusWithin(root, true);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void FocusWithinCanBeUsedAsARegisteredPropertyBindingSource()
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        var outside = new Canvas { Focusable = true };
        root.Children.Add(group);
        root.Children.Add(outside);
        group.Children.Add(child);
        var receiver = new Canvas();
        receiver.Bind(Node.IsHitTestVisibleProperty, group, Node.IsFocusWithinProperty);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.False(receiver.IsHitTestVisible);
            Assert.True(child.Focus());
            Assert.True(receiver.IsHitTestVisible);
            Assert.True(outside.Focus());
            Assert.False(receiver.IsHitTestVisible);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ReopeningAScreenDoesNotRestoreItsFormerFocusWithin()
    {
        var root = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(child);
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(child.Focus());
            screen.Close();
            AssertFocusWithin(root, false);
            AssertFocusWithin(child, false);
            screen.Open();
            Assert.Null(screen.FocusedNode);
            Assert.False(child.IsFocused);
            AssertFocusWithin(root, false);
            AssertFocusWithin(child, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("remove")]
    public void FocusWithinActivationCallbackCannotRestoreAnUnavailablePath(string change)
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        using var subscription = group.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            AssertFocusWithin(group, args.NewValue);
            if (!args.NewValue)
                return;
            switch (change)
            {
                case "disable": group.IsEnabled = false; break;
                case "hide": group.Visibility = Visibility.Hidden; break;
                case "remove": root.Children.Remove(group); break;
            }
        });
        screen.Open();
        try
        {
            Assert.True(child.Focus());
            Assert.Null(screen.FocusedNode);
            Assert.False(child.IsFocused);
            AssertFocusWithin(root, false);
            AssertFocusWithin(group, false);
            AssertFocusWithin(child, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ClearingOldLeafStillClearsOldAncestorsWhenItsCallbackDisablesTheNewTarget()
    {
        var root = new Canvas();
        var oldGroup = new Canvas();
        var newGroup = new Canvas();
        var oldLeaf = new Canvas { Focusable = true };
        var newLeaf = new Canvas { Focusable = true };
        root.Children.Add(oldGroup);
        root.Children.Add(newGroup);
        oldGroup.Children.Add(oldLeaf);
        newGroup.Children.Add(newLeaf);
        using var subscription = oldLeaf.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            if (!args.NewValue)
                newGroup.IsEnabled = false;
        });
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(oldLeaf.Focus());
            Assert.True(newLeaf.Focus());
            Assert.Null(screen.FocusedNode);
            Assert.False(oldLeaf.IsFocused);
            Assert.False(newLeaf.IsFocused);
            AssertFocusWithin(root, false);
            AssertFocusWithin(oldGroup, false);
            AssertFocusWithin(oldLeaf, false);
            AssertFocusWithin(newGroup, false);
            AssertFocusWithin(newLeaf, false);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void UnavailabilityCleanupRejectsFocusRequestsDuringRangeNotifications()
    {
        var root = new Canvas();
        var child = new Canvas { Focusable = true };
        var other = new Canvas { Focusable = true };
        root.Children.Add(child);
        root.Children.Add(other);
        var rejectedCalls = 0;
        using var subscription = child.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            if (!args.NewValue)
            {
                Assert.Throws<InvalidOperationException>(() => other.Focus());
                rejectedCalls++;
            }
        });
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.True(child.Focus());
            child.IsEnabled = false;
            Assert.Equal(1, rejectedCalls);
            Assert.Null(screen.FocusedNode);
            Assert.False(child.IsFocused);
            AssertFocusWithin(root, false);
            AssertFocusWithin(child, false);
            AssertFocusWithin(other, false);
            Assert.True(other.Focus());
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RangeCleanupPreservesFocusEstablishedByItsCallbackInAnotherScreen(bool cleanup)
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        var next = new Canvas { Focusable = true };
        root.Children.Add(group);
        root.Children.Add(next);
        group.Children.Add(child);
        var screen = new Screen(root);
        var otherRoot = new Canvas();
        var otherScreen = new Screen(otherRoot);
        var transferred = false;
        using var subscription = child.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            if (args.NewValue || transferred)
                return;
            transferred = true;
            otherRoot.Children.Add(group);
            Assert.True(child.Focus());
        });
        screen.Open();
        otherScreen.Open();
        try
        {
            Assert.True(child.Focus());
            if (cleanup)
                root.Children.Remove(group);
            else
                Assert.True(next.Focus());
            Assert.True(transferred);
            Assert.Same(child, otherScreen.FocusedNode);
            Assert.True(child.IsFocused);
            AssertFocusWithin(child, true);
            AssertFocusWithin(group, true);
            AssertFocusWithin(otherRoot, true);
            AssertFocusWithin(root, !cleanup);
            AssertFocusWithin(next, !cleanup);
            if (cleanup)
                Assert.Null(screen.FocusedNode);
            else
                Assert.Same(next, screen.FocusedNode);
        }
        finally
        {
            screen.Close();
            otherScreen.Close();
        }
    }

    [Fact]
    public void RangeActivationCanTransferFocusToAnotherScreen()
    {
        var root = new Canvas();
        var group = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(group);
        group.Children.Add(child);
        var screen = new Screen(root);
        var otherRoot = new Canvas();
        var otherScreen = new Screen(otherRoot);
        var transferred = false;
        using var subscription = group.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            if (!args.NewValue || transferred)
                return;
            transferred = true;
            otherRoot.Children.Add(group);
            Assert.True(child.Focus());
        });
        screen.Open();
        otherScreen.Open();
        try
        {
            Assert.True(child.Focus());
            Assert.True(transferred);
            Assert.Null(screen.FocusedNode);
            Assert.Same(child, otherScreen.FocusedNode);
            Assert.True(child.IsFocused);
            AssertFocusWithin(root, false);
            AssertFocusWithin(otherRoot, true);
            AssertFocusWithin(group, true);
            AssertFocusWithin(child, true);
        }
        finally
        {
            screen.Close();
            otherScreen.Close();
        }
    }

    [Fact]
    public void RangeNotificationExceptionsPropagateWithoutRollingBackCommittedState()
    {
        var root = new Canvas();
        var child = new Canvas { Focusable = true };
        root.Children.Add(child);
        var failure = new InvalidOperationException("range notification");
        using var subscription = child.Subscribe(Node.IsFocusWithinProperty, (_, args) =>
        {
            if (args.NewValue)
                throw failure;
        });
        var screen = new Screen(root);
        screen.Open();
        try
        {
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => child.Focus()));
            Assert.Same(child, screen.FocusedNode);
            AssertFocusWithin(child, true);
            AssertFocusWithin(root, true);
            screen.ClearFocus();
            AssertFocusWithin(child, false);
            AssertFocusWithin(root, false);
        }
        finally
        {
            screen.Close();
        }
    }

    private static void AssertFocusWithin(Node node, bool expected)
    {
        Assert.Equal(expected, node.IsFocusWithin);
        Assert.Equal(expected, node.HasPseudoClass(PseudoClass.FocusWithin));
    }
}
