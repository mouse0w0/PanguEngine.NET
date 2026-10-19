using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class GridStyleTests
{
    [Fact]
    public void CssDefinitionsAndPlacementParticipateInLayout()
    {
        var grid = new Grid();
        var child = new Panel();
        child.Classes.Add("field");
        grid.Children.Add(child);
        var scene = new UIScene(grid);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             Grid { column-definitions: 20px, *, 2*; row-definitions: Auto, *; column-spacing: 5px; row-spacing: 4px; }
                             .field { grid-column: 1; grid-row: 1; grid-column-span: 2; }
                             """)
        ]);
        grid.UpdateStyles();
        grid.Measure(new Size(120, 40));
        grid.Arrange(new Rect(0, 0, 120, 40));

        Assert.Equal(new Rect(25, 4, 95, 36), child.LayoutBounds);
        Assert.Equal(2, Grid.GetColumnSpan(child));
        Assert.Equal(3, grid.ColumnDefinitions.Count);
    }

    [Fact]
    public void StyleChangesInvalidateMeasureAndLocalDefinitionsCanRevert()
    {
        var grid = new Grid();
        var child = new Panel();
        grid.Children.Add(child);
        var scene = new UIScene(grid);
        scene.SetStyleSheets([StyleSheet.Parse("Grid { column-definitions: 20, *; }")]);
        grid.UpdateStyles();
        grid.Measure(new Size(100, 20));
        grid.Arrange(new Rect(0, 0, 100, 20));

        child.Classes.Add("second");
        scene.SetStyleSheets([
            StyleSheet.Parse(
                "Grid { column-definitions: 30, *; } .second { grid-column: 1; }")
        ]);
        grid.UpdateStyles();
        Assert.False(grid.IsMeasureValid);
        Assert.Equal(1, Grid.GetColumn(child));

        grid.ColumnDefinitions = new(40, GridLength.Star);
        Assert.Equal(new GridLength(40), grid.ColumnDefinitions[0].Length);
        grid.ClearValue(Grid.ColumnDefinitionsProperty);
        Assert.Equal(new GridLength(30), grid.ColumnDefinitions[0].Length);

        scene.SetStyleSheets([]);
        grid.UpdateStyles();
        Assert.Equal(GridDefinitions.Empty, grid.ColumnDefinitions);
        Assert.Equal(0, Grid.GetColumn(child));
    }

    [Fact]
    public void AttachedStyleChangesAloneInvalidateParentAndMoveChild()
    {
        var grid = new Grid();
        var child = new Panel();
        grid.Children.Add(child);
        var scene = new UIScene(grid);
        scene.SetStyleSheets([
            StyleSheet.Parse(
                "Grid { column-definitions: 20, *; } .second { grid-column: 1; }")
        ]);
        grid.UpdateStyles();
        grid.Measure(new Size(100, 20));
        grid.Arrange(new Rect(0, 0, 100, 20));
        Assert.Equal(new Rect(0, 0, 20, 20), child.LayoutBounds);

        child.Classes.Add("second");
        Assert.True(grid.IsMeasureValid);
        grid.UpdateStyles();
        Assert.False(grid.IsMeasureValid);
        grid.Measure(new Size(100, 20));
        grid.Arrange(new Rect(0, 0, 100, 20));
        Assert.Equal(new Rect(20, 0, 80, 20), child.LayoutBounds);
    }

    [Theory]
    [InlineData("column-definitions: Auto,,*;")]
    [InlineData("column-definitions: 1fr;")]
    [InlineData("column-spacing: -1px;")]
    [InlineData("grid-row: -1;")]
    [InlineData("grid-column: 1.5;")]
    [InlineData("grid-row-span: 0;")]
    [InlineData("grid-column-span: 2px;")]
    public void InvalidCssReportsInvalidValue(string declaration)
    {
        var grid = new Grid();
        var scene = new UIScene(grid);
        scene.SetStyleSheets([StyleSheet.Parse($"Grid {{ {declaration} }}")]);
        var error = Assert.Throws<StyleParseException>(grid.UpdateStyles);
        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }
}