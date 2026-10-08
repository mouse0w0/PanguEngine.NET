using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class TabViewInputTests
{
    private static readonly UiPseudoClass DraggingPseudoClass = UiPseudoClass.Get("dragging");

    [Fact]
    public void ArrowKeysMoveFocusWithoutChangingSelection()
    {
        var (manager, screen, view, items) = OpenScene(3);
        Assert.True(items[0].Focus());

        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);

        Assert.Same(items[1], screen.FocusedNode);
        Assert.Same(items[0], view.Selection.SelectedItem);

        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Same(items[2], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Same(items[2], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Left, KeyModifiers.None);
        Assert.Same(items[1], screen.FocusedNode);
        Assert.Same(items[0], view.Selection.SelectedItem);
        manager.Close();
    }

    [Fact]
    public void HomeAndEndFocusTheFirstAndLastAvailableItem()
    {
        var (manager, screen, _, items) = OpenScene(3);
        Assert.True(items[1].Focus());

        manager.ProcessKeyDown(Key.End, KeyModifiers.None);
        Assert.Same(items[2], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
        Assert.Same(items[0], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void EnterAndSpaceActivateTheFocusedTab()
    {
        var (manager, screen, view, items) = OpenScene(3);
        Assert.True(items[1].Focus());

        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        Assert.Same(items[1], view.Selection.SelectedItem);
        Assert.Same(items[1], screen.FocusedNode);

        view.Selection.SelectedItem = items[0];
        Assert.True(items[2].Focus());

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        Assert.Same(items[2], view.Selection.SelectedItem);
        manager.Close();
    }

    [Fact]
    public void ControlTabCyclesSelectionAndFocus()
    {
        var (manager, screen, view, items) = OpenScene(3);
        Assert.True(items[0].Focus());

        manager.ProcessKeyDown(Key.Tab, KeyModifiers.Control);

        Assert.Same(items[1], view.Selection.SelectedItem);
        Assert.Same(items[1], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Same(items[0], view.Selection.SelectedItem);
        Assert.Same(items[0], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void ControlF4RequestsCloseOfTheSelectedClosableTabOnce()
    {
        var (manager, _, view, items) = OpenScene(2, closable: true);
        view.Selection.SelectedItem = items[1];
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(items[1].Focus());
        var requested = new List<TabItem>();
        view.TabCloseRequested += (_, args) => requested.Add(args.Item);

        manager.ProcessKeyDown(Key.F4, KeyModifiers.Control);

        Assert.Same(items[1], Assert.Single(requested));

        manager.ProcessKeyDown(Key.F4, KeyModifiers.Control, isRepeat: true);

        Assert.Single(requested);
        manager.Close();
    }

    [Fact]
    public void ControlF4IsIgnoredWhenTheSelectedTabIsNotClosable()
    {
        var (manager, _, view, items) = OpenScene(2);
        Assert.True(items[0].Focus());
        var count = 0;
        view.TabCloseRequested += (_, _) => count++;

        manager.ProcessKeyDown(Key.F4, KeyModifiers.Control);

        Assert.Equal(0, count);
        manager.Close();
    }

    [Fact]
    public void VerticalPlacementUsesUpAndDownKeys()
    {
        var (manager, screen, _, items) = OpenScene(3, placement: TabStripPlacement.Left, width: 400, height: 300);
        Assert.True(items[0].Focus());

        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Same(items[0], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
        Assert.Same(items[1], screen.FocusedNode);

        manager.ProcessKeyDown(Key.Up, KeyModifiers.None);
        Assert.Same(items[0], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void HandledChildKeyStopsViewShortcuts()
    {
        var (manager, screen, _, items) = OpenScene(3);
        Assert.True(items[0].Focus());
        items[0].KeyDown += (_, args) => args.Handled = true;

        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);

        Assert.Same(items[0], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void ShortcutsRequireFocusWithinTheView()
    {
        var root = new Canvas();
        var view = new TabView { Width = 600, Height = 100 };
        Canvas.SetLeft(view, 0);
        Canvas.SetTop(view, 0);
        root.Children.Add(view);
        var first = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        var second = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        view.Items.Add(first);
        view.Items.Add(second);
        var other = new TestControl { Focusable = true, Width = 20, Height = 20 };
        Canvas.SetLeft(other, 0);
        Canvas.SetTop(other, 200);
        root.Children.Add(other);
        var manager = new UiManager();
        var screen = new GameScreen(root);
        manager.Open(screen);
        manager.UpdateFrame(new Size(600, 300), 0);
        Assert.True(other.Focus());
        var selected = view.Selection.SelectedItem;

        manager.ProcessKeyDown(Key.Tab, KeyModifiers.Control);

        Assert.Same(selected, view.Selection.SelectedItem);
        Assert.Same(other, screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void RemovingTheFocusedSelectedItemMovesFocusToTheNextSelection()
    {
        var (manager, screen, view, items) = OpenScene(3);
        view.Selection.SelectedItem = items[1];
        Assert.True(items[1].Focus());

        view.Items.Remove(items[1]);
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(items[2], view.Selection.SelectedItem);
        Assert.Same(items[2], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void PointerClickSelectsTabWhenReorderingIsDisabled()
    {
        var (manager, _, view, items) = OpenScene(3);
        var target = ItemCenter(items[1]);

        manager.ProcessPointerPressed(target, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(target, MouseButton.Left, KeyModifiers.None);

        Assert.Same(items[1], view.Selection.SelectedItem);
        manager.Close();
    }

    [Fact]
    public void CloseButtonClickRequestsCloseWithoutSelecting()
    {
        var (manager, _, view, items) = OpenScene(2, closable: true);
        view.Selection.SelectedItem = items[1];
        manager.UpdateFrame(new Size(600, 100), 0);
        var close = Assert.Single(items[0].Children.OfType<TabCloseButton>());
        var target = CloseCenter(close);
        Assert.Same(close, manager.CurrentScreen!.HitTest(target));
        TabItem? requested = null;
        view.TabCloseRequested += (_, args) => requested = args.Item;

        manager.ProcessPointerPressed(target, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(target, MouseButton.Left, KeyModifiers.None);

        Assert.Same(items[0], requested);
        Assert.Same(items[1], view.Selection.SelectedItem);
        Assert.Equal(2, view.Items.Count);
        manager.Close();
    }

    [Fact]
    public void DragCommitsReorderingOnReleaseInsideTheStrip()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var reordered = new List<TabReorderedEventArgs>();
        view.TabReordered += (_, args) => reordered.Add(args);
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);

        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(items[0].IsDragging);
        Assert.True(items[0].HasPseudoClass(DraggingPseudoClass));
        manager.ProcessPointerReleased(end, MouseButton.Left, KeyModifiers.None);

        Assert.False(items[0].IsDragging);
        Assert.False(items[0].HasPseudoClass(DraggingPseudoClass));
        Assert.Same(items[1], view.Items[0]);
        Assert.Same(items[2], view.Items[1]);
        Assert.Same(items[0], view.Items[2]);
        var args = Assert.Single(reordered);
        Assert.Same(items[0], args.Item);
        Assert.Equal(0, args.OldIndex);
        Assert.Equal(2, args.NewIndex);
        manager.Close();
    }

    [Fact]
    public void EscapeCancelsAnActiveDragWithoutReordering()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var reorderCount = 0;
        view.TabReordered += (_, _) => reorderCount++;
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);
        Assert.True(items[0].IsDragging);

        manager.ProcessKeyDown(Key.Escape, KeyModifiers.None);

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[0]);
        Assert.Equal(0, reorderCount);
        manager.Close();
    }

    [Fact]
    public void ReleaseOutsideTheStripCancelsTheDrag()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var reorderCount = 0;
        view.TabReordered += (_, _) => reorderCount++;
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);

        manager.ProcessPointerReleased(new Point(start.X + 400, 200), MouseButton.Left, KeyModifiers.None);

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[0]);
        Assert.Equal(0, reorderCount);
        manager.Close();
    }

    [Fact]
    public void FocusLossCommitsTheDragWithoutClickOrDuplicateReorder()
    {
        var (manager, screen, view, items) = OpenScene(3, canReorder: true);
        var reorderCount = 0;
        var clickCount = 0;
        view.TabReordered += (_, _) => reorderCount++;
        view.PointerClicked += (_, _) => clickCount++;
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);
        Assert.True(items[0].IsDragging);

        manager.ProcessFocusChanged(false);

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[2]);
        Assert.Equal(1, reorderCount);
        Assert.Equal(0, clickCount);
        Assert.Null(screen.FocusedNode);

        manager.ProcessFocusChanged(true);
        manager.ProcessPointerReleased(end, MouseButton.Left, KeyModifiers.None);
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Equal(1, reorderCount);
        Assert.Equal(0, clickCount);
        Assert.Null(screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void ClosingScreenCommitsDragWithoutClickOrDuplicateReorder()
    {
        var (manager, screen, view, items) = OpenScene(3, canReorder: true);
        var reorderCount = 0;
        var clickCount = 0;
        view.TabReordered += (_, _) => reorderCount++;
        view.PointerClicked += (_, _) => clickCount++;
        var start = ItemCenter(items[0]);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(start.X + 400, start.Y));
        Assert.True(items[0].IsDragging);

        manager.Close();

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[2]);
        Assert.Equal(1, reorderCount);
        Assert.Equal(0, clickCount);

        manager.Open(screen);
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.False(items[0].IsDragging);
        Assert.Equal(1, reorderCount);
        manager.Close();
        Assert.Equal(1, reorderCount);
        Assert.Equal(0, clickCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputLossClearsReplacementFocusWithoutRestoringItAfterLayout(bool closeScreen)
    {
        var (manager, screen, view, items) = OpenScene(2);
        var content = new TestControl { Focusable = true, Width = 40, Height = 20 };
        items[0].Content = content;
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(content.Focus());
        var replacement = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
        view.Items[0] = replacement;
        Assert.False(replacement.IsArrangeValid);
        Assert.Same(replacement, screen.FocusedNode);

        if (closeScreen)
        {
            manager.Close();
            manager.Open(screen);
        }
        else
        {
            manager.ProcessFocusChanged(false);
            manager.ProcessFocusChanged(true);
        }
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(replacement, view.Selection.SelectedItem);
        Assert.Null(screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void DisablingReorderingCancelsTheDrag()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);
        Assert.True(items[0].IsDragging);

        view.CanReorderTabs = false;

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[0]);
        manager.Close();
    }

    [Fact]
    public void ClickAfterAnUnchangedDragIsSuppressed()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        view.Selection.SelectedItem = items[0];
        var reorderCount = 0;
        view.TabReordered += (_, _) => reorderCount++;
        var start = ItemCenter(items[1]);
        var moved = new Point(start.X + 20, start.Y);

        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(moved);
        Assert.True(items[1].IsDragging);
        manager.ProcessPointerReleased(moved, MouseButton.Left, KeyModifiers.None);

        Assert.Same(items[0], view.Selection.SelectedItem);
        Assert.Same(items[1], view.Items[1]);
        Assert.Equal(0, reorderCount);
        manager.Close();
    }

    [Fact]
    public void FocusTransfersToTheNewItemWhenTheOldContentWasFocused()
    {
        var (manager, screen, view, items) = OpenScene(2);
        var content = new Panel();
        var child = new TestControl { Focusable = true, Width = 20, Height = 20 };
        content.Children.Add(child);
        items[0].Content = content;
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(child.Focus());
        Assert.Same(child, screen.FocusedNode);

        view.Selection.SelectedItem = items[1];

        Assert.Same(items[1], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void AutoScrollAdvancesWhileDraggingAtTheEdge()
    {
        var (manager, _, view, items) = OpenScene(4, canReorder: true, width: 300, height: 100);
        var strip = Assert.IsType<TabStripPanel>(view.Children[0]);
        var viewport = strip.Viewport;
        var start = ItemCenter(items[0]);
        var edge = viewport.LocalToScreen(new Point(viewport.LayoutBounds.Width - 2, viewport.LayoutBounds.Height / 2));
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(edge);
        var before = viewport.ScrollOffset;
        manager.UpdateFrame(new Size(300, 100), 0, TimeSpan.FromSeconds(1));
        Assert.Equal(before, viewport.ScrollOffset, 3);

        for (var frame = 1; frame <= 3; frame++)
        {
            manager.UpdateFrame(new Size(300, 100), 0, TimeSpan.FromSeconds(1 + frame * 0.03));
        }

        var scrolled = viewport.ScrollOffset;
        manager.ProcessPointerReleased(new Point(260, 200), MouseButton.Left, KeyModifiers.None);
        var settled = viewport.ScrollOffset;
        manager.UpdateFrame(new Size(300, 100), 0, TimeSpan.FromSeconds(2));

        Assert.True(viewport.HasOverflow);
        Assert.Equal(before + TabView.AutoScrollSpeed * 0.09, scrolled, 3);
        Assert.Equal(settled, viewport.ScrollOffset, 3);
        manager.Close();
    }

    [Fact]
    public void DragReorderedNotificationRejectsCollectionReentrancy()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        Exception? error = null;
        view.TabReordered += (_, _) => error = Record.Exception(() => view.Items.Clear());
        var start = ItemCenter(items[0]);
        var end = new Point(start.X + 400, start.Y);

        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(end);
        manager.UpdateFrame(new Size(600, 100), 0);
        manager.ProcessPointerReleased(end, MouseButton.Left, KeyModifiers.None);

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(3, view.Items.Count);
        manager.Close();
    }

    [Fact]
    public void FocusLossCancelsDragCandidateBeforeThreshold()
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var start = ItemCenter(items[0]);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerMoved(new Point(start.X + 100, start.Y));

        Assert.False(items[0].IsDragging);
        Assert.Same(items[0], view.Items[0]);
        manager.Close();
    }

    [Fact]
    public void InteractiveHeaderDoesNotActivateItsTabWithoutHandlingClick()
    {
        var (manager, screen, view, items) = OpenScene(2, canReorder: true);
        var header = new TestControl { Focusable = true, Width = 40, Height = 20 };
        items[1].Header = header;
        manager.UpdateFrame(new Size(600, 100), 0);
        var point = header.LocalToScreen(new Point(10, 10));

        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);

        Assert.Same(header, screen.FocusedNode);
        Assert.Same(items[0], view.Selection.SelectedItem);
        Assert.False(items[1].IsDragging);
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapeDuringDragSuppressesTheFollowingClick(bool crossDragThreshold)
    {
        var (manager, _, view, items) = OpenScene(3, canReorder: true);
        var start = ItemCenter(items[1]);
        var moved = new Point(start.X + 10, start.Y);
        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        if (crossDragThreshold)
            manager.ProcessPointerMoved(moved);
        manager.ProcessKeyDown(Key.Escape, KeyModifiers.None);
        manager.UpdateFrame(new Size(600, 100), 0);
        manager.ProcessPointerReleased(start, MouseButton.Left, KeyModifiers.None);

        Assert.Same(items[0], view.Selection.SelectedItem);
        Assert.False(items[1].IsDragging);

        manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(start, MouseButton.Left, KeyModifiers.None);

        Assert.Same(items[1], view.Selection.SelectedItem);
        manager.Close();
    }

    [Fact]
    public void BringIntoViewUsesContentCoordinatesAfterScrolling()
    {
        var (manager, _, view, items) = OpenScene(6, width: 300);
        var viewport = Assert.IsType<TabStripPanel>(view.Children[0]).Viewport;
        view.Selection.SelectedItem = items[^1];
        manager.UpdateFrame(new Size(300, 100), 0);
        Assert.True(viewport.ScrollOffset > 0);

        view.Selection.SelectedItem = items[1];
        manager.UpdateFrame(new Size(300, 100), 0);

        Assert.True(items[1].LayoutBounds.X >= 0);
        Assert.True(items[1].LayoutBounds.X + items[1].LayoutBounds.Width <= viewport.ViewportLength);
        manager.Close();
    }

    [Fact]
    public void ReplacingItemWithFocusedContentTransfersFocusToTheNewSelection()
    {
        var (manager, screen, view, items) = OpenScene(2);
        var content = new TestControl { Focusable = true, Width = 40, Height = 20 };
        items[0].Content = content;
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(content.Focus());
        var replacement = new TabItem { Header = new Panel { Width = 40, Height = 20 } };

        view.Items[0] = replacement;
        Assert.False(replacement.IsArrangeValid);
        Assert.Same(replacement, screen.FocusedNode);
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(replacement, view.Selection.SelectedItem);
        Assert.Same(replacement, screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void ReplacementFocusDoesNotOverrideLaterFocusWhenLayoutCompletes()
    {
        var (manager, screen, view, items) = OpenScene(2);
        var content = new TestControl { Focusable = true, Width = 40, Height = 20 };
        items[0].Content = content;
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(content.Focus());
        var replacement = new TabItem { Header = new Panel { Width = 40, Height = 20 } };

        view.Items[0] = replacement;
        Assert.False(replacement.IsArrangeValid);
        Assert.Same(replacement, screen.FocusedNode);
        Assert.True(items[1].Focus());
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(items[1], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void RemovingItemWithFocusedContentMovesFocusToTheNextSelection()
    {
        var (manager, screen, view, items) = OpenScene(3);
        var content = new TestControl { Focusable = true, Width = 40, Height = 20 };
        items[1].Content = content;
        view.Selection.SelectedItem = items[1];
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(content.Focus());
        Assert.Same(content, screen.FocusedNode);

        view.Items.Remove(items[1]);
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(items[2], view.Selection.SelectedItem);
        Assert.Same(items[2], screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void MigratingItemWithFocusedContentMovesFocusToTheNextSelection()
    {
        var (manager, screen, view, items) = OpenScene(3);
        var content = new TestControl { Focusable = true, Width = 40, Height = 20 };
        var destination = new Panel();
        items[1].Content = content;
        view.Selection.SelectedItem = items[1];
        manager.UpdateFrame(new Size(600, 100), 0);
        Assert.True(content.Focus());
        Assert.Same(content, screen.FocusedNode);

        destination.Children.Add(items[1]);
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.Same(items[2], view.Selection.SelectedItem);
        Assert.Same(items[2], screen.FocusedNode);
        manager.Close();
    }

    private static (UiManager Manager, GameScreen Screen, TabView View, List<TabItem> Items) OpenScene(
        int itemCount,
        bool closable = false,
        bool canReorder = false,
        TabStripPlacement placement = TabStripPlacement.Top,
        double width = 600,
        double height = 100)
    {
        var view = new TabView
        {
            TabStripPlacement = placement,
            CanReorderTabs = canReorder
        };
        var items = new List<TabItem>();
        for (var index = 0; index < itemCount; index++)
        {
            var item = new TabItem
            {
                Header = new Panel { Width = 40, Height = 20 },
                IsClosable = closable
            };
            view.Items.Add(item);
            items.Add(item);
        }

        var manager = new UiManager();
        var screen = new GameScreen(view);
        manager.Open(screen);
        manager.UpdateFrame(new Size(width, height), 0);
        return (manager, screen, view, items);
    }

    private static Point ItemCenter(TabItem item)
    {
        var bounds = item.LayoutBounds;
        return item.LocalToScreen(new Point(bounds.Width / 2, bounds.Height / 2));
    }

    private static Point CloseCenter(TabCloseButton close)
    {
        var bounds = close.LayoutBounds;
        return close.LocalToScreen(new Point(bounds.Width / 2, bounds.Height / 2));
    }

    private sealed class TestControl : Control
    {
    }
}
