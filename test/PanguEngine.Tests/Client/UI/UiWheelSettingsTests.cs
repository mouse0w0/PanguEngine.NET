using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(UiToolkitCollection.Name)]
public sealed class UiWheelSettingsTests
{
    [Theory]
    [InlineData(5, 40d)]
    [InlineData(-1, 100d)]
    [InlineData(100, 100d)]
    [InlineData(0, 0d)]
    public void ScrollViewUsesGlobalLinesAndPreservesFractionalDeltas(int lines, double expected)
    {
        using var settings = new WheelSettingsScope(lines);
        var view = CreateView();
        var manager = Open(view, new Size(200, 200));
        try
        {
            var handled = manager.ProcessPointerWheel(new Point(50, 50), 0, -0.5, KeyModifiers.None);

            Assert.Equal(expected, view.Offset.Y);
            Assert.Equal(0, view.Offset.X);
            Assert.Equal(lines != 0, handled);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(KeyModifiers.None)]
    [InlineData(KeyModifiers.Shift)]
    public void ScrollViewUsesTheHorizontalViewportForHorizontalWheelAndShift(KeyModifiers modifiers)
    {
        using var settings = new WheelSettingsScope(-1);
        var view = CreateView();
        var manager = Open(view, new Size(200, 100));
        try
        {
            var deltaX = modifiers == KeyModifiers.None ? -0.5 : 0;
            var deltaY = modifiers == KeyModifiers.Shift ? -0.5 : 0;

            Assert.True(manager.ProcessPointerWheel(new Point(50, 50), deltaX, deltaY, modifiers));
            Assert.Equal(new Point(100, 0), view.Offset);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ChangingGlobalLinesAffectsTheNextWheelWithoutChangingKeyboardSteps()
    {
        using var settings = new WheelSettingsScope(5);
        var view = CreateView();
        var manager = Open(view, new Size(200, 200));
        try
        {
            Assert.True(manager.ProcessPointerWheel(new Point(50, 50), 0, -1, KeyModifiers.None));
            Assert.Equal(80, view.Offset.Y);
            UiToolkit.WheelScrollLines = 2;
            manager.PrepareFrame(new Size(200, 200), 0);
            Assert.True(manager.ProcessPointerWheel(new Point(50, 50), 0, -1, KeyModifiers.None));
            Assert.Equal(112, view.Offset.Y);

            Assert.True(view.Focus());
            Assert.True(manager.ProcessKeyDown(Key.Down, KeyModifiers.None));
            Assert.Equal(128, view.Offset.Y);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(-1, -2d, 400d)]
    [InlineData(-1, 0.5d, -100d)]
    [InlineData(5, 0.5d, -40d)]
    public void WheelDistancePreservesMultipleNotchesAndReverseDirection(int lines, double delta, double distance)
    {
        using var settings = new WheelSettingsScope(lines);
        var view = CreateView();
        var manager = Open(view, new Size(200, 200));
        try
        {
            view.ScrollTo(0, 200);
            manager.PrepareFrame(new Size(200, 200), 0);

            Assert.True(manager.ProcessPointerWheel(new Point(50, 50), 0, delta, KeyModifiers.None));
            Assert.Equal(200 + distance, view.Offset.Y);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ZeroLinesDoNotConsumeTheWheelEvenWhenScrollChainingIsDisabled()
    {
        using var settings = new WheelSettingsScope(0);
        var view = CreateView();
        view.IsScrollChainingEnabled = false;
        var manager = Open(view, new Size(200, 200));
        try
        {
            Assert.False(manager.ProcessPointerWheel(new Point(50, 50), 0, -1, KeyModifiers.None));
            Assert.Equal(Point.Zero, view.Offset);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void NestedScrollViewAppliesGlobalLinesOnceWhenTheInnerViewIsAtItsBoundary()
    {
        using var settings = new WheelSettingsScope(5);
        var inner = CreateView();
        inner.Width = 200;
        inner.Height = 200;
        var content = new Canvas { Width = 200, Height = 1000 };
        content.Children.Add(inner);
        var outer = new ScrollView { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        var manager = Open(outer, new Size(200, 200));
        try
        {
            inner.ScrollTo(0, 800);
            manager.PrepareFrame(new Size(200, 200), 0);

            Assert.True(manager.ProcessPointerWheel(new Point(50, 50), 0, -0.5, KeyModifiers.None));
            Assert.Equal(800, inner.Offset.Y);
            Assert.Equal(40, outer.Offset.Y);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(5, 40d)]
    [InlineData(-1, 50d)]
    [InlineData(100, 50d)]
    [InlineData(0, 0d)]
    public void StandaloneScrollBarUsesItsLargeChangeForPageScrolling(int lines, double expected)
    {
        using var settings = new WheelSettingsScope(lines);
        var bar = new ScrollBar { Width = 16, Height = 200, Maximum = 1000, SmallChange = 16, LargeChange = 100 };
        var manager = Open(bar, new Size(16, 200));
        try
        {
            var handled = manager.ProcessPointerWheel(new Point(8, 100), 0, -0.5, KeyModifiers.None);

            Assert.Equal(expected, bar.Value);
            Assert.Equal(lines != 0, handled);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(TabStripPlacement.Top, 5)]
    [InlineData(TabStripPlacement.Top, -1)]
    [InlineData(TabStripPlacement.Top, 0)]
    [InlineData(TabStripPlacement.Left, 5)]
    [InlineData(TabStripPlacement.Left, -1)]
    [InlineData(TabStripPlacement.Left, 0)]
    public void TabStripUsesGlobalLinesOrItsViewportForEachAxis(TabStripPlacement placement, int lines)
    {
        using var settings = new WheelSettingsScope(lines);
        var view = new TabView { TabStripPlacement = placement };
        for (var index = 0; index < 20; index++)
            view.Items.Add(new TabItem { Header = new Panel { Width = 40, Height = 20 } });
        var manager = Open(view, new Size(300, 200));
        try
        {
            var viewport = Assert.IsType<TabStripPanel>(view.Children[0]).Viewport;
            Assert.True(viewport.HasOverflow);
            var position = viewport.LocalToScreen(new Point(10, 10));
            var before = viewport.ScrollOffset;
            var expected = lines == -1 ? viewport.ViewportLength / 2 : lines * 8d;

            var handled = manager.ProcessPointerWheel(position, 0, -0.5, KeyModifiers.None);

            Assert.Equal(before + expected, viewport.ScrollOffset, 8);
            Assert.Equal(lines != 0, handled);
        }
        finally
        {
            manager.Close();
        }
    }

    private static ScrollView CreateView() => new()
    {
        SmallChange = 16,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        Content = new Panel { Width = 1000, Height = 1000 }
    };

    private static UiManager Open(UiNode root, Size size)
    {
        var screen = new UiScreen(root) { Scale = 1 };
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        manager.PrepareFrame(size, 0);
        return manager;
    }

    private sealed class WheelSettingsScope : IDisposable
    {
        private readonly int _original = UiToolkit.WheelScrollLines;

        internal WheelSettingsScope(int lines) => UiToolkit.WheelScrollLines = lines;

        public void Dispose() => UiToolkit.WheelScrollLines = _original;
    }
}
