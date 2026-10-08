using PanguEngine.Client.Screens.Showcase;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiShowcaseGalleryTests
{
    [Fact]
    public void WidthChangesReflowCardsAndRowsUseTheTallestCard()
    {
        var gallery = new UiShowcaseGallery(200, 2);
        var first = new Panel { MinHeight = 40 };
        var second = new Panel { MinHeight = 70 };
        var third = new Panel { MinHeight = 30 };
        gallery.Children.Add(first);
        gallery.Children.Add(second);
        gallery.Children.Add(third);

        Arrange(gallery, 500);
        Assert.Equal(first.LayoutBounds.Y, second.LayoutBounds.Y);
        Assert.Equal(70d, first.LayoutBounds.Height);
        Assert.Equal(70d, second.LayoutBounds.Height);
        Assert.True(second.LayoutBounds.X >= first.LayoutBounds.X + first.LayoutBounds.Width);
        Assert.True(third.LayoutBounds.Y >= second.LayoutBounds.Y + second.LayoutBounds.Height);
        Assert.Equal(116d, gallery.DesiredSize.Height);

        Arrange(gallery, 300);
        Assert.Equal(first.LayoutBounds.X, second.LayoutBounds.X);
        Assert.Equal(40d, first.LayoutBounds.Height);
        Assert.Equal(70d, second.LayoutBounds.Height);
        Assert.Equal(30d, third.LayoutBounds.Height);
        Assert.True(second.LayoutBounds.Y >= first.LayoutBounds.Y + first.LayoutBounds.Height);
        Assert.True(third.LayoutBounds.Y >= second.LayoutBounds.Y + second.LayoutBounds.Height);
        Assert.Equal(172d, gallery.DesiredSize.Height);

        Arrange(gallery, 500);
        Assert.Equal(first.LayoutBounds.Y, second.LayoutBounds.Y);
        Assert.Equal(116d, gallery.DesiredSize.Height);
    }

    [Fact]
    public void CollapsedCardsDoNotReserveSlotsAndEmptyGalleryHasNoHeight()
    {
        var gallery = new UiShowcaseGallery(200, 2);
        var collapsed = new Panel { Height = 100, Visibility = Visibility.Collapsed };
        var visible = new Panel { Height = 30 };
        gallery.Children.Add(collapsed);
        gallery.Children.Add(visible);
        Arrange(gallery, 500);
        Assert.Equal(0d, visible.LayoutBounds.X);
        Assert.Equal(0d, visible.LayoutBounds.Y);
        Assert.Equal(30d, gallery.DesiredSize.Height);

        gallery.Children.Clear();
        Arrange(gallery, 500);
        Assert.Equal(0d, gallery.DesiredSize.Height);
    }

    [Fact]
    public void InfiniteWidthUsesFiniteNaturalSize()
    {
        var gallery = new UiShowcaseGallery();
        gallery.Children.Add(new Panel { Width = 80, Height = 40 });
        gallery.Measure(Size.Infinite);
        Assert.Equal(new Size(80, 40), gallery.DesiredSize);
        gallery.Arrange(new Rect(0, 0, gallery.DesiredSize));
        Assert.Equal(80d, gallery.Children[0].LayoutBounds.Width);
    }

    private static void Arrange(UiShowcaseGallery gallery, double width)
    {
        gallery.Measure(new Size(width, double.PositiveInfinity));
        gallery.Arrange(new Rect(0, 0, width, gallery.DesiredSize.Height));
    }
}
