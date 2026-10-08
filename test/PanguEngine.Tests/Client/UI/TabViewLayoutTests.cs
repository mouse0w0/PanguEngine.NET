using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class TabViewLayoutTests
{
    [Fact]
    public void TopPlacesStripAboveTheContentArea()
    {
        var (view, strip, content, _) = CreateView(2, TabStripPlacement.Top);

        Layout(view, 400, 200);

        Assert.Equal(0d, strip.LayoutBounds.Y, 3);
        Assert.Equal(strip.LayoutBounds.Height, content.LayoutBounds.Y, 3);
        Assert.Equal(400d, strip.LayoutBounds.Width, 3);
        Assert.Equal(200d, content.LayoutBounds.Y + content.LayoutBounds.Height, 3);
    }

    [Fact]
    public void BottomPlacesStripBelowTheContentArea()
    {
        var (view, strip, content, _) = CreateView(2, TabStripPlacement.Bottom);

        Layout(view, 400, 200);

        Assert.Equal(0d, content.LayoutBounds.Y, 3);
        Assert.Equal(content.LayoutBounds.Height, strip.LayoutBounds.Y, 3);
        Assert.Equal(200d, strip.LayoutBounds.Y + strip.LayoutBounds.Height, 3);
    }

    [Fact]
    public void LeftPlacesStripBeforeTheContentArea()
    {
        var (view, strip, content, _) = CreateView(2, TabStripPlacement.Left);

        Layout(view, 400, 200);

        Assert.Equal(0d, strip.LayoutBounds.X, 3);
        Assert.Equal(strip.LayoutBounds.Width, content.LayoutBounds.X, 3);
        Assert.Equal(200d, strip.LayoutBounds.Width, 3);
        Assert.Equal(200d, content.LayoutBounds.Width, 3);
    }

    [Fact]
    public void RightPlacesStripAfterTheContentArea()
    {
        var (view, strip, content, _) = CreateView(2, TabStripPlacement.Right);

        Layout(view, 400, 200);

        Assert.Equal(0d, content.LayoutBounds.X, 3);
        Assert.Equal(content.LayoutBounds.Width, strip.LayoutBounds.X, 3);
        Assert.Equal(200d, content.LayoutBounds.Width, 3);
        Assert.Equal(200d, strip.LayoutBounds.Width, 3);
    }

    [Fact]
    public void HorizontalTabsUseMaximumWidthWhenSpaceIsAbundant()
    {
        var (view, strip, _, items) = CreateView(3);

        Layout(view, 900, 100);

        Assert.False(strip.Viewport.HasOverflow);
        Assert.All(items, item => Assert.Equal(240d, item.LayoutBounds.Width, 3));
        Assert.Equal(720d, items[2].LayoutBounds.X + items[2].LayoutBounds.Width, 3);
    }

    [Fact]
    public void HorizontalTabsDivideTheStripEvenlyWhenSpaceIsModerate()
    {
        var (view, _, _, items) = CreateView(3);

        Layout(view, 600, 100);

        Assert.All(items, item => Assert.Equal(200d, item.LayoutBounds.Width, 3));
        Assert.Equal(0d, items[0].LayoutBounds.X, 3);
        Assert.Equal(200d, items[1].LayoutBounds.X, 3);
        Assert.Equal(400d, items[2].LayoutBounds.X, 3);
    }

    [Fact]
    public void HorizontalTabsUseMinimumWidthAndShowScrollButtonsWhenSpaceIsTight()
    {
        var (view, strip, _, items) = CreateView(3);

        Layout(view, 200, 100);

        Assert.True(strip.Viewport.HasOverflow);
        Assert.All(items, item => Assert.Equal(96d, item.LayoutBounds.Width, 3));
        var buttons = ScrollButtons(strip);
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, button => Assert.True(button.LayoutBounds.Width > 0));
        Assert.All(buttons, button => Assert.True(button.LayoutBounds.Height > 0));
    }

    [Fact]
    public void SufficientSpaceHidesScrollButtons()
    {
        var (view, strip, _, _) = CreateView(3);
        var manager = new UiManager();
        manager.Open(new GameScreen(view));
        manager.UpdateFrame(new Size(600, 100), 0);

        Assert.False(strip.Viewport.HasOverflow);
        var buttons = ScrollButtons(strip);
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, button =>
        {
            Assert.Equal(Visibility.Hidden, button.Visibility);
            Assert.False(button.IsEnabled);
        });
        manager.Destroy();
    }

    [Theory]
    [InlineData(TabStripPlacement.Top)]
    [InlineData(TabStripPlacement.Bottom)]
    [InlineData(TabStripPlacement.Left)]
    [InlineData(TabStripPlacement.Right)]
    public void ScrollButtonStatesSettleWithLayoutStylesAndViewportChanges(TabStripPlacement placement)
    {
        var (view, strip, _, _) = CreateView(6, placement);
        var viewport = strip.Viewport;
        var buttons = ScrollButtons(strip);
        var screen = new UiScreen(view);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            ".tab-scroll-button { width: 24; height: 24; } " +
            ".tab-scroll-button:disabled { width: 32; height: 32; }")]);
        var smallSize = view.IsHorizontalPlacement ? new Size(200, 100) : new Size(400, 100);
        var largeSize = view.IsHorizontalPlacement ? new Size(1000, 100) : new Size(400, 1000);
        screen.Open();
        try
        {
            screen.PrepareFrame(smallSize);

            Assert.True(viewport.HasOverflow);
            Assert.All(buttons, button => Assert.Equal(Visibility.Visible, button.Visibility));
            Assert.False(buttons[0].IsEnabled);
            Assert.True(buttons[1].IsEnabled);
            Assert.Equal(32d, buttons[0].Width);
            Assert.Equal(24d, buttons[1].Width);

            screen.ProcessPointerMoved(buttons[1].LocalToScreen(new Point(
                buttons[1].LayoutBounds.Width / 2, buttons[1].LayoutBounds.Height / 2)));
            Assert.True(buttons[1].IsHovered);

            viewport.ScrollBy(viewport.GetMaxOffset());
            screen.PrepareFrame(smallSize);

            Assert.Equal(viewport.GetMaxOffset(), viewport.ScrollOffset, 3);
            Assert.True(buttons[0].IsEnabled);
            Assert.False(buttons[1].IsEnabled);
            Assert.False(buttons[1].IsHovered);
            Assert.Equal(24d, buttons[0].Width);
            Assert.Equal(32d, buttons[1].Width);

            screen.PrepareFrame(largeSize);

            Assert.False(viewport.HasOverflow);
            Assert.Equal(0d, viewport.ScrollOffset);
            Assert.All(buttons, button =>
            {
                Assert.Equal(Visibility.Hidden, button.Visibility);
                Assert.False(button.IsEnabled);
            });

            screen.PrepareFrame(smallSize);

            Assert.True(viewport.HasOverflow);
            Assert.False(buttons[0].IsEnabled);
            Assert.True(buttons[1].IsEnabled);
            Assert.All(buttons, button =>
            {
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.True(button.IsMeasureValid);
                Assert.True(button.IsArrangeValid);
            });
            Assert.True(view.IsStyleSubtreeValid);
            Assert.True(view.IsMeasureValid);
            Assert.True(view.IsArrangeValid);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void HiddenTabsKeepTheirSlotWhileCollapsedTabsDoNot()
    {
        var (view, _, _, items) = CreateView(3);
        items[1].Visibility = Visibility.Hidden;

        Layout(view, 600, 100);

        Assert.Equal(200d, items[0].LayoutBounds.Width, 3);
        Assert.Equal(200d, items[1].LayoutBounds.Width, 3);
        Assert.Equal(200d, items[2].LayoutBounds.Width, 3);
        Assert.Equal(400d, items[2].LayoutBounds.X, 3);

        items[1].Visibility = Visibility.Collapsed;

        Layout(view, 600, 100);

        Assert.Equal(240d, items[0].LayoutBounds.Width, 3);
        Assert.Equal(Rect.Zero, items[1].LayoutBounds);
        Assert.Equal(240d, items[2].LayoutBounds.Width, 3);
        Assert.Equal(240d, items[2].LayoutBounds.X, 3);
    }

    [Fact]
    public void VerticalStripUsesConfiguredWidthAndItemsFillTheStrip()
    {
        var (view, strip, content, items) = CreateView(2, TabStripPlacement.Left);

        Layout(view, 400, 300);

        Assert.Equal(200d, strip.LayoutBounds.Width, 3);
        Assert.All(items, item => Assert.Equal(200d, item.LayoutBounds.Width, 3));
        Assert.True(items[0].LayoutBounds.Height > 0);
        Assert.True(items[1].LayoutBounds.Y >= items[0].LayoutBounds.Y + items[0].LayoutBounds.Height);
        Assert.Equal(strip.LayoutBounds.Width, content.LayoutBounds.Width, 3);
    }

    [Fact]
    public void VerticalOverflowShowsScrollButtons()
    {
        var (view, strip, _, _) = CreateView(4, TabStripPlacement.Left);

        Layout(view, 400, 70);

        Assert.True(strip.Viewport.HasOverflow);
        var buttons = ScrollButtons(strip);
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, button => Assert.True(button.LayoutBounds.Height > 0));
        Assert.All(buttons, button => Assert.True(button.LayoutBounds.Width > 0));
    }

    [Fact]
    public void EmptyCollectionProducesTheAvailableSize()
    {
        var view = new TabView();

        Layout(view, 100, 100);

        Assert.Equal(100d, view.DesiredSize.Width, 3);
        Assert.Equal(100d, view.DesiredSize.Height, 3);
    }

    [Fact]
    public void InfiniteMainAxisMeasurementReturnsAFiniteDesiredSize()
    {
        var (view, _, _, items) = CreateView(3);

        view.Measure(new Size(double.PositiveInfinity, 50));

        Assert.True(double.IsFinite(view.DesiredSize.Width));
        Assert.True(double.IsFinite(view.DesiredSize.Height));
        Assert.Equal(720d, view.DesiredSize.Width, 3);
        Assert.All(items, item => Assert.True(double.IsFinite(item.DesiredSize.Width)));
    }

    [Fact]
    public void InfiniteCrossAxisMeasurementReturnsAFiniteVerticalStripWidth()
    {
        var (view, _, _, _) = CreateView(2, TabStripPlacement.Left);

        view.Measure(new Size(400, double.PositiveInfinity));

        Assert.True(double.IsFinite(view.DesiredSize.Height));
        Assert.True(double.IsFinite(view.DesiredSize.Width));
        Assert.Equal(400d, view.DesiredSize.Width, 3);
    }

    [Fact]
    public void InsertingItemsBeforeTheSelectedItemKeepsItVisible()
    {
        var (view, strip, _, items) = CreateView(6);
        var viewport = strip.Viewport;
        Layout(view, 300, 100);
        view.Selection.SelectedItem = items[^1];
        Layout(view, 300, 100);
        Assert.True(viewport.ScrollOffset > 0);

        for (var index = 0; index < 6; index++)
            view.Items.Insert(0, new TabItem { Header = new Panel { Width = 40, Height = 20 } });
        Layout(view, 300, 100);

        var selected = items[^1];
        Assert.Same(selected, view.Selection.SelectedItem);
        Assert.True(selected.LayoutBounds.X >= -0.001);
        Assert.True(selected.LayoutBounds.X + selected.LayoutBounds.Width <= viewport.ViewportLength + 0.001);
    }

    [Fact]
    public void RemovingItemsBeforeTheSelectedItemKeepsItVisible()
    {
        var (view, strip, _, items) = CreateView(6);
        var viewport = strip.Viewport;
        Layout(view, 300, 100);
        view.Selection.SelectedItem = items[3];
        Layout(view, 300, 100);

        view.Items.RemoveAt(0);
        Layout(view, 300, 100);

        var selected = items[3];
        Assert.Same(selected, view.Selection.SelectedItem);
        Assert.True(selected.LayoutBounds.X >= -0.001);
        Assert.True(selected.LayoutBounds.X + selected.LayoutBounds.Width <= viewport.ViewportLength + 0.001);
    }

    [Fact]
    public void ManualScrollIsPreservedAcrossRelayout()
    {
        var (view, strip, _, items) = CreateView(6);
        var viewport = strip.Viewport;
        Layout(view, 300, 100);
        view.Selection.SelectedItem = items[0];
        Layout(view, 300, 100);

        viewport.ScrollBy(200);
        var offset = viewport.ScrollOffset;
        Assert.Equal(200d, offset, 3);

        Layout(view, 300, 100);

        Assert.Equal(offset, viewport.ScrollOffset, 3);
    }

    [Fact]
    public void MovingItemsKeepsStripAndContentOrder()
    {
        var (view, strip, contentArea, items) = CreateView(3);

        view.Items.Move(0, 2);

        Assert.Same(items[1], view.Items[0]);
        Assert.Same(items[2], view.Items[1]);
        Assert.Same(items[0], view.Items[2]);
        var tabs = strip.Viewport.Children.OfType<TabItem>().ToList();
        Assert.Same(items[1], tabs[0]);
        Assert.Same(items[2], tabs[1]);
        Assert.Same(items[0], tabs[2]);
        Assert.Same(items[0].ContentHost, contentArea.Children[2]);
    }

    private static (TabView View, TabStripPanel Strip, TabContentArea Content, List<TabItem> Items) CreateView(
        int itemCount,
        TabStripPlacement placement = TabStripPlacement.Top)
    {
        var view = new TabView { TabStripPlacement = placement };
        var items = new List<TabItem>();
        for (var index = 0; index < itemCount; index++)
        {
            var item = new TabItem { Header = new Panel { Width = 40, Height = 20 } };
            view.Items.Add(item);
            items.Add(item);
        }

        var strip = Assert.IsType<TabStripPanel>(view.Children[0]);
        var content = Assert.IsType<TabContentArea>(view.Children[1]);
        return (view, strip, content, items);
    }

    private static List<TabScrollButton> ScrollButtons(TabStripPanel strip) =>
        strip.Children.OfType<TabScrollButton>().ToList();

    private static void Layout(TabView view, double width, double height)
    {
        for (var pass = 0; pass < 16; pass++)
        {
            view.UpdateStyles();
            view.Measure(new Size(width, height));
            if (!view.IsStyleSubtreeValid || !view.IsMeasureValid)
                continue;

            view.Arrange(new Rect(0, 0, width, height));
            if (view.IsStyleSubtreeValid && view.IsMeasureValid && view.IsArrangeValid)
                return;
        }

        Assert.True(view.IsStyleSubtreeValid && view.IsMeasureValid && view.IsArrangeValid,
            "Tab view layout did not stabilize.");
    }
}
