using PanguEngine.Client.UI;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TextTests
{
    [Fact]
    public void NewMeasurementRequiresInitializedTextServices()
    {
        var text = new Text { Content = "Hello" };

        Assert.Throws<InvalidOperationException>(() => text.Measure(Size.Infinite));
    }

    [Fact]
    public void DetachedAndScreenOwnedTextUseStaticTextServices()
    {
        using var context = new UiTextTestContext();
        var detached = new Text { Content = "Detached" };
        var screenOwned = new Text { Content = "Screen owned" };
        _ = new UiScreen(screenOwned);

        detached.Measure(Size.Infinite);
        screenOwned.Measure(Size.Infinite);

        Assert.True(detached.DesiredSize.Width > 0);
        Assert.True(screenOwned.DesiredSize.Width > 0);
    }

    [Fact]
    public void PropertiesExposeFixedDefaultsAndExpectedInvalidation()
    {
        var text = new Text();
        var layoutInvalidation = UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render;

        AssertProperty(Text.ContentProperty, string.Empty, layoutInvalidation);
        AssertProperty(Text.FontProperty, new Font(string.Empty), layoutInvalidation);
        AssertProperty(Text.FontSizeProperty, 16d, layoutInvalidation);
        AssertProperty(Text.ColorProperty, new Color(255, 255, 255), UiPropertyInvalidation.Render);
        AssertProperty(Text.LineHeightProperty, 1d, layoutInvalidation);
        AssertProperty(Text.WrappingProperty, TextWrapping.NoWrap, layoutInvalidation);
        AssertProperty(Text.AlignmentProperty, TextAlignment.Left, layoutInvalidation);

        Assert.Equal(string.Empty, text.Content);
        Assert.Equal(new Font(string.Empty), text.Font);
        Assert.Equal(16, text.FontSize);
        Assert.Equal(new Color(255, 255, 255), text.Color);
        Assert.Equal(1, text.LineHeight);
        Assert.Equal(TextWrapping.NoWrap, text.Wrapping);
        Assert.Equal(TextAlignment.Left, text.Alignment);
    }

    [Fact]
    public void DefaultsUseCurrentDefaultFaceAndMeasureDrawShareLayout()
    {
        using var context = new UiTextTestContext();
        var text = new Text { Content = "Hello" };
        var screen = new UiScreen(text) { UseLayoutRounding = false };
        var manager = new UiManager();
        manager.Open(screen);

        manager.Update(new Size(400, 200));
        var first = GetCommand(screen);
        var second = GetCommand(screen);

        Assert.Same(first.Layout, second.Layout);
        Assert.Same(context.DefaultFace, first.Layout.Lines[0].GlyphRuns[0].FontFace);
        Assert.Equal(text.DesiredSize.Width, first.Layout.Width);
        Assert.Equal(text.DesiredSize.Height, first.Layout.Height);
        manager.Destroy();
    }

    [Fact]
    public void EmptyContentMeasuresEmptyAndProducesNoCommand()
    {
        using var context = new UiTextTestContext();
        var text = new Text();
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);

        manager.Update(new Size(400, 200));

        Assert.Equal(Size.Zero, text.DesiredSize);
        Assert.Empty(screen.CreateDrawCommandList());
        manager.Destroy();
    }

    [Fact]
    public void WrapUsesAvailableWidthAndMissingFamilyUsesDefaultFace()
    {
        using var context = new UiTextTestContext();
        var text = new Text
        {
            Content = "A long line that must wrap",
            Font = new Font("Missing Family"),
            Wrapping = TextWrapping.Wrap
        };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);

        manager.Update(new Size(60, 200));
        var command = GetCommand(screen);

        Assert.True(command.Layout.Lines.Count > 1);
        Assert.True(command.Layout.Width <= 60);
        Assert.All(command.Layout.Lines.SelectMany(line => line.GlyphRuns),
            run => Assert.Same(context.DefaultFace, run.FontFace));
        manager.Destroy();
    }

    [Fact]
    public void ColorChangeKeepsLayoutInstance()
    {
        using var context = new UiTextTestContext();
        var text = new Text { Content = "Hello" };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(400, 200));
        var before = GetCommand(screen);

        text.Color = new Color(10, 20, 30);
        var after = GetCommand(screen);

        Assert.Same(before.Layout, after.Layout);
        Assert.Equal(new Color(10, 20, 30), after.Color);
        Assert.True(text.IsMeasureValid);
        manager.Destroy();
    }

    [Fact]
    public void LayoutInputsAndAvailableWidthReplaceCachedLayout()
    {
        using var context = new UiTextTestContext();
        var text = new Text { Content = "Hello world" };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(400, 200));
        var previous = GetCommand(screen).Layout;

        ReplaceAfter(() => text.Content = "Changed");
        var alias = context.RegisterAlias();
        ReplaceAfter(() => text.Font = alias);
        ReplaceAfter(() => text.FontSize = 20);
        ReplaceAfter(() => text.LineHeight = 1.5);
        ReplaceAfter(() => text.Wrapping = TextWrapping.Wrap);
        ReplaceAfter(() => text.Alignment = TextAlignment.Center);

        manager.Update(new Size(300, 200));
        var widthChanged = GetCommand(screen).Layout;
        Assert.NotSame(previous, widthChanged);
        previous = widthChanged;

        manager.Destroy();

        void ReplaceAfter(Action change)
        {
            change();
            manager.Update(new Size(400, 200));
            var current = GetCommand(screen).Layout;
            Assert.NotSame(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void RegisteredRequestedFontKeepsCachedLayoutUntilLayoutInputChanges()
    {
        using var context = new UiTextTestContext();
        var requestedFont = new Font("Testxx Han Sans CN");
        var text = new Text { Content = "Hello", Font = requestedFont };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(400, 200));
        var initial = GetCommand(screen).Layout;
        Assert.Same(context.DefaultFace, initial.Lines[0].GlyphRuns[0].FontFace);

        var registered = context.RegisterAlias();
        manager.Update(new Size(400, 200));

        Assert.Same(initial, GetCommand(screen).Layout);

        text.Content = "Changed";
        manager.Update(new Size(400, 200));
        var refreshed = GetCommand(screen).Layout;
        Assert.NotSame(initial, refreshed);
        Assert.Same(
            context.FontManager.Match(registered),
            refreshed.Lines[0].GlyphRuns[0].FontFace);
        manager.Destroy();
    }

    [Fact]
    public void DefaultFontChangeKeepsCachedLayoutUntilLayoutInputChanges()
    {
        using var context = new UiTextTestContext();
        var text = new Text { Content = "Hello" };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(400, 200));
        var initial = GetCommand(screen).Layout;
        var replacement = context.RegisterAlias();

        context.FontManager.DefaultFont = replacement;
        manager.Update(new Size(400, 200));

        Assert.Same(initial, GetCommand(screen).Layout);

        text.Content = "Changed";
        manager.Update(new Size(400, 200));
        var refreshed = GetCommand(screen).Layout;
        Assert.NotSame(initial, refreshed);
        Assert.Same(
            context.FontManager.Match(replacement),
            refreshed.Lines[0].GlyphRuns[0].FontFace);
        manager.Destroy();
    }

    [Fact]
    public void ScaleAndLayoutRoundingReflowReplaceLogicalLayout()
    {
        using var context = new UiTextTestContext();
        var text = new Text { Content = "Hello" };
        var screen = new UiScreen(text);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(400, 200));
        var initial = GetCommand(screen).Layout;

        screen.Scale = 2;
        manager.Update(new Size(800, 400));
        var scaled = GetCommand(screen).Layout;
        Assert.NotSame(initial, scaled);

        screen.UseLayoutRounding = false;
        manager.Update(new Size(800, 400));
        Assert.NotSame(scaled, GetCommand(screen).Layout);
        manager.Destroy();
    }

    private static UiDrawTextCommand GetCommand(UiScreen screen) =>
        Assert.IsType<UiDrawTextCommand>(Assert.Single(screen.CreateDrawCommandList()));

    private static void AssertProperty<T>(
        UiProperty<T> property,
        T defaultValue,
        UiPropertyInvalidation invalidation)
    {
        Assert.Equal(typeof(Text), property.OwnerType);
        Assert.Equal(typeof(Text), property.TargetType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.Equal(invalidation, property.Invalidation);
    }
}
