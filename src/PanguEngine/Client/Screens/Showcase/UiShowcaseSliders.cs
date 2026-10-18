using PanguEngine.Client.UI;
using PanguEngine.Collections;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>Builds interactive samples for numeric ranges and mutable slider choices.</summary>
internal static class UiShowcaseSliders
{
    internal static UiShowcaseExample CreateExample(Action<string> report)
    {
        var volume = new Slider<double>(new NumericSliderValueModel<double>
        {
            Maximum = 1, SmallChange = .01, LargeChange = .1, TickFrequency = .25
        })
        {
            Value = .65, ShowTicks = true, ShowTickLabels = true, ValueFormatter = value => $"{value:P0}"
        };
        var distance = new Slider<int>(new NumericSliderValueModel<int>
        {
            Minimum = 2, Maximum = 32, Step = 2, TickFrequency = 6
        })
        {
            Value = 16, ShowTicks = true, ShowTickLabels = true, ValueFormatter = value => $"{value} 区块"
        };
        var vertical = new Slider<double>(new NumericSliderValueModel<double>
        {
            Maximum = 1, Step = .1, SmallChange = .1, LargeChange = .3, TickFrequency = .25
        })
        {
            Value = .5, Orientation = Orientation.Vertical, Height = 180,
            HorizontalAlignment = HorizontalAlignment.Left, ShowTicks = true, ShowTickLabels = true,
            ValueFormatter = value => $"{value:P0}"
        };
        var disabled = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 45, IsEnabled = false };
        var frameRate = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>([30, 60, 120, 144])))
        {
            Value = 60, ShowTicks = true, ShowTickLabels = true, ValueFormatter = value => $"{value} 帧"
        };
        var quality = new Slider<string>(new ListSliderValueModel<string>(new ObservableList<string>(["低", "中", "高", "极高"])))
        {
            Value = "高", ShowTicks = true, ShowTickLabels = true
        };
        var presets = new ObservableList<GraphicsPreset>([
            new("节能", 8), new("均衡", 16), new("清晰", 24), new("极致", 32)]);
        var preset = new Slider<GraphicsPreset>(new ListSliderValueModel<GraphicsPreset>(presets))
        {
            Value = presets[1], ShowTicks = true, ShowTickLabels = true, ValueFormatter = value => value.Name
        };
        var displayOptions = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton("showcase-slider-toggle-ticks", "切换刻度线", () => volume.ShowTicks = !volume.ShowTicks),
            UiShowcaseWidgets.ActionButton("showcase-slider-toggle-labels", "切换刻度文字", () => volume.ShowTickLabels = !volume.ShowTickLabels));

        return new UiShowcaseExample("滑条",
            "点击轨道或拖动滑块实时调整；聚焦后使用方向键、PageUp/Down、Home/End。滚轮用于滚动页面。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Caption("数值范围"),
                UiShowcaseWidgets.Samples(
                    Sample("连续音量", volume, report), Sample("整数视距", distance, report),
                    Sample("垂直亮度", vertical, report), Sample("禁用", disabled, report)),
                displayOptions,
                UiShowcaseWidgets.Caption("等距候选项"),
                UiShowcaseWidgets.Samples(
                    Sample("帧率", frameRate, report), Sample("画质", quality, report), Sample("对象预设", preset, report)),
                CreateBindingSample(report), CreateAssignmentSample(report), CreateMutableListSample(report)));
    }

    private static StackPanel Sample<T>(string title, Slider<T> slider, Action<string> report)
    {
        var value = UiShowcaseWidgets.Caption(string.Empty);
        void Update() => value.Content = slider.TryGetValue(out var selected)
            ? $"当前值：{slider.ValueFormatter(selected)}" : "无可选值";
        slider.Subscribe(Slider<T>.ValueProperty, (_, _) => Update());
        slider.Subscribe(Slider<T>.HasValueProperty, (_, _) => Update());
        slider.Subscribe(Slider<T>.ValueFormatterProperty, (_, _) => Update());
        slider.ValueCommitted += (_, change) => report($"{title}：{slider.ValueFormatter(change.StartValue)} → {slider.ValueFormatter(change.EndValue)}");
        Update();
        return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption(title), slider, value);
    }

    private static StackPanel CreateBindingSample(Action<string> report)
    {
        var source = new Slider<int>(new NumericSliderValueModel<int> { TickFrequency = 25 })
        {
            StyleId = "showcase-slider-binding-source", Value = 40, ShowTicks = true, ShowTickLabels = true
        };
        var peer = new Slider<int>(new NumericSliderValueModel<int> { TickFrequency = 25 })
        {
            StyleId = "showcase-slider-binding-peer", ShowTicks = true, ShowTickLabels = true
        };
        peer.BindTwoWay(Slider<int>.ValueProperty, source, Slider<int>.ValueProperty);
        return UiShowcaseWidgets.Preview(
            UiShowcaseWidgets.Caption("共享数值"),
            UiShowcaseWidgets.Caption("拖动任意滑条或用键盘调整，另一个滑条会同步变化。"),
            UiShowcaseWidgets.Samples(Sample("滑条 A", source, report), Sample("滑条 B", peer, report)));
    }

    private static StackPanel CreateAssignmentSample(Action<string> report)
    {
        var model = new NumericSliderValueModel<double> { Minimum = 2, Maximum = 32, Step = 2, TickFrequency = 6 };
        var slider = new Slider<double>(model) { Value = 16, ShowTicks = true, ShowTickLabels = true };
        var feedback = UiShowcaseWidgets.Caption("保留范围内的程序赋值；鼠标与键盘始终按步长调整。");
        void SetPolicy(SliderValueAssignmentPolicy policy, string text)
        {
            model.AssignmentPolicy = policy;
            feedback.Content = text;
        }
        var policies = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton("showcase-slider-clamp", "保留非步进值", () => SetPolicy(SliderValueAssignmentPolicy.Clamp, "程序赋值限制范围，但保留非步进值。")),
            UiShowcaseWidgets.ActionButton("showcase-slider-snap", "吸附程序赋值", () => SetPolicy(SliderValueAssignmentPolicy.Snap, "程序赋值吸附到最近步进点，等距时取较大值。")),
            UiShowcaseWidgets.ActionButton("showcase-slider-reject", "拒绝非步进值", () => SetPolicy(SliderValueAssignmentPolicy.Reject, "非法程序赋值失败，保留原值。")),
            UiShowcaseWidgets.ActionButton("showcase-slider-assign-off-step", "设置 2.5", () =>
            {
                try
                {
                    slider.Value = 2.5;
                    feedback.Content = $"设置 2.5 后：{slider.Value:0.##}";
                }
                catch (ArgumentException)
                {
                    feedback.Content = $"2.5 被拒绝，当前仍为 {slider.Value:0.##}";
                }
            }));
        return UiShowcaseWidgets.Preview(Sample("程序赋值策略", slider, report), policies, feedback);
    }

    private static StackPanel CreateMutableListSample(Action<string> report)
    {
        var items = new ObservableList<int>([30, 60, 120, 144]);
        var slider = new Slider<int>(new ListSliderValueModel<int>(items))
        {
            Value = 60, ShowTicks = true, ShowTickLabels = true, ValueFormatter = value => $"{value} 帧"
        };
        var feedback = UiShowcaseWidgets.Caption(string.Empty);
        void UpdateItems() => feedback.Content = items.Count == 0 ? "候选项为空，滑块暂不可用。" : $"候选项：{string.Join("、", items)}";
        items.Changed += (_, _) => UpdateItems();
        UpdateItems();
        var actions = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton("showcase-slider-list-add", "新增 90 帧", () =>
            {
                if (!items.Contains(90))
                    items.Add(90);
            }),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-remove", "删除当前项", () =>
            {
                if (slider.TryGetValue(out var value))
                    items.RemoveAt(items.IndexOf(value));
            }),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-replace", "当前项改为 165", () =>
            {
                if (!items.Contains(165) && slider.TryGetValue(out var value))
                    items[items.IndexOf(value)] = 165;
            }),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-reverse", "反转顺序", items.Reverse),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-clear", "清空", items.Clear),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-reset", "恢复候选项", () => items.ReplaceAll([30, 60, 120, 144])),
            UiShowcaseWidgets.ActionButton("showcase-slider-list-invalid", "尝试设置 75 帧", () =>
            {
                try
                {
                    slider.Value = 75;
                }
                catch (ArgumentException)
                {
                    feedback.Content = "75 不属于候选项，原选择保留。";
                }
                catch (InvalidOperationException)
                {
                    feedback.Content = "候选项为空，无法设置值。";
                }
            }));
        return UiShowcaseWidgets.Preview(Sample("动态帧率列表", slider, report), feedback, actions);
    }

    private sealed record GraphicsPreset(string Name, int ViewDistance);
}
