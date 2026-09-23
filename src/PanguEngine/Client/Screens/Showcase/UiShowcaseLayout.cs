using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens;

/// <summary>
/// Builds the layout showcase examples for panel alignment, stacking, canvas placement and visibility.
/// </summary>
internal static class UiShowcaseLayout
{
    /// <summary>
    /// Creates the layout showcase examples.
    /// </summary>
    /// <param name="report">Receives a short feedback message for the most recent operation.</param>
    /// <returns>The ordered layout examples.</returns>
    internal static UiShowcaseExample[] CreateExamples(Action<string> report) =>
    [
        CreatePanelExample(report),
        CreateStackExample(report),
        CreateCanvasExample(report),
        CreateVisibilityExample(report)
    ];

    private static UiShowcaseExample CreatePanelExample(Action<string> report)
    {
        var host = new Panel
        {
            StyleId = "showcase-panel-host",
            Width = 240,
            Height = 120,
            Background = new SolidColorBrush(0x1F, 0x23, 0x29)
        };
        var target = new Panel
        {
            StyleId = "showcase-panel-target",
            Width = 96,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(0x2F, 0x64, 0xA0)
        };
        host.Children.Add(target);

        var horizontal = HorizontalAlignment.Left;
        var vertical = VerticalAlignment.Top;
        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-panel-report");

        void Refresh() => status.Content = $"对齐: {DescribeHorizontal(horizontal)}/{DescribeVertical(vertical)}";

        var horizontalButton = UiShowcaseWidgets.ActionButton("showcase-panel-horizontal", "切换水平对齐", () =>
        {
            horizontal = horizontal switch
            {
                HorizontalAlignment.Left => HorizontalAlignment.Center,
                HorizontalAlignment.Center => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };
            target.HorizontalAlignment = horizontal;
            Refresh();
            report($"水平对齐: {DescribeHorizontal(horizontal)}");
        });
        var verticalButton = UiShowcaseWidgets.ActionButton("showcase-panel-vertical", "切换垂直对齐", () =>
        {
            vertical = vertical switch
            {
                VerticalAlignment.Top => VerticalAlignment.Center,
                VerticalAlignment.Center => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };
            target.VerticalAlignment = vertical;
            Refresh();
            report($"垂直对齐: {DescribeVertical(vertical)}");
        });

        Refresh();

        var content = UiShowcaseWidgets.Column(
            host,
            UiShowcaseWidgets.Row(horizontalButton, verticalButton),
            status);
        return new UiShowcaseExample(
            "Panel 对齐",
            "切换示例节点的水平与垂直对齐，观察节点在内容槽中移动。",
            content);
    }

    private static UiShowcaseExample CreateStackExample(Action<string> report)
    {
        var host = new StackPanel
        {
            StyleId = "showcase-stack-host",
            Orientation = Orientation.Vertical,
            Spacing = 8
        };
        var item1 = CreateSlot("showcase-stack-item-1");
        var item2 = CreateSlot("showcase-stack-item-2");
        var item3 = CreateSlot("showcase-stack-item-3");
        host.Children.Add(item1);
        host.Children.Add(item2);
        host.Children.Add(item3);

        var spacingValues = new[] { 0d, 8d, 16d };
        var spacingIndex = 1;
        var marginValues = new[] { 0d, 6d, 12d };
        var marginIndex = 0;
        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-stack-report");

        void Refresh() => status.Content =
            $"方向: {DescribeOrientation(host.Orientation)} 间距: {host.Spacing} 外边距: {item2.Margin.Left}";

        var orientationButton = UiShowcaseWidgets.ActionButton("showcase-stack-orientation", "切换方向", () =>
        {
            host.Orientation = host.Orientation == Orientation.Vertical
                ? Orientation.Horizontal
                : Orientation.Vertical;
            Refresh();
            report($"方向: {DescribeOrientation(host.Orientation)}");
        });
        var spacingButton = UiShowcaseWidgets.ActionButton("showcase-stack-spacing", "切换间距", () =>
        {
            spacingIndex = (spacingIndex + 1) % spacingValues.Length;
            host.Spacing = spacingValues[spacingIndex];
            Refresh();
            report($"间距: {host.Spacing}");
        });
        var marginButton = UiShowcaseWidgets.ActionButton("showcase-stack-margin", "切换外边距", () =>
        {
            marginIndex = (marginIndex + 1) % marginValues.Length;
            var value = marginValues[marginIndex];
            item2.Margin = new Thickness(value);
            Refresh();
            report($"外边距: {value}");
        });

        Refresh();

        var content = UiShowcaseWidgets.Column(
            host,
            UiShowcaseWidgets.Row(orientationButton, spacingButton, marginButton),
            status);
        return new UiShowcaseExample(
            "StackPanel 布局",
            "切换方向、间距与中间项外边距，观察顺序布局变化。",
            content);
    }

    private static UiShowcaseExample CreateCanvasExample(Action<string> report)
    {
        var host = new Canvas
        {
            StyleId = "showcase-canvas-host",
            Width = 260,
            Height = 140,
            Background = new SolidColorBrush(0x1F, 0x23, 0x29)
        };
        var target = new Panel
        {
            StyleId = "showcase-canvas-target",
            Width = 80,
            Height = 36,
            Background = new SolidColorBrush(0x2F, 0x5B, 0x3A)
        };
        host.Children.Add(target);

        var positions = new (double X, double Y)[] { (10, 10), (150, 10), (10, 84), (150, 84) };
        var positionIndex = 0;
        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-canvas-report");

        void Apply()
        {
            var (x, y) = positions[positionIndex];
            Canvas.SetLeft(target, x);
            Canvas.SetTop(target, y);
            status.Content = $"位置: ({x}, {y})";
        }

        var cycle = UiShowcaseWidgets.ActionButton("showcase-canvas-cycle", "切换位置", () =>
        {
            positionIndex = (positionIndex + 1) % positions.Length;
            Apply();
            var (x, y) = positions[positionIndex];
            report($"位置: ({x}, {y})");
        });

        Apply();

        var content = UiShowcaseWidgets.Column(host, cycle, status);
        return new UiShowcaseExample(
            "Canvas 位置",
            "切换预设坐标，观察固定位置布局。",
            content);
    }

    private static UiShowcaseExample CreateVisibilityExample(Action<string> report)
    {
        var host = UiShowcaseWidgets.Row();
        host.StyleId = "showcase-visibility-host";
        var before = CreateSlot("showcase-visibility-before");
        var target = CreateSlot("showcase-visibility-target");
        var after = CreateSlot("showcase-visibility-after");
        host.Children.Add(before);
        host.Children.Add(target);
        host.Children.Add(after);

        var order = new[] { Visibility.Visible, Visibility.Hidden, Visibility.Collapsed };
        var index = 0;
        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-visibility-report");

        void Apply()
        {
            target.Visibility = order[index];
            status.Content = $"可见性: {DescribeVisibility(order[index])}";
        }

        var cycle = UiShowcaseWidgets.ActionButton("showcase-visibility-cycle", "循环可见性", () =>
        {
            index = (index + 1) % order.Length;
            Apply();
            report($"可见性: {DescribeVisibility(order[index])}");
        });

        Apply();

        var content = UiShowcaseWidgets.Column(host, cycle, status);
        return new UiShowcaseExample(
            "可见性三态",
            "循环 Visible、Hidden、Collapsed，观察隐藏保留占位、折叠移除占位。",
            content);
    }

    private static Panel CreateSlot(string id) => new()
    {
        StyleId = id,
        Width = 48,
        Height = 32,
        Background = new SolidColorBrush(0x4A, 0x3B, 0x6B)
    };

    private static string DescribeHorizontal(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Left => "左",
        HorizontalAlignment.Center => "中",
        HorizontalAlignment.Right => "右",
        _ => "拉伸"
    };

    private static string DescribeVertical(VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => "上",
        VerticalAlignment.Center => "中",
        VerticalAlignment.Bottom => "下",
        _ => "拉伸"
    };

    private static string DescribeOrientation(Orientation orientation) =>
        orientation == Orientation.Vertical ? "纵" : "横";

    private static string DescribeVisibility(Visibility visibility) => visibility switch
    {
        Visibility.Visible => "可见",
        Visibility.Hidden => "隐藏",
        _ => "折叠"
    };
}
