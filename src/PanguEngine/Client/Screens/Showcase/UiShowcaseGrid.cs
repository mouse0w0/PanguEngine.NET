using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds grid comparisons for sizing, cell spans, spacing and track limits.
/// </summary>
internal static class UiShowcaseGrid
{
    private static readonly Color Blue = new(47, 91, 122);
    private static readonly Color Green = new(47, 91, 58);
    private static readonly Color Purple = new(74, 59, 107);

    /// <summary>
    /// Creates a grid layout example containing sizing, spans, spacing and limits.
    /// </summary>
    /// <returns>The grid layout example.</returns>
    internal static UiShowcaseExample CreateExample()
    {
        var sizingAndSpans = new UiShowcaseGallery(260, 2);
        sizingAndSpans.Children.Add(CreateSizingSample());
        sizingAndSpans.Children.Add(CreateSpanSample());
        return new UiShowcaseExample(
            "Grid 布局",
            "比较固定、Auto、Star 尺寸、跨行跨列、行列间距和 Min/Max 约束；调整窗口宽度观察变化。",
            UiShowcaseWidgets.Column(
                sizingAndSpans,
                CreateSpacingSample(),
                CreateLimitsSample()));
    }

    private static UiNode CreateSizingSample()
    {
        var grid = CreateGrid("showcase-grid-sizing", 74,
            new(64, GridLength.Auto, GridLength.Star, new GridLength(2, GridUnitType.Star)),
            new(GridLength.Star));
        AddCell(grid, "64px", 0, 0, Blue);
        AddCell(grid, "Auto", 0, 1, Green);
        AddCell(grid, "*", 0, 2, Purple);
        AddCell(grid, "2*", 0, 3, Blue);
        return UiShowcaseWidgets.Sample("尺寸分配 · 64px / Auto / * / 2*", grid);
    }

    private static UiNode CreateSpanSample()
    {
        var grid = CreateGrid("showcase-grid-spans", 180,
            new(64, GridLength.Star, GridLength.Star),
            new(36, GridLength.Star, GridLength.Star));
        AddCell(grid, "跨三列", 0, 0, Blue, columnSpan: 3);
        AddCell(grid, "跨两行", 1, 0, Green, rowSpan: 2);
        AddCell(grid, "跨两列", 1, 1, Purple, columnSpan: 2);
        AddCell(grid, "单元 A", 2, 1, Blue);
        AddCell(grid, "单元 B", 2, 2, Green);
        return UiShowcaseWidgets.Sample("跨行跨列 · 列：64px / * / *；行：36px / * / *", grid);
    }

    private static UiNode CreateSpacingSample()
    {
        var samples = new UiShowcaseGallery(170, 3);
        foreach (var spacing in new[] { 0, 8, 16 })
        {
            var grid = CreateGrid($"showcase-grid-spacing-{spacing}", 96,
                new(GridLength.Star, GridLength.Star),
                new(GridLength.Star, GridLength.Star));
            grid.RowSpacing = spacing;
            grid.ColumnSpacing = spacing;
            AddCell(grid, "A", 0, 0, Blue);
            AddCell(grid, "B", 0, 1, Green);
            AddCell(grid, "C", 1, 0, Purple);
            AddCell(grid, "D", 1, 1, Blue);
            samples.Children.Add(UiShowcaseWidgets.Sample($"行列间距 {spacing}px", grid));
        }
        return UiShowcaseWidgets.Sample("行列间距 · 0 / 8 / 16", samples);
    }

    private static UiNode CreateLimitsSample()
    {
        var equal = CreateGrid("showcase-grid-equal", 74,
            new(GridLength.Star, GridLength.Star), new(GridLength.Star));
        var constrained = CreateGrid("showcase-grid-limits", 74,
            new(new GridDefinition(GridLength.Star, minLength: 64, maxLength: 96), GridLength.Star),
            new(GridLength.Star));
        foreach (var grid in new[] { equal, constrained })
        {
            AddCell(grid, "左列", 0, 0, Blue);
            AddCell(grid, "右列", 0, 1, Green);
        }
        var samples = new UiShowcaseGallery(170, 2);
        samples.Children.Add(UiShowcaseWidgets.Sample("两列等分：* / *", equal));
        samples.Children.Add(UiShowcaseWidgets.Sample("左列：Min 64 / Max 96；右列：*", constrained));
        return UiShowcaseWidgets.Sample("Min / Max 约束", samples);
    }

    private static Grid CreateGrid(string id, double height, GridDefinitions columns, GridDefinitions rows)
    {
        var grid = new Grid
        {
            StyleId = id,
            Height = height,
            ColumnDefinitions = columns,
            RowDefinitions = rows,
            ColumnSpacing = 8,
            RowSpacing = 8,
            Padding = new Thickness(8)
        };
        grid.Classes.Add("showcase-layout-host");
        return grid;
    }

    private static void AddCell(Grid grid, string text, int row, int column, Color color,
        int rowSpan = 1, int columnSpan = 1)
    {
        var cell = new Panel { Padding = new Thickness(4), Background = new SolidColorBrush(color) };
        cell.Children.Add(new Text
        {
            Content = text,
            FontSize = 13,
            Wrapping = TextWrapping.Wrap,
            Alignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        Grid.SetRowSpan(cell, rowSpan);
        Grid.SetColumnSpan(cell, columnSpan);
        grid.Children.Add(cell);
    }
}
