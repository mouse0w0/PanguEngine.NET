using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class TabItemTests
{
    private static readonly UiPseudoClass SelectedPseudoClass = UiPseudoClass.Get("selected");

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
        var headerHost = Assert.IsType<TabHeaderHost>(header.Parent);
        Assert.Same(item, headerHost.Parent);

        var view = new TabView();
        view.Items.Add(item);

        Assert.NotNull(item.ContentHost);
        Assert.Same(item.ContentHost, content.Parent);
        Assert.Same(content, item.ContentHost!.Content);
    }

    [Fact]
    public void HeaderAndContentCannotReferenceTheSameNode()
    {
        var node = new Panel();
        var item = new TabItem();
        item.Header = node;

        Assert.Throws<InvalidOperationException>(() => item.Content = node);
        Assert.Null(item.Content);
        Assert.Same(node, item.Header);

        var otherNode = new Panel();
        var otherItem = new TabItem();
        otherItem.Content = otherNode;

        Assert.Throws<InvalidOperationException>(() => otherItem.Header = otherNode);
        Assert.Null(otherItem.Header);
    }

    [Fact]
    public void MovingTheHeaderOutOfTheItemClearsTheHeaderSlot()
    {
        var item = new TabItem();
        var header = new Panel();
        item.Header = header;
        var other = new Panel();

        other.Children.Add(header);

        Assert.Null(item.Header);
        Assert.Empty(Assert.IsType<TabHeaderHost>(Assert.Single(item.Children)).Children);
        Assert.Same(other, header.Parent);
    }

    [Fact]
    public void MovingTheContentOutOfItsHostClearsTheContentSlot()
    {
        var item = new TabItem();
        var content = new Panel();
        item.Content = content;
        var view = new TabView();
        view.Items.Add(item);
        var other = new Panel();

        other.Children.Add(content);

        Assert.Null(item.Content);
        Assert.Null(item.ContentHost!.Content);
        Assert.Same(other, content.Parent);
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
        Assert.Same(item.ContentHost, content.Parent);
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

        Assert.True(first.HasPseudoClass(SelectedPseudoClass));
        Assert.False(second.HasPseudoClass(SelectedPseudoClass));

        view.Selection.SelectedItem = second;

        Assert.False(first.HasPseudoClass(SelectedPseudoClass));
        Assert.True(second.HasPseudoClass(SelectedPseudoClass));

        view.Selection.SelectedItem = null;

        Assert.False(second.HasPseudoClass(SelectedPseudoClass));
    }

    [Fact]
    public void AHeaderCannotReferenceTheOwningItem()
    {
        var item = new TabItem();

        Assert.Throws<InvalidOperationException>(() => item.Header = item);
        Assert.Null(item.Header);
    }

    [Fact]
    public void ContentCannotReferenceTheOwningItem()
    {
        var view = new TabView();
        var item = new TabItem();
        view.Items.Add(item);

        Assert.Throws<InvalidOperationException>(() => item.Content = item);

        Assert.Null(item.Content);
        Assert.Same(item, view.Items[0]);
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
    public void UnownedItemCannotAdoptViewInternalParts()
    {
        var view = new TabView();
        var item = new TabItem();
        var strip = view.Children[0];
        var contentArea = view.Children[1];

        Assert.Throws<InvalidOperationException>(() => item.Header = strip);
        Assert.Throws<InvalidOperationException>(() => item.Content = contentArea);

        Assert.Same(view, strip.Parent);
        Assert.Same(view, contentArea.Parent);
        Assert.Null(item.Header);
        Assert.Null(item.Content);
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
        var host = Assert.IsType<TabHeaderHost>(header.Parent);
        var close = Assert.Single(item.Children.OfType<TabCloseButton>());

        Assert.True(host.ClipToBounds);
        Assert.True(host.LayoutBounds.X + host.LayoutBounds.Width <= close.LayoutBounds.X);
        Assert.Same(close, item.HitTest(new Point(
            close.LayoutBounds.X + close.LayoutBounds.Width / 2,
            close.LayoutBounds.Y + close.LayoutBounds.Height / 2)));
    }
}
