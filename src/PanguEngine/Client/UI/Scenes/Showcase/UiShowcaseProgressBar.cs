using PanguEngine.UI.Controls;

namespace PanguEngine.Client.UI.Scenes.Showcase;

/// <summary>Builds progress samples with numeric binding and direction comparisons.</summary>
internal static class UiShowcaseProgressBar
{
    /// <summary>Creates the interactive progress bar showcase page.</summary>
    /// <returns>The progress bar example.</returns>
    internal static UiShowcaseExample CreateExample()
    {
        var editor = new NumericUpDown<double>
        {
            StyleId = "showcase-progress-value", Minimum = -50, Maximum = 150, Increment = 5, Value = 40
        };
        var reverse = new ToggleSwitch { StyleId = "showcase-progress-reversed" };
        var interactive = BoundBar(editor, Orientation.Horizontal, false);
        interactive.Bind(ProgressBar.IsReversedProperty, reverse, ToggleSwitch.IsOnProperty);
        var value = UiShowcaseWidgets.Caption(string.Empty);
        value.Bind(Text.ContentProperty, editor, NumericUpDown<double>.ValueProperty,
            static current => $"原始值：{current:0.##}；进度条范围：0～100，越界仅截断显示。");

        var accent = new ProgressBar { Value = 65 };
        accent.Classes.Add("showcase-progress-accent");
        return new UiShowcaseExample(
            "进度条",
            "进度条只负责显示。使用数值输入与反向开关调整原值和填充方向；文字由外部 Text 组合。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Caption("确定进度与原始值"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("空：0 / 100", new ProgressBar()),
                    UiShowcaseWidgets.Sample("部分：40 / 100", new ProgressBar { Value = 40 }),
                    UiShowcaseWidgets.Sample("满：100 / 100", new ProgressBar { Value = 100 }),
                    UiShowcaseWidgets.Sample("原值 150，显示满条", new ProgressBar { Value = 150 })),
                UiShowcaseWidgets.Preview(
                    UiShowcaseWidgets.Samples(
                        UiShowcaseWidgets.Sample("原值：-50～150", editor),
                        UiShowcaseWidgets.Sample("反向填充", reverse)),
                    interactive, value),
                UiShowcaseWidgets.Caption("四种方向：共享上方原值，比较填充起点"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("水平：向右", BoundBar(editor, Orientation.Horizontal, false)),
                    UiShowcaseWidgets.Sample("水平反向：向左", BoundBar(editor, Orientation.Horizontal, true)),
                    UiShowcaseWidgets.Sample("垂直：向上", BoundBar(editor, Orientation.Vertical, false)),
                    UiShowcaseWidgets.Sample("垂直反向：向下", BoundBar(editor, Orientation.Vertical, true))),
                UiShowcaseWidgets.Caption("状态与 CSS 配色"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("禁用确定进度", new ProgressBar { Value = 60, IsEnabled = false }),
                    UiShowcaseWidgets.Sample("自定义 CSS", accent))));
    }

    private static ProgressBar BoundBar(NumericUpDown<double> source, Orientation orientation,
        bool reversed)
    {
        var bar = new ProgressBar { Orientation = orientation, IsReversed = reversed };
        bar.Bind(ProgressBar.ValueProperty, source, NumericUpDown<double>.ValueProperty);
        return bar;
    }
}
