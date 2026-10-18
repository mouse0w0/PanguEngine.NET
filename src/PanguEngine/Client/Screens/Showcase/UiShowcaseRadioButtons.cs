using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Client.Screens.Showcase;

internal static class UiShowcaseRadioButtons
{
    internal static UiShowcaseExample CreateExample(Action<string> report)
    {
        var settings = new DisplaySettings { Mode = DisplayMode.Windowed };
        var group = new RadioGroup<DisplayMode?>();
        var windowed = new RadioButton<DisplayMode?>
        {
            Group = group, Value = DisplayMode.Windowed, Content = "窗口"
        };
        var borderless = new RadioButton<DisplayMode?>
        {
            Group = group, Value = DisplayMode.Borderless, Content = new ModeLabel("无边框")
        };
        var fullscreen = new RadioButton<DisplayMode?>
        {
            Group = group, Value = DisplayMode.Fullscreen, Content = CreateDetailedContent()
        };
        var disabled = new RadioButton<DisplayMode?>
        {
            Group = group, Value = DisplayMode.Exclusive, Content = "独占模式（暂不可用）", IsEnabled = false
        };
        group.BindTwoWay(RadioGroup<DisplayMode?>.SelectedValueProperty, settings, DisplaySettings.ModeProperty);
        var feedback = UiShowcaseWidgets.Label(string.Empty);
        feedback.Bind(Text.ContentProperty, settings, DisplaySettings.ModeProperty, static mode => mode switch
        {
            DisplayMode.Windowed => "当前模式：窗口",
            DisplayMode.Borderless => "当前模式：无边框",
            DisplayMode.Fullscreen => "当前模式：全屏",
            DisplayMode.Exclusive => "当前模式：独占模式",
            _ => "当前模式：未选择"
        });

        var independent = new RadioGroup<string>();
        var indicatorOnly = new RadioButton<string> { Group = independent, Value = "indicator" };
        var interactive = new RadioButton<string>
        {
            Group = independent,
            Value = "interactive",
            Content = UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Label("自定义内容"),
                UiShowcaseWidgets.ActionButton("showcase-radio-inner-action", "内容中的操作", () =>
                    report("已执行内容中的操作，单选选择保持原值")))
        };
        var independentFeedback = UiShowcaseWidgets.Label(string.Empty);
        independentFeedback.Bind(Text.ContentProperty, independent, RadioGroup<string>.SelectedValueProperty,
            static value => value switch
            {
                "indicator" => "独立组：仅指示器",
                "interactive" => "独立组：自定义内容",
                _ => "独立组：未选择"
            });

        return new UiShowcaseExample(
            "单选按钮",
            "点击选项或聚焦后使用 Space、Enter 和方向键。同组成员跨布局互斥；内容中的按钮独立响应。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("显示模式", UiShowcaseWidgets.Column(windowed, borderless)),
                    UiShowcaseWidgets.Sample("同组的其他布局", UiShowcaseWidgets.Column(fullscreen, disabled)),
                    UiShowcaseWidgets.Sample("独立组", UiShowcaseWidgets.Column(indicatorOnly, interactive))),
                UiShowcaseWidgets.Preview(feedback, independentFeedback),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.ActionButton("showcase-radio-program-select", "程序选择全屏", () =>
                        settings.Mode = DisplayMode.Fullscreen),
                    UiShowcaseWidgets.ActionButton("showcase-radio-clear", "清空显示模式", group.Clear))));
    }

    private static StackPanel CreateDetailedContent()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new Ellipse
        {
            Width = 14, Height = 14, Fill = new SolidColorBrush(84, 169, 255), IsHitTestVisible = false
        });
        row.Children.Add(UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Label("全屏"), UiShowcaseWidgets.Caption("图形与说明文字组成的内容")));
        return row;
    }

    private enum DisplayMode
    {
        Windowed,
        Borderless,
        Fullscreen,
        Exclusive
    }

    private sealed record ModeLabel(string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class DisplaySettings : ObservableObject
    {
        internal static readonly DirectProperty<DisplaySettings, DisplayMode?> ModeProperty =
            Property.RegisterDirect<DisplaySettings, DisplayMode?>(nameof(Mode),
                static settings => settings.Mode, static (settings, value) => settings.Mode = value);

        private DisplayMode? _mode;

        internal DisplayMode? Mode
        {
            get => _mode;
            set => SetField(ModeProperty, ref _mode, value);
        }
    }
}
