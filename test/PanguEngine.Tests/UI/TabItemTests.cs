using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.Tests.UI;

public sealed class TabItemTests
{
    [Fact]
    public void SharedDataUsesIndependentTemplatesAndKeepsItsPageAcrossSelectionAndReattachment()
    {
        var data = new object();
        var headerCalls = 0;
        var contentCalls = 0;
        var item = new TabItem
        {
            HeaderTemplate = new FuncDataTemplate<object>(_ =>
            {
                headerCalls++;
                return new Panel();
            }),
            ContentTemplate = new FuncDataTemplate<object>(_ =>
            {
                contentCalls++;
                return new Panel();
            }),
            Header = data,
            Content = data
        };
        var header = item.HeaderRoot;
        var content = item.ContentRoot;
        var other = new TabItem();
        var view = new TabView();
        view.Items.Add(item);
        view.Items.Add(other);
        view.Selection.SelectedItem = other;
        view.Selection.SelectedItem = item;
        view.Items.Remove(item);
        view.Items.Add(item);

        Assert.Same(data, item.Header);
        Assert.Same(data, item.Content);
        Assert.NotSame(header, content);
        Assert.Same(header, item.HeaderRoot);
        Assert.Same(content, item.ContentRoot);
        Assert.Equal(1, headerCalls);
        Assert.Equal(1, contentCalls);
    }

    [Fact]
    public void IsClosableTogglesAnIndependentNonFocusableCloseButton()
    {
        var item = new TabItem();
        Assert.Empty(item.Children.OfType<TabCloseButton>());

        item.IsClosable = true;

        var close = Assert.Single(item.Children.OfType<TabCloseButton>());
        Assert.False(close.Focusable);

        item.IsClosable = false;

        Assert.Empty(item.Children.OfType<TabCloseButton>());
    }

    [Fact]
    public void HeaderAndContentUseIndependentSlots()
    {
        var item = new TabItem();
        var header = new Panel();
        var content = new Panel();

        item.Header = header;
        item.Content = content;

        Assert.Same(header, item.Header);
        Assert.Same(content, item.Content);
        Assert.Same(item, header.GetStyleRoot());

        var view = new TabView();
        view.Items.Add(item);

        Assert.NotNull(item.ContentHost);
        Assert.Same(content, item.ContentRoot);
        Assert.Same(content, item.ContentHost!.Content);
    }

    [Fact]
    public void ContentInstanceIsPreservedAcrossRemoveAndReAdd()
    {
        var item = new TabItem();
        var content = new Panel();
        item.Content = content;
        var view = new TabView();
        view.Items.Add(item);

        view.Items.Remove(item);

        Assert.Same(content, item.Content);

        view.Items.Add(item);

        Assert.Same(content, item.Content);
        Assert.Same(content, item.ContentRoot);
        Assert.Same(content, item.ContentHost!.Content);
    }

    [Fact]
    public void SelectionDrivesTheSelectedPseudoClass()
    {
        var view = new TabView();
        var first = new TabItem();
        var second = new TabItem();
        view.Items.Add(first);
        view.Items.Add(second);

        Assert.True(first.HasPseudoClass(PseudoClass.Selected));
        Assert.False(second.HasPseudoClass(PseudoClass.Selected));

        view.Selection.SelectedItem = second;

        Assert.False(first.HasPseudoClass(PseudoClass.Selected));
        Assert.True(second.HasPseudoClass(PseudoClass.Selected));

        view.Selection.SelectedItem = null;

        Assert.False(second.HasPseudoClass(PseudoClass.Selected));
    }

    [Fact]
    public void SelectionDoesNotOverrideTheContentNodeVisibility()
    {
        var item = new TabItem();
        var content = new Panel { Visibility = Visibility.Hidden };
        item.Content = content;
        var view = new TabView();
        view.Items.Add(item);

        Assert.Same(content, item.ContentHost!.Content);
        Assert.Equal(Visibility.Hidden, content.Visibility);
        Assert.Equal(Visibility.Visible, item.ContentHost.Visibility);
    }

    [Fact]
    public void MovingATabOutOfItsStripUpdatesTheItemsAndSelection()
    {
        var view = new TabView();
        var first = new TabItem();
        var second = new TabItem();
        view.Items.Add(first);
        view.Items.Add(second);
        var destination = new Panel();

        destination.Children.Add(first);

        Assert.Same(second, Assert.Single(view.Items));
        Assert.Same(second, view.Selection.SelectedItem);
        Assert.False(first.IsSelected);
        Assert.Null(first.OwnerView);
        Assert.Same(destination, first.Parent);
    }

    [Fact]
    public void OversizedHeaderIsClippedBeforeTheCloseButton()
    {
        var header = new Panel { Width = 1000, Height = 20 };
        var item = new TabItem { Header = header, IsClosable = true };
        item.Measure(new Size(96, 30));
        item.Arrange(new Rect(0, 0, 96, 30));
        var host = Assert.Single(item.Children.OfType<TabHeaderHost>());
        var close = Assert.Single(item.Children.OfType<TabCloseButton>());

        Assert.True(host.ClipToBounds);
        Assert.True(host.LayoutBounds.X + host.LayoutBounds.Width <= close.LayoutBounds.X);
        Assert.Same(close, item.HitTest(new Point(
            close.LayoutBounds.X + close.LayoutBounds.Width / 2,
            close.LayoutBounds.Y + close.LayoutBounds.Height / 2)));
    }
}
