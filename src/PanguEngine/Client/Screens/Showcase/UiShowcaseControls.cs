using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds side-by-side control samples with direct interaction.
/// </summary>
internal static class UiShowcaseControls
{
    internal const string ButtonCounterId = "showcase-controls-button-counter";
    internal const string ButtonCountId = "showcase-controls-button-count";
    internal const string ButtonDisabledId = "showcase-controls-button-disabled";
    internal const string TextBoxEditorId = "showcase-controls-textbox-editor";
    internal const string TextBoxFeedbackId = "showcase-controls-textbox-feedback";

    internal static UiShowcaseExample[] CreateExamples(Action<string> report) =>
    [
        UiShowcaseStyles.CreateTypographyExample(),
        CreateImageExample(),
        CreateButtonExample(report),
        CreateTextBoxExample(),
        UiShowcaseTabs.CreateExample(),
        UiShowcaseScroll.CreateScrollBarExample(),
        UiShowcaseScroll.CreateScrollViewExample()
    ];

    private static UiShowcaseExample CreateImageExample()
    {
        var source = CreatePatternImage();
        var samples = new[] { ImageStretch.None, ImageStretch.Fill, ImageStretch.Uniform, ImageStretch.UniformToFill }
            .Select(mode => UiShowcaseWidgets.Sample(mode.ToString(), new ImageView
            {
                StyleId = $"showcase-controls-image-{mode}",
                Source = source,
                Stretch = mode,
                Width = 150,
                Height = 96
            })).ToArray();
        return new UiShowcaseExample(
            "图片",
            "同一张图片的原始尺寸、拉伸、等比适配与等比裁切。",
            UiShowcaseWidgets.Samples(samples));
    }

    private static UiShowcaseExample CreateButtonExample(Action<string> report)
    {
        var clicks = 0;
        var count = UiShowcaseWidgets.Label("点击次数：0", ButtonCountId);
        var primary = UiShowcaseWidgets.ActionButton(ButtonCounterId, "主要操作", () =>
        {
            count.Content = $"点击次数：{++clicks}";
            report($"按钮点击：{clicks} 次");
        });
        primary.Classes.Add("showcase-primary");
        var secondary = new Button { StyleId = "showcase-state-target", Text = "次要操作" };
        var outline = new Button { Text = "描边按钮" };
        outline.Classes.Add("showcase-outline");
        var danger = new Button { Text = "危险操作" };
        danger.Classes.Add("danger");
        var disabled = new Button { Text = "暂不可用", StyleId = ButtonDisabledId, IsEnabled = false };
        return new UiShowcaseExample(
            "按钮",
            "悬停、按住或聚焦按钮查看状态；主要操作记录点击次数。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("主要", primary),
                    UiShowcaseWidgets.Sample("次要", secondary),
                    UiShowcaseWidgets.Sample("描边", outline),
                    UiShowcaseWidgets.Sample("危险", danger),
                    UiShowcaseWidgets.Sample("禁用", disabled)), count,
                UiShowcaseWidgets.Caption("组合状态：让下方按钮同时悬停与聚焦，观察强调色。"),
                UiShowcaseStyles.CreateComboStateExample().Content));
    }

    private static UiShowcaseExample CreateTextBoxExample()
    {
        var editor = new TextBox { StyleId = TextBoxEditorId, Placeholder = "在此输入，预览会实时更新" };
        var feedback = UiShowcaseWidgets.Label(string.Empty, TextBoxFeedbackId);
        feedback.Bind(Text.ContentProperty, editor, TextBox.TextProperty,
            static text => text.Length <= 40
                ? $"当前文本：{text}"
                : $"当前文本：{text[..40]}…（共 {text.Length} 字符）");
        return new UiShowcaseExample(
            "文本框",
            "支持选择、复制、粘贴、撤销及常用编辑快捷键。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Sample("实时编辑", editor),
                UiShowcaseWidgets.Preview(feedback),
                UiShowcaseWidgets.Caption("编辑状态对比"),
                CreateStateExample().Content));
    }

    private static UiShowcaseExample CreateStateExample()
    {
        var states = new[]
        {
            ("editable", "可编辑", false, true),
            ("readonly", "只读", true, true),
            ("disabled", "禁用", false, false),
            ("readonly-disabled", "只读且禁用", true, false)
        };
        return new UiShowcaseExample(
            "只读与禁用",
            "直接比较各状态的外观、文本选择和编辑能力。",
            UiShowcaseWidgets.Samples(states.Select(state => UiShowcaseWidgets.Sample(state.Item2,
                new TextBox
                {
                    StyleId = $"showcase-controls-state-{state.Item1}",
                    Text = "状态示例",
                    IsReadOnly = state.Item3,
                    IsEnabled = state.Item4
                })).ToArray()));
    }

    private static UiImage CreatePatternImage()
    {
        const int width = 96;
        const int height = 60;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = (byte)(32 + x * 200 / width);
            pixels[offset + 1] = (byte)(32 + y * 200 / height);
            pixels[offset + 2] = (byte)((x / 12 + y / 12) % 2 == 0 ? 220 : 90);
            pixels[offset + 3] = 255;
        }
        return UiImage.FromRgba(pixels, width, height);
    }
}
