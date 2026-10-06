using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class UiShowcaseTabsTests
{
    [Theory]
    [InlineData(TabStripPlacement.Left)]
    [InlineData(TabStripPlacement.Right)]
    public void VerticalScrollButtonsUpdateDirectionAndFillStripWidth(TabStripPlacement placement)
    {
        using var context = new UiTextTestContext();
        using var host = new Host();
        var strip = Assert.Single(host.View.Children.OfType<TabStripPanel>());
        var buttons = strip.Children.OfType<TabScrollButton>().ToArray();
        var arrows = buttons.Select(button => Assert.Single(
            button.Children.OfType<PanguEngine.Client.UI.Controls.Path>())).ToArray();

        host.View.TabStripPlacement = placement;
        host.Prepare();

        Assert.Contains("tab-scroll-arrow-up", (IReadOnlySet<string>)arrows[0].Classes);
        Assert.Contains("tab-scroll-arrow-down", (IReadOnlySet<string>)arrows[1].Classes);
        Assert.DoesNotContain("tab-scroll-arrow-left", (IReadOnlySet<string>)arrows[0].Classes);
        Assert.DoesNotContain("tab-scroll-arrow-right", (IReadOnlySet<string>)arrows[1].Classes);
        Assert.All(buttons, button =>
        {
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.Equal(strip.ContentBounds.Width, button.LayoutBounds.Width);
            Assert.Equal(12d, button.LayoutBounds.Height);
        });

        host.View.TabStripPlacement = TabStripPlacement.Top;
        host.Prepare();

        Assert.Contains("tab-scroll-arrow-left", (IReadOnlySet<string>)arrows[0].Classes);
        Assert.Contains("tab-scroll-arrow-right", (IReadOnlySet<string>)arrows[1].Classes);
        Assert.DoesNotContain("tab-scroll-arrow-up", (IReadOnlySet<string>)arrows[0].Classes);
        Assert.DoesNotContain("tab-scroll-arrow-down", (IReadOnlySet<string>)arrows[1].Classes);
        Assert.All(buttons, button =>
        {
            Assert.Equal(12d, button.LayoutBounds.Width);
            Assert.Equal(host.View.Items[0].LayoutBounds.Height, button.LayoutBounds.Height);
        });
    }

    [Fact]
    public void InitialTabsOverflowAndNewTabIsSelectedAndBroughtIntoView()
    {
        using var context = new UiTextTestContext();
        using var host = new Host();
        var view = host.View;
        Assert.Equal(12, view.Items.Count);
        Assert.Same(view.Items[0], view.Selection.SelectedItem);
        Assert.True(view.LayoutBounds.Width <= 720);
        Assert.Equal(160d, view.LayoutBounds.Height);
        var strip = Assert.Single(view.Children.OfType<TabStripPanel>());
        var viewport = Assert.Single(strip.Children.OfType<TabStripViewport>());
        Assert.Equal(0d, viewport.ScrollOffset);
        Assert.True(viewport.HasOverflow);
        var buttons = strip.Children.OfType<TabScrollButton>().ToArray();
        Assert.False(buttons[0].IsEnabled);
        Assert.True(buttons[1].IsEnabled);

        host.Click(UiShowcaseTabs.AddId);
        host.Prepare();

        Assert.Equal(13, view.Items.Count);
        Assert.Same(view.Items[12], view.Selection.SelectedItem);
        Assert.Equal("选中：标签 13", host.Text(UiShowcaseTabs.SelectedId));
        Assert.Equal("总数：13", host.Text(UiShowcaseTabs.CountId));
        Assert.True(viewport.ScrollOffset > 0);
        var bounds = view.Items[12].LayoutBounds;
        Assert.True(bounds.X >= 0);
        Assert.True(bounds.X + bounds.Width <= viewport.ViewportLength + 0.001);
    }

    [Fact]
    public void WheelAndButtonsScrollAndClosingOverflowHidesButtonsUntilReset()
    {
        using var context = new UiTextTestContext();
        using var host = new Host();
        var strip = Assert.Single(host.View.Children.OfType<TabStripPanel>());
        var viewport = Assert.Single(strip.Children.OfType<TabStripViewport>());
        var buttons = strip.Children.OfType<TabScrollButton>().ToArray();
        var position = viewport.LocalToScreen(new Point(40, viewport.LayoutBounds.Height / 2));
        Assert.True(host.Manager.ProcessPointerWheel(position, 0, -1));
        host.Prepare();
        Assert.True(viewport.ScrollOffset > 0);
        var offset = viewport.ScrollOffset;
        host.Click(buttons[1]);
        host.Prepare();
        Assert.True(viewport.ScrollOffset > offset);
        Assert.True(viewport.ScrollOffset <= viewport.GetMaxOffset());
        offset = viewport.ScrollOffset;
        host.Click(buttons[0]);
        host.Prepare();
        Assert.True(viewport.ScrollOffset < offset);

        foreach (var item in host.View.Items.Skip(5).ToArray())
            item.NotifyCloseRequested();
        host.Prepare();
        Assert.False(viewport.HasOverflow);
        Assert.Equal(0d, viewport.ScrollOffset);
        Assert.All(buttons, button =>
        {
            Assert.False(button.IsEnabled);
            Assert.Equal(Visibility.Hidden, button.Visibility);
        });

        host.Click(UiShowcaseTabs.ResetId);
        host.Prepare();
        Assert.True(viewport.HasOverflow);
        Assert.All(buttons, button => Assert.Equal(Visibility.Visible, button.Visibility));
        Assert.False(buttons[0].IsEnabled);
        Assert.True(buttons[1].IsEnabled);
    }

    [Fact]
    public void ClosingUnselectedAndAllTabsUpdatesDiagnosticsAndResetRestoresNumbering()
    {
        using var context = new UiTextTestContext();
        using var host = new Host();
        var view = host.View;
        var first = view.Items[0];
        view.Items[1].NotifyCloseRequested();
        Assert.Same(first, view.Selection.SelectedItem);
        Assert.Equal("总数：11", host.Text(UiShowcaseTabs.CountId));
        Assert.StartsWith("顺序：标签 1 > 标签 3 >", host.Text(UiShowcaseTabs.OrderId));

        foreach (var item in view.Items.ToArray())
            item.NotifyCloseRequested();
        Assert.Null(view.Selection.SelectedItem);
        Assert.Equal("选中：无", host.Text(UiShowcaseTabs.SelectedId));
        Assert.Equal("总数：0", host.Text(UiShowcaseTabs.CountId));
        Assert.Equal("顺序：无", host.Text(UiShowcaseTabs.OrderId));

        host.Click(UiShowcaseTabs.ResetId);
        Assert.Equal(12, view.Items.Count);
        Assert.NotSame(first, view.Items[0]);
        Assert.Same(view.Items[0], view.Selection.SelectedItem);
        host.Click(UiShowcaseTabs.AddId);
        Assert.Equal("选中：标签 13", host.Text(UiShowcaseTabs.SelectedId));
    }

    [Fact]
    public void DragUpdatesOrderAndPreservesSelectedContentAndEditedText()
    {
        using var context = new UiTextTestContext();
        using var host = new Host();
        var view = host.View;
        var first = view.Items[0];
        var content = Assert.IsType<StackPanel>(first.Content);
        var editor = Assert.Single(content.Children.OfType<TextBox>());
        editor.Text = "保留编辑状态";
        host.Prepare();
        var start = first.LocalToScreen(new Point(20, first.LayoutBounds.Height / 2));
        var target = view.Items[2];
        var end = target.LocalToScreen(new Point(target.LayoutBounds.Width - 10, target.LayoutBounds.Height / 2));

        host.Manager.ProcessPointerPressed(start, MouseButton.Left, KeyModifiers.None);
        host.Manager.ProcessPointerMoved(end);
        host.Manager.ProcessPointerReleased(end, MouseButton.Left, KeyModifiers.None);

        Assert.Same(first, view.Items[2]);
        Assert.Same(first, view.Selection.SelectedItem);
        Assert.Same(content, first.Content);
        Assert.Equal("保留编辑状态", editor.Text);
        Assert.StartsWith("顺序：标签 2 > 标签 3 > 标签 1 >", host.Text(UiShowcaseTabs.OrderId));
    }

    private sealed class Host : IDisposable
    {
        private readonly UiNode _content;
        internal UiManager Manager { get; } = new();
        internal TabView View { get; }

        internal Host()
        {
            var example = UiShowcaseControls.CreateExamples(_ => { })[4];
            _content = example.Content;
            var screen = new UiScreen(_content) { UseLayoutRounding = false, Scale = 1 };
            screen.SetStyleSheets([ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase")]);
            Manager.Open(screen);
            Prepare();
            View = ShowcaseTestSupport.FindById<TabView>(_content, UiShowcaseTabs.ViewId);
        }

        internal void Prepare() => Manager.PrepareFrame(new Size(800, 600), 0);

        internal string Text(string id) => ShowcaseTestSupport.FindById<Text>(_content, id).Content;

        internal void Click(string id)
            => Click(ShowcaseTestSupport.FindById<Button>(_content, id));

        internal void Click(UiNode button)
        {
            Prepare();
            var center = button.LocalToScreen(new Point(button.LayoutBounds.Width / 2, button.LayoutBounds.Height / 2));
            Manager.ProcessPointerPressed(center, MouseButton.Left, KeyModifiers.None);
            Manager.ProcessPointerReleased(center, MouseButton.Left, KeyModifiers.None);
        }

        public void Dispose() => Manager.Destroy();
    }
}
