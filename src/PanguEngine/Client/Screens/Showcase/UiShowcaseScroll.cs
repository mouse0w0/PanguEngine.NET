using System.Globalization;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds directly scrollable samples for scroll bar variants and view policies.
/// </summary>
internal static class UiShowcaseScroll
{
    internal static UiShowcaseExample CreateScrollBarExample()
    {
        var arrows = CreateBar("showcase-scrollbar-arrows", Orientation.Horizontal);
        var minimal = CreateBar("showcase-scrollbar-minimal", Orientation.Horizontal);
        minimal.ShowArrows = false;
        var disabled = CreateBar("showcase-scrollbar-disabled", Orientation.Horizontal);
        disabled.IsEnabled = false;
        var vertical = CreateBar("showcase-scrollbar-vertical", Orientation.Vertical);
        var verticalMinimal = CreateBar("showcase-scrollbar-vertical-minimal", Orientation.Vertical);
        verticalMinimal.ShowArrows = false;
        var verticalDisabled = CreateBar("showcase-scrollbar-vertical-disabled", Orientation.Vertical);
        verticalDisabled.IsEnabled = false;
        return new UiShowcaseExample(
            "滚动条",
            "拖动滑块、点击轨道或长按箭头；聚焦后可用方向键、PageUp/Down、Home/End。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Caption("横向滚动条"),
                UiShowcaseWidgets.Samples(
                    CreateBarSample("带箭头", "支持箭头步进与轨道翻页。", arrows, "showcase-scrollbar-value"),
                    CreateBarSample("简洁", "隐藏箭头，直接拖动滑块。", minimal),
                    CreateBarSample("禁用", "显示禁用外观，不响应输入。", disabled)),
                UiShowcaseWidgets.Caption("纵向滚动条"),
                UiShowcaseWidgets.Samples(
                    CreateBarSample("带箭头", "支持上下步进与轨道翻页。", vertical, "showcase-scrollbar-vertical-value"),
                    CreateBarSample("简洁", "隐藏箭头，直接拖动滑块。", verticalMinimal),
                    CreateBarSample("禁用", "显示禁用外观，不响应输入。", verticalDisabled))));
    }

    private static StackPanel CreateBarSample(string title, string description, ScrollBar bar, string? valueId = null)
    {
        var value = UiShowcaseWidgets.Caption(string.Empty);
        value.StyleId = valueId;
        value.Bind(Text.ContentProperty, bar, ScrollBar.ValueProperty,
            static number => string.Create(CultureInfo.InvariantCulture, $"当前位置：{number:0.##}"));
        return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption(title), bar,
            UiShowcaseWidgets.Caption(description), value);
    }

    internal static UiShowcaseExample CreateScrollViewExample()
    {
        var policies = new[] { ScrollBarVisibility.Auto, ScrollBarVisibility.Visible, ScrollBarVisibility.Hidden, ScrollBarVisibility.Disabled };
        return new UiShowcaseExample(
            "滚动视图",
            "比较自动、常驻、隐藏滚动条及禁用滚动；滚动各视图互不影响。",
            UiShowcaseWidgets.Samples(policies.Select(policy =>
            {
                var view = new ScrollView
                {
                    StyleId = $"showcase-scrollview-{policy}",
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = policy,
                    ShowArrows = false,
                    Height = 144,
                    Content = CreateScrollContent()
                };
                view.Classes.Add("showcase-scroll-view");
                var offset = UiShowcaseWidgets.Caption(string.Empty);
                offset.Bind(Text.ContentProperty, view, ScrollView.OffsetProperty,
                    static point => string.Create(CultureInfo.InvariantCulture, $"滚动位置：{point.Y:0.#}"));
                return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption(policy.ToString()), view, offset);
            }).ToArray()));
    }

    private static ScrollBar CreateBar(string id, Orientation orientation)
    {
        var bar = new ScrollBar
        {
            StyleId = id,
            Orientation = orientation,
            ViewportSize = 40,
            SmallChange = 5,
            LargeChange = 25,
            Value = 40
        };
        bar.Classes.Add(orientation == Orientation.Horizontal ? "showcase-scroll-horizontal" : "showcase-scroll-vertical");
        return bar;
    }

    private static StackPanel CreateScrollContent()
    {
        var content = UiShowcaseWidgets.Column();
        content.Spacing = 4;
        content.Padding = new Thickness(8);
        for (var number = 1; number <= 12; number++)
        {
            var row = UiShowcaseWidgets.Column(UiShowcaseWidgets.Label($"条目 {number:00}"));
            row.Classes.Add("showcase-scroll-row");
            if (number % 2 == 0)
                row.Classes.Add("showcase-scroll-row-alternate");
            content.Children.Add(row);
        }
        return content;
    }
}
