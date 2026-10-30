using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Input;
using PanguEngine.UI.Rendering;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class ButtonTests
{
    [Fact]
    public void PublicSurfaceAndDefaultsMatchTheButtonContract()
    {
        var button = new Button();
        button.GetStyleRoot().UpdateStyles();

        Assert.False(typeof(Button).IsSealed);
        Assert.Equal(typeof(ContentControl), typeof(Button).BaseType);
        AssertProperty(Button.ContentProperty, (object?)null);
        Assert.Null(button.ContentTemplate);
        Assert.Equal(typeof(UIElement), Button.FontFamilyProperty.OwnerType);
        Assert.Equal(typeof(UIElement), Button.ColorProperty.OwnerType);
        Assert.Equal(new Color(242, 244, 247), button.Color);
        Assert.True(button.Focusable);
        Assert.Equal(new Thickness(12, 7), button.Padding);
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
        Assert.Equal(new SolidColorBrush(92, 103, 116), button.BorderBrush);
        Assert.Equal(new Thickness(1), button.BorderThickness);
        Assert.Equal("Button", StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        Assert.False(StyleSource(button, Region.BackgroundProperty)!.IsMaskedByLocalValue);
        Assert.False(button.ClipToBounds);
        Assert.NotNull(typeof(Button).GetEvent(
            nameof(Button.Click),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

        foreach (var propertyName in new[] { "Graphic", "Command", "ClickMode" })
        {
            Assert.Null(typeof(Button).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        Assert.Null(typeof(Button).GetProperty(
            nameof(Container.Children),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.IsAssignableFrom<IReadOnlyList<UIElement>>(button.ReadOnlyChildren);
    }

    [Fact]
    public void TextContentChangesReplaceTheDisplayedTextAndEmptyContentRemovesIt()
    {
        var button = new Button { Content = "Open" };
        var text = FindDescendant<Text>(button);
        Assert.Equal("Open", text.Content);
        Assert.False(text.IsHitTestVisible);
        Assert.Equal(TextWrapping.NoWrap, text.Wrapping);

        button.Content = "Save";
        Assert.Equal("Save", FindDescendant<Text>(button).Content);
        Assert.Null(text.Parent);

        button.Content = string.Empty;
        Assert.Empty(Descendants(button).OfType<Text>());
    }

    [Fact]
    public void ContentPropertiesSynchronizeExistingAndFutureChildren()
    {
        var font = new Font("Requested Family");
        var button = new Button
        {
            FontFamily = font.FamilyName,
            FontWeight = font.Weight,
            FontStyle = font.Style,
            FontSize = 19,
            Color = new Color(10, 20, 30)
        };

        button.Content = "Value";
        var text = FindDescendant<Text>(button);

        Assert.Equal(font.FamilyName, text.FontFamily);
        Assert.Equal(font.Weight, text.FontWeight);
        Assert.Equal(font.Style, text.FontStyle);
        Assert.Equal(19, text.FontSize);
        Assert.Equal(new Color(10, 20, 30), text.Color);

        button.FontFamily = "Replacement Family";
        button.FontSize = 21;
        button.Color = new Color(40, 50, 60);

        Assert.Equal(button.FontFamily, text.FontFamily);
        Assert.Equal(button.FontWeight, text.FontWeight);
        Assert.Equal(button.FontStyle, text.FontStyle);
        Assert.Equal(21, text.FontSize);
        Assert.Equal(new Color(40, 50, 60), text.Color);
    }

    [Fact]
    public void EmptyButtonMeasuresFromPaddingAndBorder()
    {
        var button = new Button();
        button.GetStyleRoot().UpdateStyles();

        button.Measure(Size.Infinite);
        button.Arrange(new Rect(0, 0, button.DesiredSize));

        Assert.Equal(new Size(26, 16), button.DesiredSize);
        Assert.Empty(Descendants(button).OfType<Text>());
        Assert.Equal(new Rect(13, 8, 0, 0), button.ContentBounds);
    }

    [Fact]
    public void TextMeasurementRequiresInitializedToolkitServices()
    {
        var button = new Button { Content = "Text" };

        Assert.Throws<InvalidOperationException>(() => button.Measure(Size.Infinite));

        Assert.False(button.IsMeasureValid);
        Assert.False(button.IsArrangeValid);
    }

    [Fact]
    public void ContentIsCenteredWithLayoutRounding()
    {
        const double scale = 1.25;
        var content = new Panel
        {
            Width = 30,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var button = new Button { Content = content };
        _ = new UIScene(button) { Scale = scale };
        button.GetStyleRoot().UpdateStyles();

        button.Measure(new Size(200, 80));
        button.Arrange(new Rect(0, 0, 200, 80));

        var origin = content.LocalToScene(Point.Zero);
        AssertCenteredWithinHalfPhysicalPixel(
            button.ContentBounds.X + button.ContentBounds.Width / 2,
            origin.X + content.LayoutBounds.Width / 2,
            scale);
        AssertCenteredWithinHalfPhysicalPixel(
            button.ContentBounds.Y + button.ContentBounds.Height / 2,
            origin.Y + content.LayoutBounds.Height / 2,
            scale);
    }

    [Fact]
    public void ExplicitSmallSizeKeepsCenteredOverflowAndDoesNotEnableClipping()
    {
        var content = new Panel
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var button = new Button
        {
            Content = content,
            Width = 10,
            Height = 8
        };
        _ = new UIScene(button);
        button.GetStyleRoot().UpdateStyles();

        button.Measure(new Size(100, 100));
        button.Arrange(new Rect(0, 0, 10, 8));

        var origin = content.LocalToScene(Point.Zero);
        Assert.True(origin.X < button.ContentBounds.X);
        Assert.True(origin.Y < button.ContentBounds.Y);
        Assert.False(button.ClipToBounds);
    }

    [Fact]
    public void InteractiveStatesChangeExistingBackgroundAndBorder()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 80, 32);
        var manager = new UIManager();
        var scene = new GameUIScene(root) { UseLayoutRounding = false };
        manager.Open(scene);
        manager.UpdateFrame(new Size(120, 80), 0);

        var normalCommands = scene.CreateDrawCommandList();
        var normal = normalCommands.OfType<FillRectangleCommand>().ToArray();
        Assert.Equal(5, normal.Length);
        Assert.Equal(new Color(48, 54, 62), normal[0].Color);
        Assert.DoesNotContain(normalCommands, command => command is PushClipCommand);

        manager.ProcessPointerMoved(new Point(20, 20));
        var hovered = GetFills(scene);
        Assert.Equal(5, hovered.Count);
        Assert.Equal(new Color(67, 73, 80), hovered[0].Color);
        Assert.Equal(new Rect(1, 1, 78, 30), hovered[0].Bounds);

        var builder = new DrawBuilder();
        builder.Build(scene.CreateDrawCommandList(), 120, 80, false);
        Assert.Equal(11f, builder.Vertices[0].X);
        Assert.Equal(11f, builder.Vertices[0].Y);
        Assert.Equal(89f, builder.Vertices[2].X);
        Assert.Equal(41f, builder.Vertices[2].Y);

        manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
        var pressed = GetFills(scene);
        Assert.Equal(5, pressed.Count);
        Assert.Equal(new Color(37, 42, 48), pressed[0].Color);
        Assert.All(pressed.Skip(1), command => Assert.Equal(new Color(84, 169, 255), command.Color));

        button.IsEnabled = false;
        var disabled = GetFills(scene);
        Assert.Equal(5, disabled.Count);
        Assert.Equal(new Color(27, 30, 35), disabled[0].Color);
        Assert.All(disabled.Skip(1), command => Assert.Equal(new Color(52, 58, 65), command.Color));
        Assert.Equal(new Color(139, 148, 160), button.Color);
        manager.Close();
    }

    [Fact]
    public void LocalBackgroundAndBorderOverrideStateStylesUntilCleared()
    {
        var root = new Canvas();
        var button = Place(
            root,
            new Button
            {
                Background = new SolidColorBrush(104, 43, 45),
                BorderBrush = new SolidColorBrush(157, 73, 77)
            },
            10,
            10,
            80,
            32);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(120, 80), 0);

        manager.ProcessPointerMoved(new Point(20, 20));
        Assert.Equal(new SolidColorBrush(104, 43, 45), button.Background);
        Assert.Equal(new Color(104, 43, 45), GetFills(scene)[0].Color);
        manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
        Assert.True(button.IsFocused);
        Assert.Equal(new SolidColorBrush(104, 43, 45), button.Background);
        Assert.Equal(new SolidColorBrush(157, 73, 77), button.BorderBrush);

        button.IsEnabled = false;
        var fills = GetFills(scene);
        Assert.Equal(5, fills.Count);
        Assert.Equal(new Color(104, 43, 45), fills[0].Color);
        Assert.All(fills.Skip(1), command => Assert.Equal(new Color(157, 73, 77), command.Color));
        Assert.True(StyleSource(button, Region.BackgroundProperty)!.IsMaskedByLocalValue);
        Assert.Equal(new Color(139, 148, 160), button.Color);

        button.ClearValue(Region.BackgroundProperty);
        button.ClearValue(Region.BorderBrushProperty);
        Assert.Equal(new SolidColorBrush(27, 30, 35), button.Background);
        Assert.Equal(new SolidColorBrush(52, 58, 65), button.BorderBrush);
        Assert.False(StyleSource(button, Region.BackgroundProperty)!.IsMaskedByLocalValue);
        manager.Close();
    }

    [Fact]
    public void DangerClassProvidesNormalAndCombinedStateStyles()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 80, 32);
        button.Classes.Add("danger");
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(120, 80), 0);

        Assert.Equal(new SolidColorBrush(104, 43, 45), button.Background);
        Assert.Equal(new SolidColorBrush(157, 73, 77), button.BorderBrush);
        Assert.Equal("Button.danger", StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        Assert.False(StyleSource(button, Region.BackgroundProperty)!.IsMaskedByLocalValue);

        manager.ProcessPointerMoved(new Point(20, 20));
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(118, 63, 65), button.Background);
        Assert.Equal("Button.danger:hover", StyleSource(button, Region.BackgroundProperty)?.SelectorText);

        manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(81, 34, 35), button.Background);
        Assert.Equal(new SolidColorBrush(84, 169, 255), button.BorderBrush);
        Assert.Equal("Button.danger:pressed", StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        Assert.Equal("Button.danger:focus", StyleSource(button, Region.BorderBrushProperty)?.SelectorText);

        button.IsEnabled = false;
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(58, 24, 25), button.Background);
        Assert.Equal(new SolidColorBrush(88, 41, 43), button.BorderBrush);
        Assert.Equal(new Color(139, 148, 160), button.Color);
        Assert.Equal("Button.danger:disabled", StyleSource(button, Region.BackgroundProperty)?.SelectorText);

        button.Classes.Remove("danger");
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(27, 30, 35), button.Background);
        Assert.Equal(new SolidColorBrush(52, 58, 65), button.BorderBrush);
        manager.Close();
    }

    [Fact]
    public void StyleDrivenContentRemovalLeavesDetachedChildStylesPending()
    {
        var button = new StyledContentHost();
        var scene = new UIScene(button);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(
                    StyleSelector.For<StyledContentHost>(),
                    [StyleSetter.Create(StyledContentHost.ShowContentProperty, true)]),
                new StyleRule(
                    StyleSelector.For<Text>(),
                    [StyleSetter.Create(UIElement.OpacityProperty, 0.4)])
            ])
        ]);
        scene.Root!.UpdateStyles();
        var oldText = FindDescendant<Text>(button);
        scene.Root!.UpdateStyles();
        var opacityNotifications = 0;
        oldText.PropertyChanged += (_, e) =>
            opacityNotifications += ReferenceEquals(e.Property, UIElement.OpacityProperty) ? 1 : 0;

        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(
                    StyleSelector.For<StyledContentHost>(),
                    [StyleSetter.Create(StyledContentHost.ShowContentProperty, false)]),
                new StyleRule(
                    StyleSelector.For<Text>(),
                    [StyleSetter.Create(UIElement.OpacityProperty, 0.8)])
            ])
        ]);
        scene.Root!.UpdateStyles();

        Assert.Null(button.Content);
        Assert.Null(oldText.Scene);
        Assert.Equal(0.4, oldText.Opacity);
        Assert.Equal(0, opacityNotifications);
        oldText.UpdateStyles();
        Assert.Equal(1, oldText.Opacity);
        Assert.Equal(1, opacityNotifications);
    }

    [Fact]
    public void FocusChangesExistingBorderWithoutChangingLayoutOrGeometry()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 80, 32);
        var manager = new UIManager();
        var scene = new GameUIScene(root) { UseLayoutRounding = false };
        manager.Open(scene);
        manager.UpdateFrame(new Size(120, 80), 0);
        var originalBounds = GetFills(scene).Select(command => command.Bounds).ToArray();
        Assert.True(button.Focus());

        var fills = GetFills(scene);

        Assert.True(button.IsMeasureValid);
        Assert.True(button.IsArrangeValid);
        Assert.Equal(new Thickness(1), button.BorderThickness);
        Assert.Equal(5, fills.Count);
        Assert.Equal(originalBounds, fills.Select(command => command.Bounds).ToArray());
        Assert.All(fills.Skip(1), command =>
            Assert.Equal(new Color(84, 169, 255), command.Color));
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledStyleWinsWhenOtherPseudoClassesRemainActive(bool danger)
    {
        var button = new Button();
        if (danger)
            button.Classes.Add("danger");
        button.SetHovered(true);
        button.SetFocused(true);

        button.IsEnabled = false;
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(
            danger ? new SolidColorBrush(58, 24, 25) : new SolidColorBrush(27, 30, 35),
            button.Background);
        Assert.Equal(
            danger ? new SolidColorBrush(88, 41, 43) : new SolidColorBrush(52, 58, 65),
            button.BorderBrush);
        Assert.Equal(new Color(139, 148, 160), button.Color);
    }

    [Fact]
    public void DisabledColorIsInheritedByTextAndHonorsLocalOverride()
    {
        var button = new Button { Content = "Delete" };
        button.Classes.Add("danger");
        var text = FindDescendant<Text>(button);

        button.IsEnabled = false;
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(new Color(139, 148, 160), text.Color);
        button.Color = new Color(240, 230, 220);
        Assert.Equal(new Color(240, 230, 220), text.Color);
        button.ClearValue(UIElement.ColorProperty);
        Assert.Equal(new Color(139, 148, 160), text.Color);
    }

    [Fact]
    public void LowScaleCanRoundFocusedBorderToZero()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 40, 20);
        var manager = new UIManager();
        var scene = new GameUIScene(root) { Scale = 0.25 };
        manager.Open(scene);
        manager.UpdateFrame(new Size(30, 20), 0);
        Assert.True(button.Focus());

        Assert.DoesNotContain(
            GetFills(scene),
            command => command.Color == new Color(84, 169, 255));
        manager.Close();
    }

    [Fact]
    public void FocusDoesNotAddGeometryWhenLocalBorderThicknessIsZero()
    {
        var root = new Canvas();
        var button = Place(root, new Button { BorderThickness = Thickness.Zero }, 10, 10, 80, 32);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(120, 80), 0);

        Assert.True(button.Focus());

        Assert.Single(GetFills(scene));
        Assert.Equal(Thickness.Zero, button.BorderThickness);
        manager.Close();
    }

    [Fact]
    public void ImageKeepsItsViewClipWhileTextInheritsTheOuterClip()
    {
        using var context = new TextTestContext();
        var root = new Canvas();
        var button = Place(
            root,
            new Button { Content = CreateImageContent(CreateImage(8, 4), "Go") },
            10,
            10,
            100,
            40);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(140, 80), 0);

        var commands = scene.CreateDrawCommandList();
        var imageCommand = Assert.Single(commands.OfType<DrawImageCommand>());
        var textCommand = Assert.Single(commands.OfType<DrawTextCommand>());
        var image = FindDescendant<ImageView>(button);

        Assert.Equal(
            new Rect(0, 0, image.LayoutBounds.Width, image.LayoutBounds.Height),
            Assert.Single(commands.OfType<PushClipCommand>()).Clip);
        var ordered = commands.ToArray();
        Assert.True(Array.IndexOf(ordered, imageCommand) < Array.IndexOf(ordered, textCommand));
        manager.Close();
    }

    [Fact]
    public void LeftPointerClickRunsRawEventThenClickAndStopsAtButton()
    {
        var root = new Canvas();
        _ = Place(root, new Button(), 0, 0, 80, 32);
        var button = Assert.IsType<Button>(root.Children[0]);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var calls = new List<string>();
        button.PointerClicked += (_, eventArgs) => calls.Add($"raw:{eventArgs.Handled}");
        button.Click += (_, _) => calls.Add("click");
        root.PointerClicked += (_, _) => calls.Add("root");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["raw:False", "click"], calls);
        manager.Close();
    }

    [Fact]
    public void HandledRawClickCancelsButtonActivation()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var ancestorClicks = 0;
        var clicks = 0;
        button.PointerClicked += (_, eventArgs) => eventArgs.Handled = true;
        button.Click += (_, _) => clicks++;
        root.PointerClicked += (_, _) => ancestorClicks++;

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(0, clicks);
        Assert.Equal(0, ancestorClicks);
        manager.Close();
    }

    [Fact]
    public void DisablingAnAncestorInRawClickDoesNotCancelTheCurrentDefaultAction()
    {
        var (manager, _, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.PointerClicked += (_, _) =>
        {
            calls.Add("raw");
            root.IsEnabled = false;
        };
        button.Click += (_, _) => calls.Add("click");

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["raw", "click"], calls);
        manager.Close();
    }

    [Fact]
    public void NonLeftButtonsContinueBubblingWithoutClick()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var clicks = 0;
        var ancestorButtons = new List<MouseButton>();
        button.Click += (_, _) => clicks++;
        root.PointerClicked += (_, eventArgs) =>
        {
            Assert.False(eventArgs.Handled);
            ancestorButtons.Add(eventArgs.Button);
        };

        foreach (var mouseButton in new[] { MouseButton.Right, MouseButton.Middle, MouseButton.Button12 })
            Click(manager, new Point(5, 5), mouseButton);

        Assert.Equal(0, clicks);
        Assert.Equal([MouseButton.Right, MouseButton.Middle, MouseButton.Button12], ancestorButtons);
        manager.Close();
    }

    [Fact]
    public void ReleaseOutsideOrAfterCrossSceneMoveDoesNotClick()
    {
        var firstRoot = new Canvas();
        var button = Place(firstRoot, new Button(), 0, 0, 40, 40);
        var firstManager = new UIManager();
        var firstScene = new GameUIScene(firstRoot);
        var secondRoot = new Canvas();
        var secondManager = new UIManager();
        var secondScene = new GameUIScene(secondRoot);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        firstManager.Open(firstScene);
        secondManager.Open(secondScene);
        firstManager.UpdateFrame(new Size(100, 100), 0);
        secondManager.UpdateFrame(new Size(100, 100), 0);

        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        firstManager.ProcessPointerReleased(new Point(80, 80), MouseButton.Left, KeyModifiers.None);
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        secondRoot.Children.Add(button);
        firstManager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(0, clicks);
        Assert.False(button.IsPressed);
        Assert.Same(secondScene, button.Scene);
        firstManager.Close();
        secondManager.Close();
    }

    [Fact]
    public void RawPointerClickRemovalCancelsButtonActivation()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.PointerClicked += (_, _) =>
        {
            calls.Add("raw");
            Assert.True(root.Children.Remove(button));
        };
        button.Click += (_, _) => calls.Add("click");

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["raw"], calls);
        Assert.Null(button.Parent);
        manager.Close();
    }

    [Fact]
    public void RawPointerExceptionStopsClickAndKeepsTheExceptionInstance()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var error = new InvalidOperationException("raw click");
        var clicks = 0;
        button.PointerClicked += (_, _) => throw error;
        button.Click += (_, _) => clicks++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));

        Assert.Same(error, actual);
        Assert.Equal(0, clicks);
        Assert.False(button.IsPressed);
        manager.Close();
    }

    [Fact]
    public void PointerReleasedExceptionSuppressesPointerClickedAndClick()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var error = new InvalidOperationException("release");
        var rawClicks = 0;
        var clicks = 0;
        button.PointerReleased += (_, _) => throw error;
        button.PointerClicked += (_, _) => rawClicks++;
        button.Click += (_, _) => clicks++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));

        Assert.Same(error, actual);
        Assert.Equal(0, rawClicks);
        Assert.Equal(0, clicks);
        Assert.False(button.IsPressed);
        manager.Close();
    }

    [Fact]
    public void EnterClicksOncePerKeyPairAndHandlesItsEvents()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.KeyDown += (_, eventArgs) => calls.Add($"down:{eventArgs.Handled}");
        button.KeyUp += (_, eventArgs) => calls.Add($"up:{eventArgs.Handled}");
        button.Click += (_, _) => calls.Add("click");
        root.KeyDown += (_, _) => calls.Add("root-down");
        root.KeyUp += (_, _) => calls.Add("root-up");
        Assert.True(button.Focus());

        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(
            ["down:False", "click", "down:False", "up:False", "down:False", "click"],
            calls);
        manager.Close();
    }

    [Fact]
    public void SpaceClicksOnKeyUpAndUsesPressedAppearanceWhilePaired()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.KeyDown += (_, eventArgs) => calls.Add($"down:{eventArgs.Handled}");
        button.KeyUp += (_, eventArgs) => calls.Add($"up:{eventArgs.Handled}");
        button.Click += (_, _) => calls.Add("click");
        Assert.True(button.Focus());

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        scene.Root!.UpdateStyles();

        Assert.Equal(["down:False", "down:False"], calls);
        Assert.Equal(
            "Button:pressed",
            StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        Assert.Contains(
            GetFills(scene),
            command => command.Color == new Color(37, 42, 48));

        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(["down:False", "down:False", "up:False", "click"], calls);
        Assert.DoesNotContain(
            GetFills(scene),
            command => command.Color == new Color(37, 42, 48));
        Assert.Equal("Button", StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        manager.Close();
    }

    [Fact]
    public void LostFocusClearsSpacePressedStyleSource()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        scene.Root!.UpdateStyles();
        Assert.Equal(
            "Button:pressed",
            StyleSource(button, Region.BackgroundProperty)?.SelectorText);

        scene.ClearFocus();
        scene.Root!.UpdateStyles();

        Assert.Equal("Button", StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        manager.Close();
    }

    [Fact]
    public void PressedStyleRemainsUntilPointerAndSpaceAreBothReleased()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        Assert.True(button.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        scene.Root!.UpdateStyles();

        Assert.Equal(
            "Button:pressed",
            StyleSource(button, Region.BackgroundProperty)?.SelectorText);

        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        scene.Root!.UpdateStyles();

        Assert.Equal(
            button.IsHovered ? "Button:hover" : "Button",
            StyleSource(button, Region.BackgroundProperty)?.SelectorText);
        manager.Close();
    }

    [Fact]
    public void OtherKeysContinueBubblingAndUnmatchedActivationKeyUpDoesNotClick()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var rootKeys = new List<Key>();
        var clicks = 0;
        root.KeyDown += (_, eventArgs) => rootKeys.Add(eventArgs.Key);
        root.KeyUp += (_, eventArgs) => rootKeys.Add(eventArgs.Key);
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());

        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        manager.ProcessKeyUp(Key.A, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal([Key.A, Key.A], rootKeys);
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void LostFocusClearsEnterPairBeforeNewFocus()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        scene.ClearFocus();
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(2, clicks);
        manager.Close();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LifecycleLossCancelsSpaceActivation(int cleanupKind)
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        switch (cleanupKind)
        {
            case 0:
                scene.ClearFocus();
                break;
            case 1:
                button.IsEnabled = false;
                break;
            case 2:
                Assert.True(root.Children.Remove(button));
                break;
            case 3:
                scene.Root = new Canvas();
                break;
            case 4:
                manager.Close();
                break;
        }

        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(0, clicks);
        if (manager.CurrentScene is not null)
            manager.Close();
    }

    [Fact]
    public void RawKeyboardTreeAndSceneChangesCancelActivation()
    {
        var (enterManager, enterScene, enterRoot, enterButton) = OpenButtonScene();
        var enterClicks = 0;
        enterButton.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter)
                Assert.True(enterRoot.Children.Remove(enterButton));
        };
        enterButton.Click += (_, _) => enterClicks++;
        Assert.True(enterButton.Focus());

        enterManager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(0, enterClicks);
        enterManager.Close();

        var (spaceManager, spaceScene, spaceRoot, spaceButton) = OpenButtonScene();
        var spaceClicks = 0;
        spaceButton.KeyUp += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Space)
                spaceManager.Close();
        };
        spaceButton.Click += (_, _) => spaceClicks++;
        Assert.True(spaceButton.Focus());
        spaceManager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        spaceManager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(0, spaceClicks);
        Assert.Null(spaceManager.CurrentScene);
    }

    [Fact]
    public void RawKeyboardExceptionsKeepPhysicalPairTransitionsAndStopClick()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var enterError = new InvalidOperationException("enter");
        var spaceError = new InvalidOperationException("space");
        var clicks = 0;
        EventHandler<KeyEventArgs> enterHandler = (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter)
                throw enterError;
        };
        EventHandler<KeyEventArgs> spaceHandler = (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Space)
                throw spaceError;
        };
        button.KeyDown += enterHandler;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());

        var actualEnter = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessKeyDown(Key.Enter, KeyModifiers.None));
        Assert.Same(enterError, actualEnter);
        button.KeyDown -= enterHandler;
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        Assert.Equal(0, clicks);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        button.KeyUp += spaceHandler;
        var actualSpace = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessKeyUp(Key.Space, KeyModifiers.None));
        Assert.Same(spaceError, actualSpace);
        button.KeyUp -= spaceHandler;
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(0, clicks);

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(1, clicks);
        manager.Close();
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void RefocusingInsideAKeyCallbackDoesNotRestoreTheActivationPair(Key key)
    {
        var (manager, scene, _, button) = OpenButtonScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        EventHandler<KeyEventArgs> refocus = (_, _) =>
        {
            scene.ClearFocus();
            Assert.True(button.Focus());
        };
        if (key == Key.Enter)
            button.KeyDown += refocus;
        else
            button.KeyUp += refocus;
        Assert.True(button.Focus());

        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);

        Assert.Equal(0, clicks);
        Assert.False(button.HasPseudoClass(PseudoClass.Pressed));
        manager.Close();
    }

    [Fact]
    public void ClickCanCloseSceneAndLaterSubscribersStillRun()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var calls = new List<string>();
        InvalidOperationException? reopenError = null;
        button.Click += (_, _) =>
        {
            calls.Add("first");
            manager.Close();
            reopenError = Assert.Throws<InvalidOperationException>(() => manager.Open(scene));
        };
        button.Click += (_, _) => calls.Add("second");

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["first", "second"], calls);
        Assert.NotNull(reopenError);
        Assert.Null(manager.CurrentScene);
    }

    [Fact]
    public void ClickCannotStartNestedPhysicalInput()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        InvalidOperationException? nestedError = null;
        button.Click += (_, _) => nestedError = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessKeyDown(Key.A, KeyModifiers.None));

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.NotNull(nestedError);
        manager.Close();
    }

    [Fact]
    public void ClickExceptionKeepsInstanceStopsSubscribersAndDoesNotRollbackInput()
    {
        var (manager, scene, root, button) = OpenButtonScene();
        var error = new InvalidOperationException("click");
        var laterCalls = 0;
        button.Click += (_, _) => throw error;
        button.Click += (_, _) => laterCalls++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));

        Assert.Same(error, actual);
        Assert.Equal(0, laterCalls);
        Assert.False(button.IsPressed);
        Assert.True(button.IsFocused);
        Assert.Same(scene, button.Scene);
        manager.Close();
    }

    [Fact]
    public void PointerEnterAndSpaceUseTheSameOverridableActivation()
    {
        var root = new Canvas();
        var button = Place(root, new ActivationButton(), 0, 0, 80, 32);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Click(manager, new Point(5, 5), MouseButton.Left);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None, isRepeat: true);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, isRepeat: true);
        Assert.Equal(2, button.Activations);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(3, button.Activations);
        Assert.Equal(3, clicks);
        manager.Close();
    }

    private sealed class ActivationButton : Button
    {
        public int Activations { get; private set; }

        protected override void OnClick()
        {
            Activations++;
            base.OnClick();
        }
    }

    private static (UIManager Manager, GameUIScene Scene, Canvas Root, Button Button) OpenButtonScene()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var button = Place(root, new Button(), 0, 0, 80, 32);
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        return (manager, scene, root, button);
    }

    private static T Place<T>(
        Canvas parent,
        T child,
        double x,
        double y,
        double width,
        double height)
        where T : UIElement
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
        return child;
    }

    private static void Click(UIManager manager, Point point, MouseButton button)
    {
        manager.ProcessPointerPressed(point, button, KeyModifiers.None);
        manager.ProcessPointerReleased(point, button, KeyModifiers.None);
    }

    private static IReadOnlyList<FillRectangleCommand> GetFills(UIScene scene)
    {
        scene.Root!.UpdateStyles();
        return scene.CreateDrawCommandList().OfType<FillRectangleCommand>().ToArray();
    }

    private static StyleValueSource? StyleSource(UIElement node, Property property) =>
        node.GetStyleValueSources(property).SingleOrDefault();

    private static Image CreateImage(int width, int height) =>
        Image.FromRgba(new byte[checked(width * height * 4)], width, height);

    private static StackPanel CreateImageContent(Image image, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ImageView
        {
            Source = image,
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new Text { Content = value, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static T FindDescendant<T>(Container parent) where T : UIElement =>
        Assert.Single(Descendants(parent).OfType<T>());

    private static IEnumerable<UIElement> Descendants(Container parent)
    {
        foreach (var child in parent.ReadOnlyChildren)
        {
            yield return child;
            if (child is Container nested)
                foreach (var descendant in Descendants(nested))
                    yield return descendant;
        }
    }

    private sealed class StyledContentHost : ContentControl
    {
        internal static readonly Property<bool> ShowContentProperty =
            Property.Register<StyledContentHost, bool>(nameof(ShowContent),
                onChanged: static (host, _, visible) =>
                    host.Content = visible ? new Text { Content = "Old" } : null);

        internal bool ShowContent
        {
            get => GetValue(ShowContentProperty);
            set => SetValue(ShowContentProperty, value);
        }
    }

    private static void AssertCenteredWithinHalfPhysicalPixel(
        double expected,
        double actual,
        double scale) =>
        Assert.InRange(Math.Abs(expected - actual) * scale, 0, 0.5 + 1e-9);

    private static void AssertProperty<T>(
        Property<T> property,
        T defaultValue)
    {
        Assert.Equal(typeof(ContentControl), property.OwnerType);
        Assert.Equal(typeof(ContentControl), property.TargetType);
        Assert.Equal(typeof(T), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.False(property.IsReadOnly);
    }
}
