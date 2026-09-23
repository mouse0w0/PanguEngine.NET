using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class UiShowcaseScreenTests
{
    [Fact]
    public void ConstructorInjectsAuthorSheetsAndPauseBehavior()
    {
        var sheet = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");

        var screen = new UiShowcaseScreen(sheet, overrides, () => { });

        Assert.True(screen.PausesGame);
        Assert.True(screen.CloseOnEscape);
        Assert.Same(BuiltinInputContexts.Ui, screen.InputContext);
        Assert.Contains(sheet, screen.StyleSheets);
        Assert.DoesNotContain(overrides, screen.StyleSheets);
        Assert.Single(screen.BaseStyleSheets);
        Assert.Equal(0, screen.CurrentCategory);
        Assert.Equal(0, screen.CurrentExampleIndex);
        Assert.Equal(4, screen.Shell.CategoryButtons.Count);
        Assert.Equal("返回暂停菜单", screen.Shell.ReturnButton.Text);
    }

    [Fact]
    public void CategorySelectionRetainsExamplePerCategory()
    {
        var screen = CreateScreen();

        screen.SelectCategory(2);
        Assert.Equal(2, screen.CurrentCategory);
        screen.SelectExample(1);
        var retained = screen.CurrentExample;
        Assert.Equal(1, screen.CurrentExampleIndex);

        screen.SelectCategory(0);
        Assert.Equal(0, screen.CurrentCategory);
        Assert.Equal(0, screen.CurrentExampleIndex);

        screen.SelectCategory(2);
        Assert.Equal(1, screen.CurrentExampleIndex);
        Assert.Same(retained, screen.CurrentExample);
    }

    [Fact]
    public void SwitchingCategoryReplacesSingleContentChildAndCachesExample()
    {
        var screen = CreateScreen();
        screen.SelectCategory(0);
        var first = screen.CurrentExample.Content;
        Assert.Single(screen.Shell.ContentHost.Children);
        Assert.Same(first, screen.Shell.ContentHost.Children[0]);

        screen.SelectCategory(1);
        Assert.Single(screen.Shell.ContentHost.Children);
        Assert.NotSame(first, screen.Shell.ContentHost.Children[0]);

        screen.SelectCategory(0);
        Assert.Single(screen.Shell.ContentHost.Children);
        Assert.Same(first, screen.Shell.ContentHost.Children[0]);
    }

    [Fact]
    public void NavigationClicksPreserveTwoWayBindingDataAcrossCategories()
    {
        var screen = CreateScreen();

        ShowcaseTestSupport.Click(screen.Shell.CategoryButtons[2]);
        Assert.Equal(2, screen.CurrentCategory);
        ShowcaseTestSupport.Click((Button)screen.Shell.ExampleSwitches.Children[1]);
        Assert.Equal(1, screen.CurrentExampleIndex);

        var editor = ShowcaseTestSupport.FindById<TextBox>(
            screen.Shell.ContentHost,
            UiShowcaseBindings.TwoWayEditorId);
        var mirror = ShowcaseTestSupport.FindById<Text>(
            screen.Shell.ContentHost,
            UiShowcaseBindings.TwoWayMirrorId);

        editor.Text = "修改后的文本";
        Assert.Equal("修改后的文本", mirror.Content);

        ShowcaseTestSupport.Click(screen.Shell.CategoryButtons[0]);
        ShowcaseTestSupport.Click(screen.Shell.CategoryButtons[2]);
        ShowcaseTestSupport.Click((Button)screen.Shell.ExampleSwitches.Children[1]);

        var returnedEditor = ShowcaseTestSupport.FindById<TextBox>(
            screen.Shell.ContentHost,
            UiShowcaseBindings.TwoWayEditorId);
        var returnedMirror = ShowcaseTestSupport.FindById<Text>(
            screen.Shell.ContentHost,
            UiShowcaseBindings.TwoWayMirrorId);

        Assert.Same(editor, returnedEditor);
        Assert.Equal("修改后的文本", returnedEditor.Text);
        Assert.Equal("修改后的文本", returnedMirror.Content);
    }

    [Fact]
    public void SetOverridesSwapsAuthorSheetListKeepingBaseSheets()
    {
        var sheet = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");
        var screen = new UiShowcaseScreen(sheet, overrides, () => { });

        screen.SetOverrides(true);
        Assert.Contains(sheet, screen.StyleSheets);
        Assert.Contains(overrides, screen.StyleSheets);

        screen.SetOverrides(false);
        Assert.Contains(sheet, screen.StyleSheets);
        Assert.DoesNotContain(overrides, screen.StyleSheets);
        Assert.Single(screen.BaseStyleSheets);
    }

    [Fact]
    public void ReturnToPauseInvokesInjectedCallback()
    {
        var calls = 0;
        var screen = CreateScreen(() => calls++);

        screen.ReturnToPause();
        Assert.Equal(1, calls);
        screen.ReturnToPause();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void CategorySwitchClearsFocusFromRemovedContent()
    {
        using var context = new UiTextTestContext();
        var screen = CreateScreen();
        screen.Scale = 1;
        screen.Open();
        screen.PrepareFrame(new Size(800, 600), 0);

        var probe = new Button { Text = "probe" };
        screen.Shell.ContentHost.Children.Add(probe);
        screen.PrepareFrame(new Size(800, 600), 0);

        Assert.True(probe.Focus());
        Assert.Same(probe, screen.FocusedNode);

        screen.SelectCategory(1);

        Assert.Null(screen.FocusedNode);
        Assert.False(probe.IsFocused);
        screen.Close();
    }

    [Fact]
    public void SmallViewportShowsWarningAndKeepsShellControlsReachable()
    {
        using var context = new UiTextTestContext();
        var screen = CreateScreen();
        screen.Scale = 1;
        screen.Open();

        screen.PrepareFrame(new Size(800, 600), 0);
        screen.PrepareFrame(new Size(400, 300), 0);
        screen.PrepareFrame(new Size(400, 300), 0);

        Assert.True(screen.Shell.IsCompact);
        Assert.True(screen.Shell.ShowWarning);
        foreach (var button in screen.Shell.CategoryButtons)
            AssertReachable(screen, button, 400, 300);
        AssertReachable(screen, screen.Shell.ReturnButton, 400, 300);
        foreach (var button in screen.Shell.ScaleButtons)
            AssertReachable(screen, button, 400, 300);

        screen.PrepareFrame(new Size(800, 600), 0);
        screen.PrepareFrame(new Size(800, 600), 0);

        Assert.False(screen.Shell.IsCompact);
        Assert.False(screen.Shell.ShowWarning);
        screen.Close();
    }

    private static UiShowcaseScreen CreateScreen(Action? returnToPause = null)
    {
        var sheet = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");
        return new UiShowcaseScreen(sheet, overrides, returnToPause ?? (() => { }));
    }

    private static void AssertReachable(UiScreen screen, UiNode node, double width, double height)
    {
        Assert.True(node.IsArrangeValid);
        var origin = node.LocalToScreen(Point.Zero);
        Assert.True(origin.X >= -0.5, "The control origin is left of the viewport.");
        Assert.True(origin.Y >= -0.5, "The control origin is above the viewport.");
        Assert.True(origin.X + node.LayoutBounds.Width <= width + 0.5, "The control exceeds the viewport width.");
        Assert.True(origin.Y + node.LayoutBounds.Height <= height + 0.5, "The control exceeds the viewport height.");

        var center = new Point(
            origin.X + node.LayoutBounds.Width / 2,
            origin.Y + node.LayoutBounds.Height / 2);
        Assert.Same(node, screen.HitTest(center));
    }
}

[Collection(UiSettingsCollection.Name)]
public sealed class UiShowcaseScreenScaleTests
{
    [Fact]
    public void ExplicitScaleFactorsUseOpeningScaleWithoutChangingGlobalSettings()
    {
        var original = UiSettings.DefaultScale;
        try
        {
            UiSettings.DefaultScale = 2;
            var screen = CreateScreen();
            screen.Open();
            Assert.Equal(2, screen.Scale);

            screen.SetScaleFactor(0.75);
            Assert.Equal(1.5, screen.Scale);
            screen.SetScaleFactor(1);
            Assert.Equal(2, screen.Scale);
            screen.SetScaleFactor(1.25);
            Assert.Equal(2.5, screen.Scale);

            UiSettings.DefaultScale = 3;
            screen.SetScaleFactor(1);
            Assert.Equal(2, screen.Scale);
            Assert.Equal(3, UiSettings.DefaultScale);
            screen.Close();
        }
        finally
        {
            UiSettings.DefaultScale = original;
        }
    }

    private static UiShowcaseScreen CreateScreen()
    {
        var sheet = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");
        return new UiShowcaseScreen(sheet, overrides, () => { });
    }
}
