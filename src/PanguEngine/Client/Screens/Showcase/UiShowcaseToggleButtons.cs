using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds interactive toggle button examples with shared state.
/// </summary>
internal static class UiShowcaseToggleButtons
{
    /// <summary>
    /// Creates a page that demonstrates toggle states and synchronized controls.
    /// </summary>
    internal static UiShowcaseExample CreateExample(Action<string> report)
    {
        var model = new ShowcaseToggleModel();
        var primary = new ToggleButton { StyleId = "showcase-toggle-primary", Text = "显示网格" };
        var peer = new ToggleButton { StyleId = "showcase-toggle-peer", Text = "同步开关" };
        primary.BindTwoWay(ToggleButton.IsCheckedProperty, model, ShowcaseToggleModel.IsGridVisibleProperty);
        peer.BindTwoWay(ToggleButton.IsCheckedProperty, model, ShowcaseToggleModel.IsGridVisibleProperty);

        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-toggle-status");
        status.Bind(Text.ContentProperty, model, ShowcaseToggleModel.IsGridVisibleProperty,
            static value => value ? "网格显示：开启" : "网格显示：关闭");
        var clicks = 0;
        var count = UiShowcaseWidgets.Label("开关点击次数：0", "showcase-toggle-count");
        void RecordClick(object? sender, EventArgs eventArgs)
        {
            count.Content = $"开关点击次数：{++clicks}";
            report(model.IsGridVisible ? "网格显示已开启" : "网格显示已关闭");
        }
        primary.Click += RecordClick;
        peer.Click += RecordClick;

        var external = UiShowcaseWidgets.ActionButton("showcase-toggle-external", "从外部切换", () =>
        {
            model.IsGridVisible = !model.IsGridVisible;
            report(model.IsGridVisible ? "从外部开启网格显示" : "从外部关闭网格显示");
        });

        var icon = UiImage.FromRgba(
        [
            84, 169, 255, 255, 48, 54, 62, 255,
            48, 54, 62, 255, 84, 169, 255, 255
        ], 2, 2);
        var withIcon = new ToggleButton
        {
            StyleId = "showcase-toggle-icon",
            Text = "图标切换",
            Icon = icon,
            IsChecked = true
        };
        var disabled = new ToggleButton
        {
            StyleId = "showcase-toggle-disabled",
            Text = "关闭且禁用",
            IsEnabled = false
        };
        var disabledChecked = new ToggleButton
        {
            StyleId = "showcase-toggle-disabled-checked",
            Text = "开启且禁用",
            IsChecked = true,
            IsEnabled = false
        };

        return new UiShowcaseExample(
            "切换按钮",
            "点击切换开关；聚焦后 Enter 按下或 Space 松开也可切换。选中效果会持续保留。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("共享开关 A", primary),
                    UiShowcaseWidgets.Sample("共享开关 B", peer)),
                UiShowcaseWidgets.Preview(status, count),
                external,
                UiShowcaseWidgets.Caption("两个开关双向同步；从外部改变状态不会增加开关点击次数。"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("图标与文字 · 初始开启", withIcon),
                    UiShowcaseWidgets.Sample("禁用 · 关闭", disabled),
                    UiShowcaseWidgets.Sample("禁用 · 开启", disabledChecked))));
    }

    /// <summary>
    /// Stores the display option shared by the interactive examples.
    /// </summary>
    private sealed class ShowcaseToggleModel : ObservableObject
    {
        /// <summary>
        /// Identifies the <see cref="IsGridVisible"/> property.
        /// </summary>
        public static readonly Property<bool> IsGridVisibleProperty =
            Property.Register<ShowcaseToggleModel, bool>(nameof(IsGridVisible));

        /// <summary>
        /// Gets or sets whether grid display is enabled.
        /// </summary>
        public bool IsGridVisible
        {
            get => GetValue(IsGridVisibleProperty);
            set => SetValue(IsGridVisibleProperty, value);
        }
    }
}
