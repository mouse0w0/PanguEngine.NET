using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Input;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;
using PanguEngine.Resources;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiShowcaseStyleTests
{
    [Fact]
    public void ShowcaseAssetsParseAndDeclareShellAndExampleSelectors()
    {
        var showcase = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");

        var selectors = showcase.Rules.Select(rule => rule.Selector.SelectorText).ToArray();

        Assert.Contains(".showcase-nav", selectors);
        Assert.Contains(".showcase-nav.selected", selectors);
        Assert.Contains("Text.showcase-heading", selectors);
        Assert.Contains("Text.showcase-feedback", selectors);
        Assert.Contains("Text.showcase-warning", selectors);
        Assert.Contains("Button.showcase-typed", selectors);
        Assert.Contains(".showcase-classonly", selectors);
        Assert.Contains("#showcase-id-target", selectors);
        Assert.Contains(".showcase-wildcard", selectors);
        Assert.Contains("Button.showcase-combo", selectors);
        Assert.Contains("Button.showcase-combo:focus:hover", selectors);

        Assert.Equal(
            new[] { ".showcase-author-target" },
            overrides.Rules.Select(rule => rule.Selector.SelectorText).ToArray());
    }

    [Fact]
    public void SelectorExampleDistinguishesTypeClassIdAndWildcard()
    {
        var showcase = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var examples = UiShowcaseStyles.CreateExamples(_ => { }, _ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "class、id 与通配选择器");
        var screen = new UiScreen(example.Content);
        screen.SetStyleSheets([showcase]);

        var root = example.Content;
        var plain = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-plain");
        var typed = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-typed");
        var classOnly = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-classonly");
        var idTarget = ShowcaseTestSupport.FindById<Button>(root, "showcase-id-target");
        var wildcard = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-wildcard");

        Assert.Equal(new SolidColorBrush(48, 54, 62), plain.Background);
        Assert.Equal("Button", ShowcaseTestSupport.Source(plain).SelectorText);

        Assert.Equal(new SolidColorBrush(0x2B, 0x4A, 0x6F), typed.Background);
        Assert.Equal("Button.showcase-typed", ShowcaseTestSupport.Source(typed).SelectorText);

        Assert.Equal(new SolidColorBrush(0x4A, 0x3B, 0x6B), classOnly.Background);
        Assert.Equal(".showcase-classonly", ShowcaseTestSupport.Source(classOnly).SelectorText);

        Assert.Equal(new SolidColorBrush(0x6B, 0x3B, 0x2B), idTarget.Background);
        Assert.Equal("#showcase-id-target", ShowcaseTestSupport.Source(idTarget).SelectorText);

        Assert.Equal(new SolidColorBrush(0x2F, 0x5B, 0x3A), wildcard.Background);
        Assert.Equal(".showcase-wildcard", ShowcaseTestSupport.Source(wildcard).SelectorText);

        var toggleTyped = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-toggle-typed");
        var report = ShowcaseTestSupport.FindById<Text>(root, "showcase-selector-report");
        Assert.Contains("类型: Button.showcase-typed", report.Content);
        ShowcaseTestSupport.Click(toggleTyped);
        Assert.Equal(new SolidColorBrush(48, 54, 62), typed.Background);
        Assert.Contains("类型: Button", report.Content);
        Assert.DoesNotContain("Button.showcase-typed", report.Content);
        ShowcaseTestSupport.Click(toggleTyped);
        Assert.Equal(new SolidColorBrush(0x2B, 0x4A, 0x6F), typed.Background);

        var toggleWildcard = ShowcaseTestSupport.FindById<Button>(root, "showcase-selector-toggle-wildcard");
        ShowcaseTestSupport.Click(toggleWildcard);
        Assert.Equal(new SolidColorBrush(48, 54, 62), wildcard.Background);
        ShowcaseTestSupport.Click(toggleWildcard);
        Assert.Equal(new SolidColorBrush(0x2F, 0x5B, 0x3A), wildcard.Background);
    }

    [Fact]
    public void DefaultStateExampleUsesBuiltInButtonStatesAndToggle()
    {
        var showcase = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var examples = UiShowcaseStyles.CreateExamples(_ => { }, _ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "默认外观与交互状态");
        var screen = new UiScreen(example.Content);
        screen.SetStyleSheets([showcase]);

        var target = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-state-target");
        var editor = ShowcaseTestSupport.FindById<TextBox>(example.Content, "showcase-state-textbox");
        var toggle = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-state-toggle-enabled");
        var label = ShowcaseTestSupport.FindById<Text>(example.Content, "showcase-state-value");

        Assert.Equal("Button", ShowcaseTestSupport.Source(target).SelectorText);
        Assert.True(editor.IsEnabled);
        editor.SetHovered(true);
        Assert.Equal("TextBox:hover", editor.GetStyleValueSources(Region.BorderBrushProperty).Single().SelectorText);
        editor.SetFocused(true);
        Assert.Equal("TextBox:focus", editor.GetStyleValueSources(Region.BorderBrushProperty).Single().SelectorText);
        editor.SetHovered(false);
        editor.SetFocused(false);

        target.SetHovered(true);
        Assert.Equal(new SolidColorBrush(0x43, 0x49, 0x50), target.Background);
        Assert.Equal("Button:hover", ShowcaseTestSupport.Source(target).SelectorText);
        target.SetHovered(false);
        Assert.Equal(new SolidColorBrush(48, 54, 62), target.Background);

        ShowcaseTestSupport.Click(toggle);
        Assert.False(target.IsEnabled);
        Assert.False(editor.IsEnabled);
        Assert.Equal("TextBox:disabled", editor.GetStyleValueSources(Region.BorderBrushProperty).Single().SelectorText);
        Assert.Equal(new SolidColorBrush(0x1B, 0x1E, 0x23), target.Background);
        Assert.Contains("已禁用", label.Content);

        ShowcaseTestSupport.Click(toggle);
        Assert.True(target.IsEnabled);
        Assert.True(editor.IsEnabled);
        Assert.Equal(new SolidColorBrush(48, 54, 62), target.Background);
        Assert.Contains("可用", label.Content);
    }

    [Fact]
    public void AuthorOverrideSwitchesSheetListAndLocalValueClearRestoresSource()
    {
        var showcase = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");
        UiScreen? screen = null;
        var examples = UiShowcaseStyles.CreateExamples(
            _ => { },
            enabled => screen!.SetStyleSheets(enabled ? new[] { showcase, overrides } : new[] { showcase }));
        var example = ShowcaseTestSupport.FindExample(examples, "Author 覆盖与本地值");
        screen = new UiScreen(example.Content);
        screen.SetStyleSheets([showcase]);

        var root = example.Content;
        var target = ShowcaseTestSupport.FindById<Button>(root, "showcase-author-target");
        var valueLabel = ShowcaseTestSupport.FindById<Text>(root, "showcase-author-value");
        var sourceLabel = ShowcaseTestSupport.FindById<Text>(root, "showcase-author-source");
        var toggle = ShowcaseTestSupport.FindById<Button>(root, "showcase-author-toggle-overrides");
        var setLocal = ShowcaseTestSupport.FindById<Button>(root, "showcase-author-set-local");
        var clearLocal = ShowcaseTestSupport.FindById<Button>(root, "showcase-author-clear-local");

        Assert.Equal(new SolidColorBrush(0x2F, 0x5B, 0x7A), target.Background);
        var baseSource = ShowcaseTestSupport.Source(target);
        Assert.Equal(UiStyleOrigin.Author, baseSource.Origin);
        Assert.Equal("showcase", baseSource.SheetSourceName);
        Assert.Equal(0, baseSource.SheetIndex);
        Assert.Contains("遮蔽:否", sourceLabel.Content);
        Assert.Contains("Author[0]", sourceLabel.Content);

        ShowcaseTestSupport.Click(toggle);
        Assert.Equal(new SolidColorBrush(0x7A, 0x2F, 0x5B), target.Background);
        var overrideSource = ShowcaseTestSupport.Source(target);
        Assert.Equal("showcase-overrides", overrideSource.SheetSourceName);
        Assert.Equal(1, overrideSource.SheetIndex);
        Assert.Contains("遮蔽:否", sourceLabel.Content);
        Assert.Contains("Author[1]", sourceLabel.Content);

        ShowcaseTestSupport.Click(toggle);
        Assert.Equal(new SolidColorBrush(0x2F, 0x5B, 0x7A), target.Background);

        ShowcaseTestSupport.Click(setLocal);
        Assert.Equal(new SolidColorBrush(0x3F, 0x8F, 0x4F), target.Background);
        Assert.Contains("遮蔽:是", sourceLabel.Content);
        Assert.Contains("#3F8F4F", valueLabel.Content);

        ShowcaseTestSupport.Click(clearLocal);
        Assert.Equal(new SolidColorBrush(0x2F, 0x5B, 0x7A), target.Background);
        Assert.Contains("遮蔽:否", sourceLabel.Content);

        var styleColor = new Color(0x2F, 0x5B, 0x7A);
        ShowcaseTestSupport.Click(setLocal);
        Assert.Equal(new SolidColorBrush(0x3F, 0x8F, 0x4F), target.Background);
        Assert.Contains("遮蔽:是", sourceLabel.Content);

        target.SetValue(Region.BackgroundProperty, new SolidColorBrush(styleColor));
        Assert.Equal(new SolidColorBrush(styleColor), target.Background);
        Assert.Contains("遮蔽:是", sourceLabel.Content);

        ShowcaseTestSupport.Click(clearLocal);
        Assert.Equal(new SolidColorBrush(styleColor), target.Background);
        Assert.Contains("遮蔽:否", sourceLabel.Content);
    }

    [Fact]
    public void ComboStateRuleAppliesOnlyWhenHoverAndFocusAreActive()
    {
        var showcase = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var examples = UiShowcaseStyles.CreateExamples(_ => { }, _ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "状态组合伪类");
        var screen = new UiScreen(example.Content);
        screen.SetStyleSheets([showcase]);

        var target = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-combo-target");
        var sourceLabel = ShowcaseTestSupport.FindById<Text>(example.Content, "showcase-combo-source");

        Assert.Equal(new SolidColorBrush(0x3A, 0x3F, 0x47), target.Background);
        Assert.Equal("Button.showcase-combo", ShowcaseTestSupport.Source(target).SelectorText);

        target.SetHovered(true);
        target.SetFocused(true);
        Assert.Equal(new SolidColorBrush(0x7A, 0x4A, 0x1F), target.Background);
        Assert.Equal("Button.showcase-combo:focus:hover", ShowcaseTestSupport.Source(target).SelectorText);
        Assert.Contains("Button.showcase-combo:focus:hover", sourceLabel.Content);

        target.SetHovered(false);
        target.SetFocused(false);
        Assert.Equal(new SolidColorBrush(0x3A, 0x3F, 0x47), target.Background);
        Assert.Equal("Button.showcase-combo", ShowcaseTestSupport.Source(target).SelectorText);
    }
}

public sealed class UiShowcaseLayoutTests
{
    [Fact]
    public void PanelAlignmentButtonsChangeTargetAlignment()
    {
        var examples = UiShowcaseLayout.CreateExamples(_ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "Panel 对齐");
        var target = ShowcaseTestSupport.FindById<Panel>(example.Content, "showcase-panel-target");
        var horizontal = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-panel-horizontal");
        var vertical = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-panel-vertical");

        Assert.Equal(HorizontalAlignment.Left, target.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, target.VerticalAlignment);

        ShowcaseTestSupport.Click(horizontal);
        Assert.Equal(HorizontalAlignment.Center, target.HorizontalAlignment);
        ShowcaseTestSupport.Click(horizontal);
        Assert.Equal(HorizontalAlignment.Right, target.HorizontalAlignment);
        ShowcaseTestSupport.Click(horizontal);
        Assert.Equal(HorizontalAlignment.Left, target.HorizontalAlignment);

        ShowcaseTestSupport.Click(vertical);
        Assert.Equal(VerticalAlignment.Center, target.VerticalAlignment);
        ShowcaseTestSupport.Click(vertical);
        Assert.Equal(VerticalAlignment.Bottom, target.VerticalAlignment);
        ShowcaseTestSupport.Click(vertical);
        Assert.Equal(VerticalAlignment.Top, target.VerticalAlignment);
    }

    [Fact]
    public void StackPanelButtonsChangeOrientationSpacingAndMargin()
    {
        var examples = UiShowcaseLayout.CreateExamples(_ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "StackPanel 布局");
        var host = ShowcaseTestSupport.FindById<StackPanel>(example.Content, "showcase-stack-host");
        var item = ShowcaseTestSupport.FindById<Panel>(example.Content, "showcase-stack-item-2");
        var orientation = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-stack-orientation");
        var spacing = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-stack-spacing");
        var margin = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-stack-margin");

        Assert.Equal(Orientation.Vertical, host.Orientation);
        Assert.Equal(8d, host.Spacing);
        Assert.Equal(Thickness.Zero, item.Margin);

        ShowcaseTestSupport.Click(orientation);
        Assert.Equal(Orientation.Horizontal, host.Orientation);
        ShowcaseTestSupport.Click(spacing);
        Assert.Equal(16d, host.Spacing);
        ShowcaseTestSupport.Click(margin);
        Assert.Equal(new Thickness(6), item.Margin);
    }

    [Fact]
    public void CanvasButtonCyclesTargetCoordinates()
    {
        var examples = UiShowcaseLayout.CreateExamples(_ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "Canvas 位置");
        var target = ShowcaseTestSupport.FindById<Panel>(example.Content, "showcase-canvas-target");
        var cycle = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-canvas-cycle");

        Assert.Equal(10d, Canvas.GetLeft(target));
        Assert.Equal(10d, Canvas.GetTop(target));

        ShowcaseTestSupport.Click(cycle);
        Assert.Equal(150d, Canvas.GetLeft(target));
        Assert.Equal(10d, Canvas.GetTop(target));
        ShowcaseTestSupport.Click(cycle);
        Assert.Equal(10d, Canvas.GetLeft(target));
        Assert.Equal(84d, Canvas.GetTop(target));
    }

    [Fact]
    public void VisibilityButtonCyclesThreeStates()
    {
        var examples = UiShowcaseLayout.CreateExamples(_ => { });
        var example = ShowcaseTestSupport.FindExample(examples, "可见性三态");
        var target = ShowcaseTestSupport.FindById<Panel>(example.Content, "showcase-visibility-target");
        var cycle = ShowcaseTestSupport.FindById<Button>(example.Content, "showcase-visibility-cycle");

        Assert.Equal(Visibility.Visible, target.Visibility);
        ShowcaseTestSupport.Click(cycle);
        Assert.Equal(Visibility.Hidden, target.Visibility);
        ShowcaseTestSupport.Click(cycle);
        Assert.Equal(Visibility.Collapsed, target.Visibility);
        ShowcaseTestSupport.Click(cycle);
        Assert.Equal(Visibility.Visible, target.Visibility);
    }
}

internal static class ShowcaseTestSupport
{
    internal static UiStyleSheet LoadSheet(string resourcePath, string sourceName)
    {
        using var source = new DirectoryResourceSource(AppContext.BaseDirectory);
        using var stream = source.Open(resourcePath);
        return UiStyleSheet.Parse(stream, sourceName);
    }

    internal static UiShowcaseExample FindExample(UiShowcaseExample[] examples, string title)
    {
        foreach (var example in examples)
        {
            if (string.Equals(example.Title, title, StringComparison.Ordinal))
                return example;
        }

        throw new InvalidOperationException($"Example '{title}' was not found.");
    }

    internal static T FindById<T>(UiNode root, string id)
        where T : UiNode =>
        Assert.IsType<T>(FindById(root, id));

    internal static UiStyleValueSource Source(UiNode node) =>
        node.GetStyleValueSources(Region.BackgroundProperty).Single();

    internal static void Click(Button button) =>
        button.RaisePointerClicked(new UiPointerButtonEventArgs(
            button,
            new Point(0, 0),
            MouseButton.Left,
            KeyModifiers.None,
            Array.Empty<UiHitPathEntry>()));

    private static UiNode FindById(UiNode root, string id) =>
        FindByIdOrNull(root, id) ?? throw new InvalidOperationException($"Node '{id}' was not found.");

    private static UiNode? FindByIdOrNull(UiNode node, string id)
    {
        if (string.Equals(node.StyleId, id, StringComparison.Ordinal))
            return node;

        if (node is Parent parent)
        {
            foreach (var child in parent.Children)
            {
                var match = FindByIdOrNull(child, id);
                if (match is not null)
                    return match;
            }
        }

        return null;
    }
}
