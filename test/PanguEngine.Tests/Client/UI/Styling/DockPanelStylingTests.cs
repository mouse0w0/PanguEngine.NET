using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class DockPanelStylingTests
{
    [Fact]
    public void DockPanelSelectorResolvesFillAndInheritedBoxProperties()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            DockPanel { last-child-fill: false; padding: 3px; }
            """)]);
        var snapshot = resolver.Resolve(new DockPanel());

        Assert.False(snapshot.GetValue(DockPanel.LastChildFillProperty));
        Assert.Equal(new Thickness(3), snapshot.GetValue(Region.PaddingProperty));
    }

    [Theory]
    [InlineData("left", Dock.Left)]
    [InlineData("TOP", Dock.Top)]
    [InlineData("Right", Dock.Right)]
    [InlineData("bottom", Dock.Bottom)]
    public void NamedDockValuesAreCaseInsensitive(string value, Dock expected)
    {
        Assert.Equal(expected, UiCssValueConverters.ParseDock(value));
    }

    [Theory]
    [InlineData("UiNode { dock: center; }", false)]
    [InlineData("UiNode { dock: 0; }", false)]
    [InlineData("DockPanel { last-child-fill: yes; }", true)]
    public void InvalidDockAndFillValuesProduceCssDiagnostics(string css, bool targetsPanel)
    {
        var panel = new DockPanel();
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(css)]);
        UiNode node = targetsPanel ? panel : new TestNode();

        var error = Assert.Throws<UiStyleParseException>(() => resolver.Resolve(node));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void CssDirectionChangesInvalidateLayoutAndLocalValuesCanBeCleared()
    {
        var panel = new DockPanel();
        var edge = new TestNode { CoreDesiredSize = new Size(20, 10) };
        var fill = new TestNode();
        edge.Classes.Add("edge");
        panel.Children.AddRange([edge, fill]);
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            DockPanel { last-child-fill: false; }
            .edge { dock: top; }
            .bottom { dock: bottom; }
            """)]);
        panel.UpdateStyles();
        ValidateLayout(panel);

        Assert.False(panel.LastChildFill);
        Assert.Equal(Dock.Top, DockPanel.GetDock(edge));
        Assert.Equal(new Rect(0, 0, 100, 10), edge.LayoutBounds);

        edge.Classes.Add("bottom");
        panel.UpdateStyles();

        Assert.False(edge.IsMeasureValid);
        Assert.False(panel.IsMeasureValid);
        Assert.Equal(Dock.Bottom, DockPanel.GetDock(edge));
        ValidateLayout(panel);
        Assert.Equal(new Rect(0, 50, 100, 10), edge.LayoutBounds);

        DockPanel.SetDock(edge, Dock.Right);
        ValidateLayout(panel);
        Assert.Equal(new Rect(80, 0, 20, 60), edge.LayoutBounds);

        edge.ClearValue(DockPanel.DockProperty);
        Assert.False(panel.IsMeasureValid);
        Assert.Equal(Dock.Bottom, DockPanel.GetDock(edge));
        ValidateLayout(panel);
        Assert.Equal(new Rect(0, 50, 100, 10), edge.LayoutBounds);

        panel.LastChildFill = true;
        Assert.True(panel.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);
        panel.Arrange(new Rect(0, 0, 100, 60));
        Assert.Equal(new Rect(0, 0, 100, 50), fill.LayoutBounds);

        panel.ClearValue(DockPanel.LastChildFillProperty);
        Assert.True(panel.IsMeasureValid);
        Assert.False(panel.LastChildFill);
        Assert.False(panel.IsArrangeValid);
        panel.Arrange(new Rect(0, 0, 100, 60));
        Assert.Equal(new Rect(0, 0, 0, 50), fill.LayoutBounds);
    }

    private static void ValidateLayout(UiNode node)
    {
        node.Measure(new Size(100, 60));
        node.Arrange(new Rect(0, 0, 100, 60));
    }

    private sealed class TestNode : UiNode
    {
        internal Size CoreDesiredSize { get; set; }

        protected override Size MeasureCore(Size availableSize) => CoreDesiredSize;
    }
}
