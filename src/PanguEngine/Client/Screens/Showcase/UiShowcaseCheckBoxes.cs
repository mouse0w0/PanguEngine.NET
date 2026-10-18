using PanguEngine.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>Builds interactive check box samples with state feedback and shared selections.</summary>
internal static class UiShowcaseCheckBoxes
{
    internal static UiShowcaseExample CreateExample(Action<string> report)
    {
        var twoState = new CheckBox { Content = "显示坐标" };
        var threeState = new CheckBox { Content = "阴影细节", IsThreeState = true };
        var initialMixed = new CheckBox { Content = "部分选中", IsChecked = null };
        var disabled = new CheckBox { Content = "暂不可用", IsChecked = true, IsEnabled = false };
        var source = new CheckBox { Content = "选项 A", IsThreeState = true };
        var peer = new CheckBox { Content = "选项 B", IsThreeState = true };
        peer.BindTwoWay(CheckBox.IsCheckedProperty, source, CheckBox.IsCheckedProperty);
        var custom = new CheckBox { Content = new OptionLabel("显示提示"), IsChecked = true };
        custom.Classes.Add("showcase-checkbox-accent");
        var description = new CheckBox
        {
            Content = UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Label("启用环境音"),
                UiShowcaseWidgets.Caption("播放风声、水声和周围环境的声音。"))
        };

        foreach (var box in new[] { twoState, threeState, initialMixed, source, peer, custom, description })
            box.Click += (_, _) => report($"复选框：{DescribeState(box.IsChecked)}");

        return new UiShowcaseExample(
            "复选框",
            "点击方框或标签切换；聚焦后也可使用空格。三态选项依次切换为已选中、不确定、未选中。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("二态", WithFeedback(twoState)),
                    UiShowcaseWidgets.Sample("三态", WithFeedback(threeState)),
                    UiShowcaseWidgets.Sample("不确定初值", WithFeedback(initialMixed)),
                    UiShowcaseWidgets.Sample("禁用", disabled)),
                UiShowcaseWidgets.Caption("同步选项：改变任意一项，另一项会同步。"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("选项 A", WithFeedback(source)),
                    UiShowcaseWidgets.Sample("选项 B", WithFeedback(peer))),
                UiShowcaseWidgets.Caption("标签可以包含标题和说明，也可以使用不同的字号与颜色。"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("组合标签", description),
                    UiShowcaseWidgets.Sample("文字样式", custom))));
    }

    private static StackPanel WithFeedback(CheckBox box)
    {
        var feedback = UiShowcaseWidgets.Caption(string.Empty);
        feedback.Bind(Text.ContentProperty, box, CheckBox.IsCheckedProperty, DescribeState);
        return UiShowcaseWidgets.Column(box, feedback);
    }

    private static string DescribeState(bool? state) => state switch
    {
        true => "已选中",
        false => "未选中",
        null => "不确定"
    };

    private sealed record OptionLabel(string Name)
    {
        public override string ToString() => Name;
    }
}
