using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class UiShowcaseBindingTests
{
    [Fact]
    public void ControlsImageExampleCyclesAllStretchModesAndUpdatesLabel()
    {
        using var context = new UiTextTestContext();
        var reports = new List<string>();
        var example = UiShowcaseControls.CreateExamples(reports.Add)[0];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var image = Find<ImageView>(example.Content, UiShowcaseControls.TextImageImageId);
        var mode = Find<Text>(example.Content, UiShowcaseControls.TextImageModeId);
        var stretchButton = Find<Button>(example.Content, UiShowcaseControls.TextImageStretchButtonId);

        var source = Assert.IsType<UiImage>(image.Source);
        Assert.Equal(8, source.PixelWidth);
        Assert.Equal(5, source.PixelHeight);
        Assert.Equal(ImageStretch.None, image.Stretch);
        Assert.Equal(ImageStretch.None.ToString(), mode.Content);

        var expected = new[]
        {
            ImageStretch.Fill,
            ImageStretch.Uniform,
            ImageStretch.UniformToFill,
            ImageStretch.None
        };
        foreach (var stretch in expected)
        {
            host.Click(stretchButton);
            Assert.Equal(stretch, image.Stretch);
            Assert.Equal(stretch.ToString(), mode.Content);
        }

        Assert.Equal(4, reports.Count);
        Assert.Equal($"图片拉伸：{ImageStretch.None}", reports[^1]);
    }

    [Fact]
    public void ControlsImageExampleTogglesShortText()
    {
        using var context = new UiTextTestContext();
        var example = UiShowcaseControls.CreateExamples(_ => { })[0];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var sampleText = Find<Text>(example.Content, UiShowcaseControls.TextImageTextId);
        var textButton = Find<Button>(example.Content, UiShowcaseControls.TextImageTextButtonId);
        var initial = sampleText.Content;

        host.Click(textButton);
        Assert.NotEqual(initial, sampleText.Content);
        Assert.False(string.IsNullOrEmpty(sampleText.Content));

        host.Click(textButton);
        Assert.Equal(initial, sampleText.Content);
    }

    [Fact]
    public void ControlsButtonExampleCountsClicksAndRespectsDisabledState()
    {
        using var context = new UiTextTestContext();
        var example = UiShowcaseControls.CreateExamples(_ => { })[1];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var counter = Find<Button>(example.Content, UiShowcaseControls.ButtonCounterId);
        var countLabel = Find<Text>(example.Content, UiShowcaseControls.ButtonCountId);
        var toggle = Find<Button>(example.Content, UiShowcaseControls.ButtonToggleEnabledId);
        var stateLabel = Find<Text>(example.Content, UiShowcaseControls.ButtonStateId);

        host.Click(counter);
        host.Click(counter);
        Assert.Equal("点击次数：2", countLabel.Content);

        host.Click(toggle);
        Assert.False(counter.IsEnabled);
        Assert.Equal("状态：禁用", stateLabel.Content);

        host.Click(counter);
        Assert.Equal("点击次数：2", countLabel.Content);

        host.Click(toggle);
        Assert.True(counter.IsEnabled);
        host.Click(counter);
        Assert.Equal("点击次数：3", countLabel.Content);
    }

    [Fact]
    public void ControlsTextBoxExampleReportsBoundedFeedback()
    {
        using var context = new UiTextTestContext();
        var reports = new List<string>();
        var example = UiShowcaseControls.CreateExamples(reports.Add)[2];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var editor = Find<TextBox>(example.Content, UiShowcaseControls.TextBoxEditorId);
        var reportButton = Find<Button>(example.Content, UiShowcaseControls.TextBoxReportId);
        var feedback = Find<Text>(example.Content, UiShowcaseControls.TextBoxFeedbackId);

        editor.Text = "abc";
        host.Click(reportButton);
        Assert.Equal("当前文本：abc", reports[^1]);
        Assert.Equal("当前文本：abc", feedback.Content);

        var longText = new string('A', 200);
        editor.Text = longText;
        host.Click(reportButton);
        var summary = reports[^1];
        Assert.True(summary.Length <= 80, "The reported feedback must stay bounded.");
        Assert.DoesNotContain(longText, summary);
        Assert.Contains("共 200 字符", summary);
        Assert.Equal(summary, feedback.Content);
    }

    [Fact]
    public void ControlsStateExampleTogglesReadOnlyAndDisabled()
    {
        using var context = new UiTextTestContext();
        var example = UiShowcaseControls.CreateExamples(_ => { })[3];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var editor = Find<TextBox>(example.Content, UiShowcaseControls.StateEditorId);
        var toggle = Find<Button>(example.Content, UiShowcaseControls.StateToggleId);
        var label = Find<Text>(example.Content, UiShowcaseControls.StateLabelId);

        Assert.False(editor.IsReadOnly);
        Assert.True(editor.IsEnabled);
        Assert.Contains("可编辑", label.Content);

        host.Click(toggle);
        Assert.True(editor.IsReadOnly);
        Assert.True(editor.IsEnabled);
        Assert.Contains("只读", label.Content);

        host.Click(toggle);
        Assert.False(editor.IsReadOnly);
        Assert.False(editor.IsEnabled);
        Assert.Contains("禁用", label.Content);

        host.Click(toggle);
        Assert.True(editor.IsReadOnly);
        Assert.False(editor.IsEnabled);
        Assert.Contains("只读且禁用", label.Content);

        host.Click(toggle);
        Assert.False(editor.IsReadOnly);
        Assert.True(editor.IsEnabled);
        Assert.Equal("状态示例", editor.Text);
    }

    [Fact]
    public void BindingsOneWayExampleUpdatesTargetAndCountFromSource()
    {
        using var context = new UiTextTestContext();
        var reports = new List<string>();
        var example = UiShowcaseBindings.CreateExamples(reports.Add)[0];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var advance = Find<Button>(example.Content, UiShowcaseBindings.OneWayAdvanceId);
        var text = Find<Text>(example.Content, UiShowcaseBindings.OneWayTextId);
        var count = Find<Text>(example.Content, UiShowcaseBindings.OneWayCountId);

        Assert.Equal("初始文本", text.Content);
        Assert.Equal("更新 0 次", count.Content);
        Assert.True(text.IsBound(Text.ContentProperty));
        Assert.Throws<InvalidOperationException>(() => text.Content = "直接写入");

        host.Click(advance);
        Assert.Equal("文本 1", text.Content);
        Assert.Equal("更新 1 次", count.Content);

        host.Click(advance);
        Assert.Equal("文本 2", text.Content);
        Assert.Equal("更新 2 次", count.Content);
        Assert.Equal("单向绑定：文本 2", reports[^1]);
    }

    [Fact]
    public void BindingsTwoWayExampleMirrorsSourceOnEditAndPreset()
    {
        using var context = new UiTextTestContext();
        var reports = new List<string>();
        var example = UiShowcaseBindings.CreateExamples(reports.Add)[1];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var editor = Find<TextBox>(example.Content, UiShowcaseBindings.TwoWayEditorId);
        var mirror = Find<Text>(example.Content, UiShowcaseBindings.TwoWayMirrorId);
        var preset = Find<Button>(example.Content, UiShowcaseBindings.TwoWayPresetId);

        Assert.Equal("可编辑文本", editor.Text);
        Assert.Equal("可编辑文本", mirror.Content);

        editor.Text = "编辑后的文本";
        Assert.Equal("编辑后的文本", mirror.Content);

        host.Click(preset);
        Assert.Equal("预设值", editor.Text);
        Assert.Equal("预设值", mirror.Content);
        Assert.Equal("双向绑定：源已写入 预设值", reports[^1]);
    }

    [Fact]
    public void BindingsComputedExampleRecalculatesOnSourceChanges()
    {
        using var context = new UiTextTestContext();
        var example = UiShowcaseBindings.CreateExamples(_ => { })[2];
        using var host = new ShowcaseHost(example.Content, new Size(800, 600));

        var increment = Find<Button>(example.Content, UiShowcaseBindings.ComputedIncrementId);
        var messageButton = Find<Button>(example.Content, UiShowcaseBindings.ComputedMessageId);
        var summary = Find<Text>(example.Content, UiShowcaseBindings.ComputedSummaryId);

        Assert.Equal("初始 / 0", summary.Content);

        host.Click(increment);
        Assert.Equal("初始 / 1", summary.Content);

        host.Click(messageButton);
        Assert.Equal("已修改 / 1", summary.Content);

        host.Click(increment);
        Assert.Equal("已修改 / 2", summary.Content);

        host.Click(messageButton);
        Assert.Equal("初始 / 2", summary.Content);
    }

    private static T Find<T>(UiNode root, string styleId)
        where T : UiNode
    {
        if (root is T match && string.Equals(root.StyleId, styleId, StringComparison.Ordinal))
            return match;

        if (root is Parent parent)
        {
            foreach (var child in parent.Children)
            {
                var found = TryFind<T>(child, styleId);
                if (found is not null)
                    return found;
            }
        }

        throw new InvalidOperationException($"StyleId '{styleId}' was not found.");
    }

    private static T? TryFind<T>(UiNode node, string styleId)
        where T : UiNode
    {
        if (node is T match && string.Equals(node.StyleId, styleId, StringComparison.Ordinal))
            return match;

        if (node is Parent parent)
        {
            foreach (var child in parent.Children)
            {
                var found = TryFind<T>(child, styleId);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private sealed class ShowcaseHost : IDisposable
    {
        private readonly UiManager _manager = new();
        private readonly Size _viewport;

        internal ShowcaseHost(UiNode content, Size viewport)
        {
            _viewport = viewport;
            Screen = new UiScreen(content) { UseLayoutRounding = false, Scale = 1 };
            _manager.Open(Screen);
            _manager.PrepareFrame(_viewport, 0);
        }

        internal UiScreen Screen { get; }

        internal void Click(UiNode node)
        {
            _manager.PrepareFrame(_viewport, 0);
            var center = node.LocalToScreen(new Point(
                node.LayoutBounds.Width / 2,
                node.LayoutBounds.Height / 2));
            _manager.ProcessPointerPressed(center, MouseButton.Left, KeyModifiers.None);
            _manager.ProcessPointerReleased(center, MouseButton.Left, KeyModifiers.None);
        }

        public void Dispose() => _manager.Destroy();
    }
}
