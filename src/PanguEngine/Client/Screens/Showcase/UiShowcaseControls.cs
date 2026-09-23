using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens;

internal static class UiShowcaseControls
{
    internal const string TextImageStretchButtonId = "showcase-controls-text-image-stretch";
    internal const string TextImageModeId = "showcase-controls-text-image-mode";
    internal const string TextImageTextButtonId = "showcase-controls-text-image-text-toggle";
    internal const string TextImageTextId = "showcase-controls-text-image-text";
    internal const string TextImageImageId = "showcase-controls-text-image-image";

    internal const string ButtonCounterId = "showcase-controls-button-counter";
    internal const string ButtonCountId = "showcase-controls-button-count";
    internal const string ButtonToggleEnabledId = "showcase-controls-button-toggle-enabled";
    internal const string ButtonStateId = "showcase-controls-button-state";

    internal const string TextBoxInstructionsId = "showcase-controls-textbox-instructions";
    internal const string TextBoxEditorId = "showcase-controls-textbox-editor";
    internal const string TextBoxReportId = "showcase-controls-textbox-report";
    internal const string TextBoxFeedbackId = "showcase-controls-textbox-feedback";

    internal const string StateEditorId = "showcase-controls-state-editor";
    internal const string StateToggleId = "showcase-controls-state-toggle";
    internal const string StateLabelId = "showcase-controls-state-label";

    private const int FeedbackLimit = 40;

    private static readonly ImageStretch[] StretchModes =
    [
        ImageStretch.None,
        ImageStretch.Fill,
        ImageStretch.Uniform,
        ImageStretch.UniformToFill
    ];

    private static readonly string[] SampleTexts =
    [
        "示例文本一",
        "示例文本二"
    ];

    private static readonly (bool IsReadOnly, bool IsEnabled, string Name)[] EditorStates =
    [
        (false, true, "可编辑"),
        (true, true, "只读"),
        (false, false, "禁用"),
        (true, false, "只读且禁用")
    ];

    internal static UiShowcaseExample[] CreateExamples(Action<string> report)
    {
        return
        [
            CreateTextImageExample(report),
            CreateButtonExample(report),
            CreateTextBoxExample(report),
            CreateStateExample(report)
        ];
    }

    private static UiShowcaseExample CreateTextImageExample(Action<string> report)
    {
        var imageView = new ImageView
        {
            StyleId = TextImageImageId,
            Source = CreatePatternImage(),
            Stretch = StretchModes[0],
            Width = 180,
            Height = 96
        };
        var modeLabel = UiShowcaseWidgets.Label(StretchModes[0].ToString(), TextImageModeId);
        var stretchButton = UiShowcaseWidgets.ActionButton(TextImageStretchButtonId, "切换拉伸", () =>
        {
            var index = (Array.IndexOf(StretchModes, imageView.Stretch) + 1) % StretchModes.Length;
            imageView.Stretch = StretchModes[index];
            modeLabel.Content = imageView.Stretch.ToString();
            report($"图片拉伸：{modeLabel.Content}");
        });

        var textIndex = 0;
        var sampleText = UiShowcaseWidgets.Label(SampleTexts[textIndex], TextImageTextId);
        var textButton = UiShowcaseWidgets.ActionButton(TextImageTextButtonId, "切换文本", () =>
        {
            textIndex = (textIndex + 1) % SampleTexts.Length;
            sampleText.Content = SampleTexts[textIndex];
            report($"示例文本：{sampleText.Content}");
        });

        var content = UiShowcaseWidgets.Column(
            imageView,
            UiShowcaseWidgets.Row(stretchButton, modeLabel),
            textButton,
            sampleText);
        return new UiShowcaseExample(
            "文本与图片",
            "切换短文本与 ImageView 拉伸模式，观察文本重布局和图片比例变化。",
            content);
    }

    private static UiShowcaseExample CreateButtonExample(Action<string> report)
    {
        var clicks = 0;
        var countLabel = UiShowcaseWidgets.Label("点击次数：0", ButtonCountId);
        var targetButton = UiShowcaseWidgets.ActionButton(ButtonCounterId, "点击计数", () =>
        {
            clicks++;
            countLabel.Content = $"点击次数：{clicks}";
            report($"按钮点击：{clicks} 次");
        });

        var targetEnabled = true;
        var stateLabel = UiShowcaseWidgets.Label("状态：启用", ButtonStateId);
        var toggleButton = UiShowcaseWidgets.ActionButton(ButtonToggleEnabledId, "切换启用", () =>
        {
            targetEnabled = !targetEnabled;
            targetButton.IsEnabled = targetEnabled;
            stateLabel.Content = targetEnabled ? "状态：启用" : "状态：禁用";
            report(stateLabel.Content);
        });

        var content = UiShowcaseWidgets.Column(
            targetButton,
            countLabel,
            toggleButton,
            stateLabel);
        return new UiShowcaseExample(
            "按钮",
            "点击普通按钮使计数递增；切换按钮可禁用和恢复目标按钮。",
            content);
    }

    private static UiShowcaseExample CreateTextBoxExample(Action<string> report)
    {
        var editor = new TextBox
        {
            StyleId = TextBoxEditorId,
            Placeholder = "在此输入文本",
            Width = 360
        };
        var feedback = UiShowcaseWidgets.Label("反馈会截断过长的文本。", TextBoxFeedbackId);
        var reportButton = UiShowcaseWidgets.ActionButton(TextBoxReportId, "显示当前文本", () =>
        {
            var summary = Summarize(editor.Text);
            feedback.Content = summary;
            report(summary);
        });
        var instructions = UiShowcaseWidgets.Label(
            "支持 Ctrl+A/C/X/V/Z/Y、方向键、Home/End、Backspace/Delete。",
            TextBoxInstructionsId);

        var content = UiShowcaseWidgets.Column(
            instructions,
            editor,
            reportButton,
            feedback);
        return new UiShowcaseExample(
            "文本框编辑",
            "输入、选择、删除并使用现有快捷键，反馈只显示有界的短文本。",
            content);
    }

    private static UiShowcaseExample CreateStateExample(Action<string> report)
    {
        var editor = new TextBox
        {
            StyleId = StateEditorId,
            Text = "状态示例",
            Width = 360
        };
        var stateIndex = 0;
        var stateLabel = UiShowcaseWidgets.Label(DescribeState(0), StateLabelId);
        var toggleButton = UiShowcaseWidgets.ActionButton(StateToggleId, "切换状态", () =>
        {
            stateIndex = (stateIndex + 1) % EditorStates.Length;
            var state = EditorStates[stateIndex];
            editor.IsReadOnly = state.IsReadOnly;
            editor.IsEnabled = state.IsEnabled;
            stateLabel.Content = DescribeState(stateIndex);
            report($"文本框状态：{state.Name}");
        });

        var content = UiShowcaseWidgets.Column(
            editor,
            toggleButton,
            stateLabel);
        return new UiShowcaseExample(
            "只读与禁用",
            "切换只读和禁用，观察可编辑性与默认外观，恢复后可继续编辑。",
            content);
    }

    private static string DescribeState(int index)
    {
        var state = EditorStates[index];
        return $"状态：{state.Name}（只读={state.IsReadOnly}，启用={state.IsEnabled}）";
    }

    private static string Summarize(string text) =>
        text.Length <= FeedbackLimit
            ? $"当前文本：{text}"
            : $"当前文本：{text[..FeedbackLimit]}…（共 {text.Length} 字符）";

    private static UiImage CreatePatternImage()
    {
        const int width = 8;
        const int height = 5;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                pixels[offset] = (byte)(32 + x * 28);
                pixels[offset + 1] = (byte)(32 + y * 44);
                pixels[offset + 2] = (byte)((x + y) % 2 == 0 ? 220 : 90);
                pixels[offset + 3] = 255;
            }
        }

        return UiImage.FromRgba(pixels, width, height);
    }
}
