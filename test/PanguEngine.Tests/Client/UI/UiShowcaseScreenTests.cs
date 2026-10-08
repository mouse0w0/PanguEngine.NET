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
    public void CategoryStripAndSelectedContentUseTheSameHeight()
    {
        using var context = new UiTextTestContext();
        var screen = CreateScreen();
        screen.Open();
        Prepare(screen, new Size(800, 600));

        var categories = screen.Shell.CategoryTabs;
        var strip = Assert.Single(categories.Children.OfType<TabStripPanel>());
        var content = Assert.Single(categories.Children.OfType<TabContentArea>());
        Assert.Equal(strip.LayoutBounds.Height, content.LayoutBounds.Height, 3);
        screen.Close();
    }

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
    public void NavigationClicksPreserveTwoWayBindingDataAcrossCategories()
    {
        var screen = CreateScreen();

        screen.SelectCategory(2);
        Assert.Equal(2, screen.CurrentCategory);
        screen.SelectExample(1);
        Assert.Equal(1, screen.CurrentExampleIndex);

        var editor = ShowcaseTestSupport.FindById<TextBox>(
            screen.CurrentExample.Content,
            UiShowcaseBindings.TwoWayEditorId);
        var mirror = ShowcaseTestSupport.FindById<Text>(
            screen.CurrentExample.Content,
            UiShowcaseBindings.TwoWayMirrorId);

        editor.Text = "修改后的文本";
        Assert.Equal("修改后的文本", mirror.Content);

        screen.SelectCategory(0);
        screen.SelectCategory(2);
        screen.SelectExample(1);

        var returnedEditor = ShowcaseTestSupport.FindById<TextBox>(
            screen.CurrentExample.Content,
            UiShowcaseBindings.TwoWayEditorId);
        var returnedMirror = ShowcaseTestSupport.FindById<Text>(
            screen.CurrentExample.Content,
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
    public void CategorySwitchMovesFocusFromHiddenContentToSelectedTab()
    {
        using var context = new UiTextTestContext();
        var screen = CreateScreen();
        screen.Scale = 1;
        screen.Open();
        Prepare(screen, new Size(800, 600));

        var probe = new Button { Text = "probe" };
        Assert.IsAssignableFrom<Panel>(screen.CurrentExample.Content).Children.Add(probe);
        Prepare(screen, new Size(800, 600));

        Assert.True(probe.Focus());
        Assert.Same(probe, screen.FocusedNode);

        screen.SelectCategory(1);

        Assert.Same(screen.Shell.CategoryTabs.Selection.SelectedItem, screen.FocusedNode);
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

        Prepare(screen, new Size(800, 600));
        Prepare(screen, new Size(400, 300));
        Prepare(screen, new Size(400, 300));

        Assert.True(screen.Shell.IsCompact);
        Assert.True(screen.Shell.ShowWarning);
        Assert.Equal(TabStripPlacement.Left, screen.Shell.CategoryTabs.TabStripPlacement);
        AssertReachable(screen, screen.Shell.ReturnButton, 400, 300);
        foreach (var button in screen.Shell.ScaleButtons)
            AssertReachable(screen, button, 400, 300);

        Prepare(screen, new Size(800, 600));
        Prepare(screen, new Size(800, 600));

        Assert.False(screen.Shell.IsCompact);
        Assert.False(screen.Shell.ShowWarning);
        Assert.Equal(TabStripPlacement.Left, screen.Shell.CategoryTabs.TabStripPlacement);
        screen.Close();
    }

    [Fact]
    public void ShortViewportShowsVerticalScrollButtonsAndRestoringHeightHidesThem()
    {
        using var context = new UiTextTestContext();
        var screen = CreateScreen();
        screen.Scale = 1;
        screen.Open();
        Prepare(screen, new Size(800, 600));
        Prepare(screen, new Size(800, 340));
        Prepare(screen, new Size(800, 340));

        var tabs = screen.Shell.CategoryTabs;
        var strip = Assert.Single(tabs.Children.OfType<TabStripPanel>());
        var viewport = Assert.Single(strip.Children.OfType<TabStripViewport>());
        var buttons = strip.Children.OfType<TabScrollButton>().ToArray();
        Assert.Equal(TabStripPlacement.Left, tabs.TabStripPlacement);
        Assert.True(viewport.HasOverflow);
        Assert.All(buttons, button =>
        {
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.True(button.LayoutBounds.Height > 0);
        });
        Assert.True(buttons[1].IsEnabled);

        Prepare(screen, new Size(800, 600));
        Prepare(screen, new Size(800, 600));
        Assert.False(viewport.HasOverflow);
        Assert.All(buttons, button => Assert.Equal(Visibility.Hidden, button.Visibility));
        screen.Close();
    }

    private static void Prepare(UiShowcaseScreen screen, Size viewportSize)
    {
        screen.UpdateFrame(0);
        screen.PrepareFrame(viewportSize);
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

[Collection(UiToolkitCollection.Name)]
public sealed class UiShowcaseScreenScaleTests
{
    [Fact]
    public void ExplicitScaleFactorsUseOpeningScaleWithoutChangingGlobalSettings()
    {
        var original = UiToolkit.DefaultScale;
        try
        {
            UiToolkit.DefaultScale = 2;
            var screen = CreateScreen();
            screen.Open();
            Assert.Equal(2, screen.Scale);

            screen.SetScaleFactor(0.75);
            Assert.Equal(1.5, screen.Scale);
            screen.SetScaleFactor(1);
            Assert.Equal(2, screen.Scale);
            screen.SetScaleFactor(1.25);
            Assert.Equal(2.5, screen.Scale);

            UiToolkit.DefaultScale = 3;
            screen.SetScaleFactor(1);
            Assert.Equal(2, screen.Scale);
            Assert.Equal(3, UiToolkit.DefaultScale);
            screen.Close();
        }
        finally
        {
            UiToolkit.DefaultScale = original;
        }
    }

    private static UiShowcaseScreen CreateScreen()
    {
        var sheet = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase.css", "showcase");
        var overrides = ShowcaseTestSupport.LoadSheet("pangu/ui/showcase-overrides.css", "showcase-overrides");
        return new UiShowcaseScreen(sheet, overrides, () => { });
    }
}
