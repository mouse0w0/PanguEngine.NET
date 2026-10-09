using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds fixed layout comparisons for alignment, spacing, placement and visibility.
/// </summary>
internal static class UiShowcaseLayout
{
    /// <summary>
    /// Creates the ordered layout gallery examples.
    /// </summary>
    /// <returns>The layout examples.</returns>
    internal static UiShowcaseExample[] CreateExamples() =>
    [
        CreatePanelExample(),
        CreateStackExample(),
        CreateUniformGridExample(),
        CreateCanvasExample(),
        CreateVisibilityExample()
    ];

    private static UiShowcaseExample CreatePanelExample()
    {
        var samples = new UiShowcaseGallery(130, 3);
        foreach (var vertical in new[] { VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom })
        foreach (var horizontal in new[] { HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right })
        {
            var host = new Panel { Height = 72 };
            host.Classes.Add("showcase-layout-host");
            var target = CreateSlot($"showcase-panel-{horizontal}-{vertical}");
            target.Width = 40;
            target.Height = 24;
            target.HorizontalAlignment = horizontal;
            target.VerticalAlignment = vertical;
            host.Children.Add(target);
            samples.Children.Add(UiShowcaseWidgets.Sample($"{HorizontalName(horizontal)} / {VerticalName(vertical)}", host));
        }
        return new UiShowcaseExample("Panel 对齐", "九个独立样本展示水平与垂直对齐组合。", samples);
    }

    private static UiShowcaseExample CreateStackExample()
    {
        var horizontal = CreateStack("showcase-stack-horizontal", Orientation.Horizontal, 8);
        var vertical = CreateStack("showcase-stack-vertical", Orientation.Vertical, 8);
        var margin = CreateStack("showcase-stack-margin", Orientation.Horizontal, 8);
        margin.Children[1].StyleId = "showcase-stack-margin-target";
        margin.Children[1].Margin = new Thickness(6);
        return new UiShowcaseExample(
            "StackPanel 布局",
            "对比排列方向、元素间距及中间项的外边距。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("横向", horizontal),
                UiShowcaseWidgets.Sample("纵向", vertical),
                UiShowcaseWidgets.Sample("间距 0", CreateStack("showcase-stack-spacing-0", Orientation.Horizontal, 0)),
                UiShowcaseWidgets.Sample("间距 8", CreateStack("showcase-stack-spacing-8", Orientation.Horizontal, 8)),
                UiShowcaseWidgets.Sample("间距 16", CreateStack("showcase-stack-spacing-16", Orientation.Horizontal, 16)),
                UiShowcaseWidgets.Sample("中间项外边距 6", margin)));
    }

    private static StackPanel CreateStack(string id, Orientation orientation, double spacing)
    {
        var stack = new StackPanel { StyleId = id, Orientation = orientation, Spacing = spacing };
        for (var index = 0; index < 3; index++)
            stack.Children.Add(CreateSlot());
        return stack;
    }

    private static UiShowcaseExample CreateUniformGridExample()
    {
        var spacing = CreateUniformGrid("showcase-uniform-grid-spacing", 0, 3);
        spacing.RowSpacing = 4;
        spacing.ColumnSpacing = 12;
        var hidden = CreateUniformGrid("showcase-uniform-grid-hidden", 0, 3);
        hidden.Children[1].Visibility = Visibility.Hidden;
        var collapsed = CreateUniformGrid("showcase-uniform-grid-collapsed", 0, 3);
        collapsed.Children[1].Visibility = Visibility.Collapsed;
        return new UiShowcaseExample(
            "UniformGrid 布局",
            "八个编号格子等分内容区，对比自动行列、固定行列及独立间距。隐藏保留占位，折叠后后续格子前移。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("自动行列 · 3 × 3", CreateUniformGrid("showcase-uniform-grid-auto", 0, 0)),
                UiShowcaseWidgets.Sample("固定 3 列 · 自动行数", CreateUniformGrid("showcase-uniform-grid-columns", 0, 3)),
                UiShowcaseWidgets.Sample("固定 2 行 · 自动列数", CreateUniformGrid("showcase-uniform-grid-rows", 2, 0)),
                UiShowcaseWidgets.Sample("行间距 4 · 列间距 12", spacing),
                UiShowcaseWidgets.Sample("3 列 · 第 2 项隐藏", hidden),
                UiShowcaseWidgets.Sample("3 列 · 第 2 项折叠", collapsed)));
    }

    private static UniformGrid CreateUniformGrid(string id, int rows, int columns)
    {
        var grid = new UniformGrid { StyleId = id, Rows = rows, Columns = columns };
        grid.Classes.Add("showcase-layout-host");
        grid.Classes.Add("showcase-uniform-grid");
        for (var index = 0; index < 8; index++)
        {
            var cell = new Panel { StyleId = $"{id}-cell-{index + 1}" };
            cell.Classes.Add("showcase-uniform-grid-cell");
            var label = UiShowcaseWidgets.Label($"{index + 1:00}");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            cell.Children.Add(label);
            grid.Children.Add(cell);
        }
        return grid;
    }

    private static UiShowcaseExample CreateCanvasExample()
    {
        var samples = new UiShowcaseGallery(190, 2);
        var positions = new[] { (12d, 12d), (128d, 12d), (12d, 80d), (128d, 80d) };
        for (var index = 0; index < positions.Length; index++)
        {
            var host = new Canvas { Width = 180, Height = 116 };
            host.Classes.Add("showcase-layout-host");
            var target = CreateSlot($"showcase-canvas-target-{index}");
            target.Width = 40;
            target.Height = 24;
            var (x, y) = positions[index];
            Canvas.SetLeft(target, x);
            Canvas.SetTop(target, y);
            host.Children.Add(target);
            samples.Children.Add(UiShowcaseWidgets.Sample($"坐标 ({x}, {y})", host));
        }
        return new UiShowcaseExample("Canvas 位置", "四组固定坐标展示自由定位。", samples);
    }

    private static UiShowcaseExample CreateVisibilityExample()
    {
        return new UiShowcaseExample(
            "可见性三态",
            "隐藏保留中间项占位，折叠移除占位。",
            UiShowcaseWidgets.Samples(new[] { Visibility.Visible, Visibility.Hidden, Visibility.Collapsed }
                .Select(visibility =>
                {
                    var row = CreateStack($"showcase-visibility-{visibility}", Orientation.Horizontal, 8);
                    row.Children[1].Visibility = visibility;
                    return UiShowcaseWidgets.Sample(visibility.ToString(), UiShowcaseWidgets.Preview(row));
                }).ToArray()));
    }

    private static Panel CreateSlot(string? id = null) => new()
    {
        StyleId = id,
        Width = 32,
        Height = 24,
        Background = new SolidColorBrush(79, 141, 245)
    };

    private static string HorizontalName(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Left => "左",
        HorizontalAlignment.Center => "中",
        _ => "右"
    };

    private static string VerticalName(VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => "上",
        VerticalAlignment.Center => "中",
        _ => "下"
    };
}
