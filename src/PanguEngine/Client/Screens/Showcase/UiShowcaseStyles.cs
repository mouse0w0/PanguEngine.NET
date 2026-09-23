using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens;

/// <summary>
/// Builds the style and cascade showcase examples backed by the showcase CSS assets.
/// </summary>
internal static class UiShowcaseStyles
{
    /// <summary>
    /// Creates the style showcase examples.
    /// </summary>
    /// <param name="report">Receives a short feedback message for the most recent operation.</param>
    /// <param name="setOverrides">Switches the optional Author override sheet on or off.</param>
    /// <returns>The ordered style examples.</returns>
    internal static UiShowcaseExample[] CreateExamples(Action<string> report, Action<bool> setOverrides) =>
    [
        CreateDefaultStateExample(report),
        CreateAuthorLocalExample(report, setOverrides),
        CreateSelectorExample(report),
        CreateComboStateExample()
    ];

    private static UiShowcaseExample CreateDefaultStateExample(Action<string> report)
    {
        var target = new Button { StyleId = "showcase-state-target", Text = "状态目标" };
        var editor = new TextBox { StyleId = "showcase-state-textbox", Placeholder = "悬停或点击聚焦", Width = 280 };
        var status = UiShowcaseWidgets.Label("当前: 可用", "showcase-state-value");
        var toggle = UiShowcaseWidgets.ActionButton("showcase-state-toggle-enabled", "切换禁用", () =>
        {
            target.IsEnabled = !target.IsEnabled;
            editor.IsEnabled = target.IsEnabled;
            status.Content = target.IsEnabled ? "当前: 可用" : "当前: 已禁用";
            report(target.IsEnabled ? "已启用状态目标" : "已禁用状态目标");
        });

        var content = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Row(target, toggle),
            editor,
            status);
        return new UiShowcaseExample(
            "默认外观与交互状态",
            "悬停、聚焦或禁用按钮与文本框，按住按钮观察按下样式。",
            content);
    }

    private static UiShowcaseExample CreateAuthorLocalExample(Action<string> report, Action<bool> setOverrides)
    {
        var target = new Button { StyleId = "showcase-author-target", Text = "覆盖目标" };
        target.Classes.Add("showcase-author-target");
        var value = UiShowcaseWidgets.Label("有效背景: -", "showcase-author-value");
        var source = UiShowcaseWidgets.Label("来源: -", "showcase-author-source");
        var overridesEnabled = false;

        void Refresh()
        {
            value.Content = $"有效背景: {FormatBrush(target.Background)}";
            var styleSource = target.GetStyleValueSources(Region.BackgroundProperty).SingleOrDefault();
            source.Content = styleSource is null
                ? "来源: 无"
                : $"来源: {styleSource.Origin}[{styleSource.SheetIndex}] {styleSource.SelectorText} 遮蔽:{(styleSource.IsMaskedByLocalValue ? "是" : "否")}";
        }

        target.PropertyChanged += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Property, Region.BackgroundProperty))
                Refresh();
        };

        var toggle = UiShowcaseWidgets.ActionButton("showcase-author-toggle-overrides", "切换 Author 覆盖", () =>
        {
            overridesEnabled = !overridesEnabled;
            setOverrides(overridesEnabled);
            Refresh();
            report(overridesEnabled ? "已启用 Author 覆盖" : "已恢复基础 Author 样式");
        });
        var setLocal = UiShowcaseWidgets.ActionButton("showcase-author-set-local", "设置本地背景", () =>
        {
            target.SetValue(Region.BackgroundProperty, new SolidColorBrush(0x3F, 0x8F, 0x4F));
            Refresh();
            report("已设置本地背景");
        });
        var clearLocal = UiShowcaseWidgets.ActionButton("showcase-author-clear-local", "清除本地背景", () =>
        {
            target.ClearValue(Region.BackgroundProperty);
            Refresh();
            report("已清除本地背景");
        });

        Refresh();

        var content = UiShowcaseWidgets.Column(
            target,
            UiShowcaseWidgets.Row(toggle, setLocal, clearLocal),
            value,
            source);
        return new UiShowcaseExample(
            "Author 覆盖与本地值",
            "切换 Author 覆盖，设置或清除本地背景，观察有效值与来源变化。",
            content);
    }

    private static UiShowcaseExample CreateSelectorExample(Action<string> report)
    {
        var plain = new Button { StyleId = "showcase-selector-plain", Text = "默认" };
        plain.Classes.Add("showcase-plain");
        var typed = new Button { StyleId = "showcase-selector-typed", Text = "类型" };
        typed.Classes.Add("showcase-typed");
        var classOnly = new Button { StyleId = "showcase-selector-classonly", Text = "类" };
        classOnly.Classes.Add("showcase-classonly");
        var idTarget = new Button { StyleId = "showcase-id-target", Text = "id" };
        var wildcard = new Button { StyleId = "showcase-selector-wildcard", Text = "通配" };
        wildcard.Classes.Add("showcase-wildcard");
        var status = UiShowcaseWidgets.Label(string.Empty, "showcase-selector-report");

        void Refresh()
        {
            var targets = new[] { plain, typed, classOnly, idTarget, wildcard };
            status.Content = string.Join("\n", targets.Select(node =>
                $"{node.Text}: {node.GetStyleValueSources(Region.BackgroundProperty).SingleOrDefault()?.SelectorText ?? "无"}"));
        }

        foreach (var node in new[] { plain, typed, classOnly, idTarget, wildcard })
            node.PropertyChanged += (_, args) =>
            {
                if (ReferenceEquals(args.Property, Region.BackgroundProperty))
                    Refresh();
            };
        Refresh();

        var toggleTyped = UiShowcaseWidgets.ActionButton("showcase-selector-toggle-typed", "切换类型 class", () =>
        {
            if (!typed.Classes.Remove("showcase-typed"))
                typed.Classes.Add("showcase-typed");
            Refresh();
            report("已切换类型 class");
        });
        var toggleWildcard = UiShowcaseWidgets.ActionButton("showcase-selector-toggle-wildcard", "切换通配 class", () =>
        {
            if (!wildcard.Classes.Remove("showcase-wildcard"))
                wildcard.Classes.Add("showcase-wildcard");
            Refresh();
            report("已切换通配 class");
        });

        var content = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Row(plain, typed, classOnly, idTarget, wildcard),
            UiShowcaseWidgets.Row(toggleTyped, toggleWildcard),
            status);
        return new UiShowcaseExample(
            "class、id 与通配选择器",
            "切换目标 class，比较具名类型、无类型 class、id 与通配规则的匹配。",
            content);
    }

    private static UiShowcaseExample CreateComboStateExample()
    {
        var target = new Button { StyleId = "showcase-combo-target", Text = "组合目标" };
        target.Classes.Add("showcase-combo");
        var source = UiShowcaseWidgets.Label("来源: -", "showcase-combo-source");

        void Refresh()
        {
            var styleSource = target.GetStyleValueSources(Region.BackgroundProperty).SingleOrDefault();
            source.Content = styleSource is null
                ? "来源: 无"
                : $"来源: {styleSource.SelectorText}";
        }

        target.PropertyChanged += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Property, Region.BackgroundProperty))
                Refresh();
        };

        Refresh();

        var content = UiShowcaseWidgets.Column(target, source);
        return new UiShowcaseExample(
            "状态组合伪类",
            "让按钮同时处于悬停与焦点，观察 :hover:focus 组合规则，条件消失后恢复。",
            content);
    }

    private static string FormatBrush(Brush? brush) =>
        brush is SolidColorBrush solid
            ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}"
            : "无";
}
