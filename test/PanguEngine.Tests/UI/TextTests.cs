using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TextTests
{
    [Fact]
    public void InheritedTypographyReflowsWhileInheritedColorReusesLayout()
    {
        using var context = new TextTestContext();
        var alias = context.RegisterAlias();
        var root = new Panel();
        var text = new Text { Content = "Hello" };
        root.Children.Add(text);
        var scene = new GameUIScene(root) { UseLayoutRounding = false };
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var initial = GetCommand(scene);

        root.FontFamily = alias.FamilyName;
        root.LineHeight = 1.5;

        Assert.False(text.IsMeasureValid);
        manager.UpdateFrame(new Size(400, 200), 0);
        var reflow = GetCommand(scene);
        Assert.NotSame(initial.Layout, reflow.Layout);
        Assert.Same(context.FontManager.Match(alias), reflow.Layout.Lines[0].GlyphRuns[0].FontFace);
        Assert.True(reflow.Layout.Height > initial.Layout.Height);

        root.Color = new Color(10, 20, 30);
        var recolored = GetCommand(scene);

        Assert.Same(reflow.Layout, recolored.Layout);
        Assert.Equal(root.Color, recolored.Color);
        manager.Destroy();
    }

    [Fact]
    public void NewMeasurementRequiresInitializedToolkitServices()
    {
        var text = new Text { Content = "Hello" };

        Assert.Throws<InvalidOperationException>(() => text.Measure(Size.Infinite));
    }

    [Fact]
    public void DetachedAndSceneOwnedTextUseSharedToolkitServices()
    {
        using var context = new TextTestContext();
        var detached = new Text { Content = "Detached" };
        var sceneOwned = new Text { Content = "Screen owned" };
        _ = new UIScene(sceneOwned);

        detached.Measure(Size.Infinite);
        sceneOwned.Measure(Size.Infinite);

        Assert.True(detached.DesiredSize.Width > 0);
        Assert.True(sceneOwned.DesiredSize.Width > 0);
    }

    [Fact]
    public void PropertiesExposeFixedDefaults()
    {
        var text = new Text();

        AssertProperty(Text.ContentProperty, string.Empty);
        Assert.Equal(typeof(UIElement), Text.FontFamilyProperty.OwnerType);
        Assert.Equal(typeof(UIElement), Text.ColorProperty.OwnerType);
        Assert.Equal(typeof(UIElement), Text.LineHeightProperty.OwnerType);
        Assert.Equal(new Color(255, 255, 255), Text.ColorProperty.DefaultValue);
        Assert.Equal(1d, Text.LineHeightProperty.DefaultValue);
        AssertProperty(Text.WrappingProperty, TextWrapping.NoWrap);
        AssertProperty(Text.AlignmentProperty, TextAlignment.Left);

        Assert.Equal(string.Empty, text.Content);
        Assert.Equal(string.Empty, text.FontFamily);
        Assert.Equal(FontWeight.Normal, text.FontWeight);
        Assert.Equal(FontStyle.Normal, text.FontStyle);
        Assert.Equal(16, text.FontSize);
        Assert.Equal(new Color(255, 255, 255), text.Color);
        Assert.Equal(1, text.LineHeight);
        Assert.Equal(TextWrapping.NoWrap, text.Wrapping);
        Assert.Equal(TextAlignment.Left, text.Alignment);
    }

    [Fact]
    public void DefaultsUseCurrentDefaultFaceAndMeasureDrawShareLayout()
    {
        using var context = new TextTestContext();
        var text = new Text { Content = "Hello" };
        var scene = new GameUIScene(text) { UseLayoutRounding = false };
        var manager = new UIManager();
        manager.Open(scene);

        manager.UpdateFrame(new Size(400, 200), 0);
        var first = GetCommand(scene);
        var second = GetCommand(scene);

        Assert.Same(first.Layout, second.Layout);
        Assert.Same(context.DefaultFace, first.Layout.Lines[0].GlyphRuns[0].FontFace);
        Assert.Equal(text.DesiredSize.Width, first.Layout.Width);
        Assert.Equal(text.DesiredSize.Height, first.Layout.Height);
        manager.Destroy();
    }

    [Fact]
    public void EmptyContentMeasuresEmptyAndProducesNoCommand()
    {
        using var context = new TextTestContext();
        var text = new Text();
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);

        manager.UpdateFrame(new Size(400, 200), 0);

        Assert.Equal(Size.Zero, text.DesiredSize);
        Assert.Empty(scene.CreateDrawCommandList());
        manager.Destroy();
    }

    [Fact]
    public void WrapUsesAvailableWidthAndMissingFamilyUsesDefaultFace()
    {
        using var context = new TextTestContext();
        var text = new Text
        {
            Content = "A long line that must wrap",
            FontFamily = "Missing Family",
            Wrapping = TextWrapping.Wrap
        };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);

        manager.UpdateFrame(new Size(60, 200), 0);
        var command = GetCommand(scene);

        Assert.True(command.Layout.Lines.Count > 1);
        Assert.True(command.Layout.Width <= 60);
        Assert.All(command.Layout.Lines.SelectMany(line => line.GlyphRuns),
            run => Assert.Same(context.DefaultFace, run.FontFace));
        manager.Destroy();
    }

    [Fact]
    public void ColorChangeKeepsLayoutInstance()
    {
        using var context = new TextTestContext();
        var text = new Text { Content = "Hello" };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var before = GetCommand(scene);

        text.Color = new Color(10, 20, 30);
        var after = GetCommand(scene);

        Assert.Same(before.Layout, after.Layout);
        Assert.Equal(new Color(10, 20, 30), after.Color);
        Assert.True(text.IsMeasureValid);
        manager.Destroy();
    }

    [Fact]
    public void LayoutInputsAndAvailableWidthReplaceCachedLayout()
    {
        using var context = new TextTestContext();
        var text = new Text { Content = "Hello world" };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var previous = GetCommand(scene).Layout;

        ReplaceAfter(() => text.Content = "Changed");
        var alias = context.RegisterAlias();
        ReplaceAfter(() => text.FontFamily = alias.FamilyName);
        ReplaceAfter(() => text.FontWeight = FontWeight.Bold);
        ReplaceAfter(() => text.FontStyle = FontStyle.Italic);
        ReplaceAfter(() => text.FontSize = 20);
        ReplaceAfter(() => text.LineHeight = 1.5);
        ReplaceAfter(() => text.Wrapping = TextWrapping.Wrap);
        ReplaceAfter(() => text.Alignment = TextAlignment.Center);

        manager.UpdateFrame(new Size(300, 200), 0);
        var widthChanged = GetCommand(scene).Layout;
        Assert.NotSame(previous, widthChanged);
        previous = widthChanged;

        manager.Destroy();

        void ReplaceAfter(Action change)
        {
            change();
            manager.UpdateFrame(new Size(400, 200), 0);
            var current = GetCommand(scene).Layout;
            Assert.NotSame(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void RegisteredRequestedFontKeepsCachedLayoutUntilLayoutInputChanges()
    {
        using var context = new TextTestContext();
        var requestedFont = new Font("Testxx Han Sans CN");
        var text = new Text
        {
            Content = "Hello",
            FontFamily = requestedFont.FamilyName,
            FontWeight = requestedFont.Weight,
            FontStyle = requestedFont.Style
        };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var initial = GetCommand(scene).Layout;
        Assert.Same(context.DefaultFace, initial.Lines[0].GlyphRuns[0].FontFace);

        var registered = context.RegisterAlias();
        manager.UpdateFrame(new Size(400, 200), 0);

        Assert.Same(initial, GetCommand(scene).Layout);

        text.Content = "Changed";
        manager.UpdateFrame(new Size(400, 200), 0);
        var refreshed = GetCommand(scene).Layout;
        Assert.NotSame(initial, refreshed);
        Assert.Same(
            context.FontManager.Match(registered),
            refreshed.Lines[0].GlyphRuns[0].FontFace);
        manager.Destroy();
    }

    [Fact]
    public void DefaultFontChangeKeepsCachedLayoutUntilLayoutInputChanges()
    {
        using var context = new TextTestContext();
        var text = new Text { Content = "Hello" };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var initial = GetCommand(scene).Layout;
        var replacement = context.RegisterAlias();

        context.FontManager.DefaultFont = replacement;
        manager.UpdateFrame(new Size(400, 200), 0);

        Assert.Same(initial, GetCommand(scene).Layout);

        text.Content = "Changed";
        manager.UpdateFrame(new Size(400, 200), 0);
        var refreshed = GetCommand(scene).Layout;
        Assert.NotSame(initial, refreshed);
        Assert.Same(
            context.FontManager.Match(replacement),
            refreshed.Lines[0].GlyphRuns[0].FontFace);
        manager.Destroy();
    }

    [Fact]
    public void ScaleAndLayoutRoundingReflowReplaceLogicalLayout()
    {
        using var context = new TextTestContext();
        var text = new Text { Content = "Hello" };
        var scene = new GameUIScene(text);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var initial = GetCommand(scene).Layout;

        scene.Scale = 2;
        manager.UpdateFrame(new Size(800, 400), 0);
        var scaled = GetCommand(scene).Layout;
        Assert.NotSame(initial, scaled);

        scene.UseLayoutRounding = false;
        manager.UpdateFrame(new Size(800, 400), 0);
        Assert.NotSame(scaled, GetCommand(scene).Layout);
        manager.Destroy();
    }

    [Fact]
    public void InheritedFontSizeInvalidatesMeasurementAndReplacesTextLayout()
    {
        using var context = new TextTestContext();
        var root = new Panel { FontSize = 18 };
        var text = new Text { Content = "Inherited size" };
        root.Children.Add(text);
        var scene = new GameUIScene(root);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        var before = GetCommand(scene);
        Assert.Equal(18d, before.FontSize);

        root.FontSize = 28;

        Assert.False(text.IsMeasureValid);
        manager.UpdateFrame(new Size(400, 200), 0);
        var after = GetCommand(scene);
        Assert.Equal(28d, after.FontSize);
        Assert.NotSame(before.Layout, after.Layout);
        Assert.True(after.Layout.Height > before.Layout.Height);
        manager.Destroy();
    }

    private static DrawTextCommand GetCommand(UIScene scene) =>
        Assert.Single(scene.CreateDrawCommandList().OfType<DrawTextCommand>());

    private static void AssertProperty<T>(
        Property<T> property,
        T defaultValue)
    {
        Assert.Equal(typeof(Text), property.OwnerType);
        Assert.Equal(typeof(Text), property.TargetType);
        Assert.Equal(defaultValue, property.DefaultValue);
    }
}