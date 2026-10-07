using System.Globalization;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens;

internal static class UiShowcaseScroll
{
    internal const string HorizontalBarId = "showcase-scrollbar-horizontal";
    internal const string VerticalBarId = "showcase-scrollbar-vertical";
    internal const string HorizontalValueId = "showcase-scrollbar-horizontal-value";
    internal const string VerticalValueId = "showcase-scrollbar-vertical-value";
    internal const string BarArrowsId = "showcase-scrollbar-arrows";
    internal const string BarPresetId = "showcase-scrollbar-preset";
    internal const string BarResetId = "showcase-scrollbar-reset";

    internal const string ViewId = "showcase-scrollview-view";
    internal const string HorizontalPolicyId = "showcase-scrollview-horizontal-policy";
    internal const string VerticalPolicyId = "showcase-scrollview-vertical-policy";
    internal const string ViewArrowsId = "showcase-scrollview-arrows";
    internal const string ViewScrollId = "showcase-scrollview-scroll";
    internal const string ViewResetId = "showcase-scrollview-reset";
    internal const string OffsetId = "showcase-scrollview-offset";
    internal const string ExtentId = "showcase-scrollview-extent";
    internal const string ViewportId = "showcase-scrollview-viewport";

    private static readonly ScrollBarVisibility[] VisibilityModes =
    [
        ScrollBarVisibility.Auto,
        ScrollBarVisibility.Visible,
        ScrollBarVisibility.Hidden,
        ScrollBarVisibility.Disabled
    ];

    internal static UiShowcaseExample CreateScrollBarExample(Action<string> report)
    {
        var horizontal = CreateBar(HorizontalBarId, Orientation.Horizontal, "showcase-scroll-horizontal");
        var vertical = CreateBar(VerticalBarId, Orientation.Vertical, "showcase-scroll-vertical");
        var horizontalValue = CreateDiagnostic(HorizontalValueId);
        var verticalValue = CreateDiagnostic(VerticalValueId);
        horizontalValue.Bind(Text.ContentProperty, horizontal, ScrollBar.ValueProperty,
            static value => string.Create(CultureInfo.InvariantCulture, $"横向位置：{value:0.##}"));
        verticalValue.Bind(Text.ContentProperty, vertical, ScrollBar.ValueProperty,
            static value => string.Create(CultureInfo.InvariantCulture, $"纵向位置：{value:0.##}"));

        var arrows = UiShowcaseWidgets.ActionButton(BarArrowsId, "隐藏箭头", () =>
        {
            var shown = !horizontal.ShowArrows;
            horizontal.ShowArrows = shown;
            vertical.ShowArrows = shown;
            report(shown ? "滚动条：显示箭头" : "滚动条：隐藏箭头并释放占位");
        });
        arrows.Bind(Button.TextProperty, horizontal, ScrollBar.ShowArrowsProperty,
            static shown => shown ? "隐藏箭头" : "显示箭头");

        var preset = UiShowcaseWidgets.ActionButton(BarPresetId, "位置 80", () =>
        {
            horizontal.Value = 80;
            vertical.Value = 80;
            report("滚动条：横向与纵向位置设为 80");
        });
        var reset = UiShowcaseWidgets.ActionButton(BarResetId, "重置", () =>
        {
            horizontal.ShowArrows = true;
            vertical.ShowArrows = true;
            horizontal.Value = 40;
            vertical.Value = 40;
            report("滚动条：恢复箭头和初始位置 40");
        });

        var keyboardHint = UiShowcaseWidgets.Label("聚焦后使用方向键、PageUp/Down、Home/End。");
        keyboardHint.Classes.Add("showcase-scroll-diagnostic");
        var demo = UiShowcaseWidgets.Column(
            horizontal,
            UiShowcaseWidgets.Row(horizontalValue, verticalValue),
            CreateActionRow(arrows, preset, reset),
            keyboardHint);
        demo.Classes.Add("showcase-scroll-bar-demo");

        return new UiShowcaseExample(
            "滚动条",
            "拖动滑块；点击或长按箭头、轨道；滚轮纵向，Shift+滚轮横向。范围 0–100，单步 5、翻页 25。",
            UiShowcaseWidgets.Row(vertical, demo));
    }

    internal static UiShowcaseExample CreateScrollViewExample(Action<string> report)
    {
        var view = new ScrollView
        {
            StyleId = ViewId,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = CreateScrollContent()
        };
        view.Classes.Add("showcase-scroll-view");

        var offset = CreateDiagnostic(OffsetId);
        var extent = CreateDiagnostic(ExtentId);
        var viewport = CreateDiagnostic(ViewportId);
        offset.Bind(Text.ContentProperty, view, ScrollView.OffsetProperty,
            static value => string.Create(CultureInfo.InvariantCulture, $"偏移：{value.X:0.#}, {value.Y:0.#}"));
        extent.Bind(Text.ContentProperty, view, ScrollView.ExtentProperty,
            static value => string.Create(CultureInfo.InvariantCulture, $"内容：{value.Width:0.#} × {value.Height:0.#}"));
        viewport.Bind(Text.ContentProperty, view, ScrollView.ViewportProperty,
            static value => string.Create(CultureInfo.InvariantCulture, $"视口：{value.Width:0.#} × {value.Height:0.#}"));

        var horizontalPolicy = UiShowcaseWidgets.ActionButton(HorizontalPolicyId, "横向：Auto", () =>
        {
            view.HorizontalScrollBarVisibility = NextVisibility(view.HorizontalScrollBarVisibility);
            report($"滚动视图：横向策略 {view.HorizontalScrollBarVisibility}");
        });
        horizontalPolicy.Bind(Button.TextProperty, view, ScrollView.HorizontalScrollBarVisibilityProperty,
            static value => $"横向：{value}");

        var verticalPolicy = UiShowcaseWidgets.ActionButton(VerticalPolicyId, "纵向：Auto", () =>
        {
            view.VerticalScrollBarVisibility = NextVisibility(view.VerticalScrollBarVisibility);
            report($"滚动视图：纵向策略 {view.VerticalScrollBarVisibility}");
        });
        verticalPolicy.Bind(Button.TextProperty, view, ScrollView.VerticalScrollBarVisibilityProperty,
            static value => $"纵向：{value}");

        var arrows = UiShowcaseWidgets.ActionButton(ViewArrowsId, "隐藏箭头", () =>
        {
            view.ShowArrows = !view.ShowArrows;
            report(view.ShowArrows ? "滚动视图：显示箭头" : "滚动视图：隐藏箭头");
        });
        arrows.Bind(Button.TextProperty, view, ScrollView.ShowArrowsProperty,
            static shown => shown ? "隐藏箭头" : "显示箭头");

        var scroll = UiShowcaseWidgets.ActionButton(ViewScrollId, "滚动 +120, +80", () =>
        {
            view.ScrollBy(120, 80);
            report(string.Create(CultureInfo.InvariantCulture,
                $"滚动视图：当前位置 {view.Offset.X:0.#}, {view.Offset.Y:0.#}"));
        });
        var reset = UiShowcaseWidgets.ActionButton(ViewResetId, "重置", () =>
        {
            view.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            view.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            view.ShowArrows = true;
            view.ScrollTo(0, 0);
            report("滚动视图：恢复双轴 Auto、箭头和起点");
        });

        return new UiShowcaseExample(
            "滚动视图",
            "滚轮纵向、Shift+滚轮横向；点击内容后可用方向键、PageUp/Down、Home/End。策略按 Auto/Visible/Hidden/Disabled 循环。",
            UiShowcaseWidgets.Column(
                view,
                CreateActionRow(horizontalPolicy, verticalPolicy, arrows),
                CreateActionRow(scroll, reset),
                UiShowcaseWidgets.Row(offset, extent, viewport)));
    }

    private static ScrollBar CreateBar(string id, Orientation orientation, string cssClass)
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
        bar.Classes.Add(cssClass);
        return bar;
    }

    private static ScrollBarVisibility NextVisibility(ScrollBarVisibility current) =>
        VisibilityModes[(Array.IndexOf(VisibilityModes, current) + 1) % VisibilityModes.Length];

    private static StackPanel CreateScrollContent()
    {
        var content = new StackPanel { Spacing = 4 };
        content.Classes.Add("showcase-scroll-content");
        for (var number = 1; number <= 16; number++)
        {
            var index = UiShowcaseWidgets.Label($"行 {number:00}");
            index.Classes.Add("showcase-scroll-number");
            var message = UiShowcaseWidgets.Label($"示例内容 {number:00}：纵向滚动查看更多行，横向滚动查看右侧标记。");
            message.Classes.Add("showcase-scroll-message");
            var marker = UiShowcaseWidgets.Label($"右端 {number:00}");
            marker.Classes.Add("showcase-scroll-marker");
            var row = UiShowcaseWidgets.Row(index, message, marker);
            row.Classes.Add("showcase-scroll-row");
            if (number % 2 == 0)
                row.Classes.Add("showcase-scroll-row-alternate");
            content.Children.Add(row);
        }

        return content;
    }

    private static Text CreateDiagnostic(string id)
    {
        var text = UiShowcaseWidgets.Label(string.Empty, id);
        text.Classes.Add("showcase-scroll-diagnostic");
        return text;
    }

    private static StackPanel CreateActionRow(params UiNode[] actions)
    {
        var row = UiShowcaseWidgets.Row(actions);
        row.Classes.Add("showcase-scroll-actions");
        return row;
    }
}
