using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class ButtonTests
{
    [Fact]
    public void PublicSurfaceAndDefaultsMatchTheButtonContract()
    {
        var button = new Button();

        Assert.True(typeof(Button).IsSealed);
        Assert.Equal(typeof(Control), typeof(Button).BaseType);
        AssertProperty(
            Button.TextProperty,
            string.Empty,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(
            Button.FontProperty,
            new Font(string.Empty),
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(
            Button.FontSizeProperty,
            16d,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(
            Button.ForegroundProperty,
            new Color(242, 244, 247),
            UiPropertyInvalidation.Render);
        AssertProperty(
            Button.IconProperty,
            (UiImage?)null,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(
            Button.IconSizeProperty,
            16d,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(Button.SpacingProperty, 6d, UiPropertyInvalidation.Measure);
        Assert.True(button.Focusable);
        Assert.Equal(new Thickness(12, 7), button.Padding);
        Assert.Equal(new SolidColorBrush(new Color(48, 54, 62)), button.Background);
        Assert.Equal(new SolidColorBrush(new Color(92, 103, 116)), button.BorderBrush);
        Assert.Equal(new Thickness(1), button.BorderThickness);
        Assert.False(button.ClipToBounds);
        Assert.Empty(button.Children);
        Assert.NotNull(typeof(Button).GetEvent(
            nameof(Button.Click),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

        foreach (var propertyName in new[] { "Content", "Graphic", "Command", "ClickMode" })
        {
            Assert.Null(typeof(Button).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }
        Assert.Null(typeof(Button).GetProperty(
            nameof(Parent.Children),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.IsAssignableFrom<IReadOnlyList<UiNode>>(button.Children);
    }

    [Fact]
    public void TextAndIconCreateOnlyRequiredChildrenInStableOrder()
    {
        var firstIcon = CreateImage(8, 4);
        var secondIcon = CreateImage(4, 8);
        var button = new Button { Text = "Open", Icon = firstIcon };

        var image = Assert.IsType<ImageView>(button.Children[0]);
        var text = Assert.IsType<Text>(button.Children[1]);
        Assert.Same(firstIcon, image.Source);
        Assert.Equal("Open", text.Content);
        Assert.False(image.IsHitTestVisible);
        Assert.False(text.IsHitTestVisible);
        Assert.Equal(ImageStretch.Uniform, image.Stretch);
        Assert.Equal(TextWrapping.NoWrap, text.Wrapping);

        button.Icon = secondIcon;
        button.Text = string.Empty;

        Assert.Single(button.Children);
        Assert.Same(image, button.Children[0]);
        Assert.Same(secondIcon, image.Source);
        Assert.Null(text.Parent);

        button.Text = "Save";

        Assert.Equal(2, button.Children.Count);
        Assert.Same(image, button.Children[0]);
        Assert.NotSame(text, button.Children[1]);

        button.Icon = null;

        Assert.IsType<Text>(Assert.Single(button.Children));
        Assert.Null(image.Parent);
    }

    [Fact]
    public void ContentPropertiesSynchronizeExistingAndFutureChildren()
    {
        var icon = CreateImage(4, 4);
        var font = new Font("Requested Family");
        var button = new Button
        {
            Font = font,
            FontSize = 19,
            Foreground = new Color(10, 20, 30),
            IconSize = 23
        };

        Assert.Empty(button.Children);

        button.Text = "Value";
        button.Icon = icon;
        var image = Assert.IsType<ImageView>(button.Children[0]);
        var text = Assert.IsType<Text>(button.Children[1]);

        Assert.Equal(font, text.Font);
        Assert.Equal(19, text.FontSize);
        Assert.Equal(new Color(10, 20, 30), text.Color);
        Assert.Equal(23, image.Width);
        Assert.Equal(23, image.Height);

        button.Font = new Font("Replacement Family");
        button.FontSize = 21;
        button.Foreground = new Color(40, 50, 60);
        button.IconSize = 17;

        Assert.Equal(button.Font, text.Font);
        Assert.Equal(21, text.FontSize);
        Assert.Equal(new Color(40, 50, 60), text.Color);
        Assert.Equal(17, image.Width);
        Assert.Equal(17, image.Height);
    }

    [Fact]
    public void EmptyButtonMeasuresFromPaddingAndBorderWithoutChildren()
    {
        var button = new Button();

        button.Measure(Size.Infinite);
        button.Arrange(new Rect(0, 0, button.DesiredSize));

        Assert.Equal(new Size(26, 16), button.DesiredSize);
        Assert.Empty(button.Children);
        Assert.Equal(new Rect(13, 8, 0, 0), button.ContentBounds);
    }

    [Fact]
    public void IconUsesAStableSquareSlotWithoutTextServices()
    {
        var button = new Button
        {
            Icon = CreateImage(20, 10),
            IconSize = 16
        };

        button.Measure(Size.Infinite);
        button.Arrange(new Rect(0, 0, button.DesiredSize));

        var image = Assert.IsType<ImageView>(Assert.Single(button.Children));
        Assert.Equal(new Size(42, 32), button.DesiredSize);
        Assert.Equal(new Size(16, 16), image.DesiredSize);
        Assert.Equal(new Rect(13, 8, 16, 16), image.LayoutBounds);
    }

    [Fact]
    public void TextMeasurementRequiresInitializedTextServices()
    {
        var button = new Button { Text = "Text" };

        Assert.Throws<InvalidOperationException>(() => button.Measure(Size.Infinite));

        Assert.False(button.IsMeasureValid);
        Assert.False(button.IsArrangeValid);
    }

    [Fact]
    public void IconAndTextAreCenteredWithRoundedSpacing()
    {
        const double scale = 1.25;
        using var context = new UiTextTestContext();
        var button = new Button
        {
            Icon = CreateImage(20, 10),
            IconSize = 16,
            Text = "Play",
            Spacing = 5.3
        };
        _ = new UiScreen(button) { Scale = scale };

        button.Measure(new Size(200, 80));
        button.Arrange(new Rect(0, 0, 200, 80));

        var image = Assert.IsType<ImageView>(button.Children[0]);
        var text = Assert.IsType<Text>(button.Children[1]);
        Assert.Equal(16, image.DesiredSize.Width, 12);
        Assert.Equal(
            image.LayoutBounds.X + image.LayoutBounds.Width + 5.6,
            text.LayoutBounds.X,
            12);
        Assert.Equal(
            button.ContentBounds.X + button.ContentBounds.Width / 2,
            (image.LayoutBounds.X + text.LayoutBounds.X + text.LayoutBounds.Width) / 2,
            12);
        Assert.Equal(
            button.ContentBounds.Y + button.ContentBounds.Height / 2,
            image.LayoutBounds.Y + image.LayoutBounds.Height / 2,
            12);
        AssertCenteredWithinHalfPhysicalPixel(
            button.ContentBounds.Y + button.ContentBounds.Height / 2,
            text.LayoutBounds.Y + text.LayoutBounds.Height / 2,
            scale);
    }

    [Fact]
    public void ExplicitSmallSizeKeepsCenteredOverflowAndDoesNotEnableClipping()
    {
        var button = new Button
        {
            Icon = CreateImage(8, 8),
            IconSize = 20,
            Width = 10,
            Height = 8
        };

        button.Measure(new Size(100, 100));
        button.Arrange(new Rect(0, 0, 10, 8));

        var image = Assert.IsType<ImageView>(Assert.Single(button.Children));
        Assert.True(image.LayoutBounds.X < button.ContentBounds.X);
        Assert.True(image.LayoutBounds.Y < button.ContentBounds.Y);
        Assert.False(button.ClipToBounds);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1d)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidIconSizeFailsBeforeMeasuringChildren(double value)
    {
        var button = new Button
        {
            Icon = CreateImage(4, 4),
            IconSize = value
        };
        var image = Assert.IsType<ImageView>(Assert.Single(button.Children));

        Assert.Throws<InvalidOperationException>(() => button.Measure(Size.Infinite));

        Assert.False(button.IsMeasureValid);
        Assert.False(button.IsArrangeValid);
        Assert.False(image.IsMeasureValid);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1d)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidSpacingFailsBeforeMeasuringChildren(double value)
    {
        var button = new Button
        {
            Icon = CreateImage(4, 4),
            Spacing = value
        };
        var image = Assert.IsType<ImageView>(Assert.Single(button.Children));

        Assert.Throws<InvalidOperationException>(() => button.Measure(Size.Infinite));

        Assert.False(button.IsMeasureValid);
        Assert.False(button.IsArrangeValid);
        Assert.False(image.IsMeasureValid);
    }

    [Fact]
    public void NormalAndInteractiveStatesDrawInTheSpecifiedOrder()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 80, 32);
        var manager = new UiManager();
        var screen = new UiScreen(root) { UseLayoutRounding = false };
        manager.Open(screen);
        manager.Update(new Size(120, 80));

        var normal = GetFills(screen);
        Assert.Equal(5, normal.Count);
        Assert.Equal(new Color(48, 54, 62), normal[0].Color);
        Assert.All(normal, command => Assert.Null(command.Clip));

        manager.ProcessPointerMoved(new Point(20, 20));
        var hovered = GetFills(screen);
        Assert.Equal(new Color(255, 255, 255, 24), hovered[5].Color);
        Assert.Equal(new Rect(10, 10, 80, 32), hovered[5].Bounds);

        manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
        var pressed = GetFills(screen);
        Assert.Equal(new Color(0, 0, 0, 56), pressed[5].Color);
        Assert.Equal(new Color(84, 169, 255), pressed[6].Color);

        button.IsEnabled = false;
        var disabled = GetFills(screen);
        Assert.Equal(6, disabled.Count);
        Assert.Equal(new Color(0, 0, 0, 112), disabled[5].Color);
        manager.Close();
    }

    [Fact]
    public void FocusFrameUsesFourNonOverlappingInnerRectangles()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 80, 32);
        var manager = new UiManager();
        var screen = new UiScreen(root) { UseLayoutRounding = false };
        manager.Open(screen);
        manager.Update(new Size(120, 80));
        Assert.True(button.Focus());

        var fills = GetFills(screen);

        Assert.Equal(9, fills.Count);
        Assert.Equal(new Rect(10, 10, 80, 1), fills[5].Bounds);
        Assert.Equal(new Rect(89, 11, 1, 30), fills[6].Bounds);
        Assert.Equal(new Rect(10, 41, 80, 1), fills[7].Bounds);
        Assert.Equal(new Rect(10, 11, 1, 30), fills[8].Bounds);
        Assert.All(fills.Skip(5), command =>
            Assert.Equal(new Color(84, 169, 255), command.Color));
        manager.Close();
    }

    [Fact]
    public void LowScaleUsesSharedRoundingAndCanOmitTheFocusFrame()
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 10, 10, 40, 20);
        var manager = new UiManager();
        var screen = new UiScreen(root) { Scale = 0.25 };
        manager.Open(screen);
        manager.Update(new Size(30, 20));
        Assert.True(button.Focus());

        Assert.DoesNotContain(
            GetFills(screen),
            command => command.Color == new Color(84, 169, 255));
        manager.Close();
    }

    [Fact]
    public void ImageKeepsItsViewClipWhileTextInheritsTheOuterClip()
    {
        using var context = new UiTextTestContext();
        var root = new Canvas();
        var button = Place(
            root,
            new Button { Icon = CreateImage(8, 4), Text = "Go" },
            10,
            10,
            100,
            40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(140, 80));

        var commands = screen.CreateDrawCommandList();
        var imageCommand = Assert.IsType<UiDrawImageCommand>(commands[5]);
        var textCommand = Assert.IsType<UiDrawTextCommand>(commands[6]);
        var image = Assert.IsType<ImageView>(button.Children[0]);
        var imageOrigin = image.LocalToScreen(Point.Zero);

        Assert.Equal(
            new Rect(
                imageOrigin.X,
                imageOrigin.Y,
                image.LayoutBounds.Width,
                image.LayoutBounds.Height),
            imageCommand.Clip);
        Assert.Null(textCommand.Clip);
        manager.Close();
    }

    [Fact]
    public void InternalContentHitTargetsTheButtonAndClicksOnce()
    {
        using var context = new UiTextTestContext();
        var root = new Canvas();
        var button = Place(
            root,
            new Button { Icon = CreateImage(8, 8), Text = "Open" },
            10,
            10,
            100,
            40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        UiNode? source = null;
        var clicks = 0;
        button.PointerClicked += (_, eventArgs) => source = eventArgs.Source;
        button.Click += (_, _) => clicks++;
        manager.Open(screen);
        manager.Update(new Size(140, 80));

        Click(manager, new Point(60, 30), MouseButton.Left);

        Assert.Same(button, source);
        Assert.Equal(1, clicks);
        manager.Close();
    }

    [Fact]
    public void LeftPointerClickRunsRawEventThenClickAndStopsAtButton()
    {
        var root = new Canvas();
        _ = Place(root, new Button(), 0, 0, 80, 32);
        var button = Assert.IsType<Button>(root.Children[0]);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var calls = new List<string>();
        button.PointerClicked += (_, eventArgs) => calls.Add($"raw:{eventArgs.Handled}");
        button.Click += (_, _) => calls.Add("click");
        root.PointerClicked += (_, _) => calls.Add("root");
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["raw:False", "click"], calls);
        manager.Close();
    }

    [Fact]
    public void HandledRawClickDoesNotCancelButtonClick()
    {
        var (manager, screen, root, button) = OpenButtonScene();
        var ancestorClicks = 0;
        var clicks = 0;
        button.PointerClicked += (_, eventArgs) => eventArgs.Handled = true;
        button.Click += (_, _) => clicks++;
        root.PointerClicked += (_, _) => ancestorClicks++;

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(1, clicks);
        Assert.Equal(0, ancestorClicks);
        manager.Close();
    }

    [Fact]
    public void NonLeftButtonsContinueBubblingWithoutClick()
    {
        var (manager, screen, root, button) = OpenButtonScene();
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
    public void ReleaseOutsideOrAfterCrossScreenMoveDoesNotClick()
    {
        var firstRoot = new Canvas();
        var button = Place(firstRoot, new Button(), 0, 0, 40, 40);
        var firstManager = new UiManager();
        var firstScreen = new UiScreen(firstRoot);
        var secondRoot = new Canvas();
        var secondManager = new UiManager();
        var secondScreen = new UiScreen(secondRoot);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        firstManager.Open(firstScreen);
        secondManager.Open(secondScreen);
        firstManager.Update(new Size(100, 100));
        secondManager.Update(new Size(100, 100));

        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        firstManager.ProcessPointerReleased(new Point(80, 80), MouseButton.Left, KeyModifiers.None);
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        secondRoot.Children.Add(button);
        firstManager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(0, clicks);
        Assert.False(button.IsPressed);
        Assert.Same(secondScreen, button.Screen);
        firstManager.Close();
        secondManager.Close();
    }

    [Fact]
    public void RawPointerClickCanRemoveButtonWithoutCancellingAcceptedClick()
    {
        var (manager, screen, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.PointerClicked += (_, _) =>
        {
            calls.Add("raw");
            Assert.True(root.Children.Remove(button));
        };
        button.Click += (_, _) => calls.Add("click");

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["raw", "click"], calls);
        Assert.Null(button.Parent);
        manager.Close();
    }

    [Fact]
    public void RawPointerExceptionStopsClickAndKeepsTheExceptionInstance()
    {
        var (manager, screen, root, button) = OpenButtonScene();
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
        var (manager, screen, root, button) = OpenButtonScene();
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
        var (manager, screen, root, button) = OpenButtonScene();
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
        var (manager, screen, root, button) = OpenButtonScene();
        var calls = new List<string>();
        button.KeyDown += (_, eventArgs) => calls.Add($"down:{eventArgs.Handled}");
        button.KeyUp += (_, eventArgs) => calls.Add($"up:{eventArgs.Handled}");
        button.Click += (_, _) => calls.Add("click");
        Assert.True(button.Focus());

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        Assert.Equal(["down:False", "down:False"], calls);
        Assert.Contains(
            GetFills(screen),
            command => command.Color == new Color(0, 0, 0, 56));

        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(["down:False", "down:False", "up:False", "click"], calls);
        Assert.DoesNotContain(
            GetFills(screen),
            command => command.Color == new Color(0, 0, 0, 56));
        manager.Close();
    }

    [Fact]
    public void OtherKeysContinueBubblingAndUnmatchedActivationKeyUpDoesNotClick()
    {
        var (manager, screen, root, button) = OpenButtonScene();
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
        var (manager, screen, root, button) = OpenButtonScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        screen.ClearFocus();
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
        var (manager, screen, root, button) = OpenButtonScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        switch (cleanupKind)
        {
            case 0:
                screen.ClearFocus();
                break;
            case 1:
                button.IsEnabled = false;
                break;
            case 2:
                Assert.True(root.Children.Remove(button));
                break;
            case 3:
                screen.Root = new Canvas();
                break;
            case 4:
                manager.Close();
                break;
        }
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

        Assert.Equal(0, clicks);
        if (manager.CurrentScreen is not null)
            manager.Close();
    }

    [Fact]
    public void AcceptedKeyboardActivationSurvivesRawHandlerTreeChanges()
    {
        var (enterManager, enterScreen, enterRoot, enterButton) = OpenButtonScene();
        var enterClicks = 0;
        enterButton.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter)
                Assert.True(enterRoot.Children.Remove(enterButton));
        };
        enterButton.Click += (_, _) => enterClicks++;
        Assert.True(enterButton.Focus());

        enterManager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(1, enterClicks);
        enterManager.Close();

        var (spaceManager, spaceScreen, spaceRoot, spaceButton) = OpenButtonScene();
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

        Assert.Equal(1, spaceClicks);
        Assert.Null(spaceManager.CurrentScreen);
    }

    [Fact]
    public void RawKeyboardExceptionsKeepPhysicalPairTransitionsAndStopClick()
    {
        var (manager, screen, root, button) = OpenButtonScene();
        var enterError = new InvalidOperationException("enter");
        var spaceError = new InvalidOperationException("space");
        var clicks = 0;
        EventHandler<UiKeyEventArgs> enterHandler = (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter)
                throw enterError;
        };
        EventHandler<UiKeyEventArgs> spaceHandler = (_, eventArgs) =>
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

    [Fact]
    public void ClickCanCloseScreenAndLaterSubscribersStillRun()
    {
        var (manager, screen, root, button) = OpenButtonScene();
        var calls = new List<string>();
        InvalidOperationException? reopenError = null;
        button.Click += (_, _) =>
        {
            calls.Add("first");
            manager.Close();
            reopenError = Assert.Throws<InvalidOperationException>(() => manager.Open(screen));
        };
        button.Click += (_, _) => calls.Add("second");

        Click(manager, new Point(5, 5), MouseButton.Left);

        Assert.Equal(["first", "second"], calls);
        Assert.NotNull(reopenError);
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void ClickCannotStartNestedPhysicalInput()
    {
        var (manager, screen, root, button) = OpenButtonScene();
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
        var (manager, screen, root, button) = OpenButtonScene();
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
        Assert.Same(screen, button.Screen);
        manager.Close();
    }

    private static (UiManager Manager, UiScreen Screen, Canvas Root, Button Button) OpenButtonScene()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var button = Place(root, new Button(), 0, 0, 80, 32);
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        return (manager, screen, root, button);
    }

    private static T Place<T>(
        Canvas parent,
        T child,
        double x,
        double y,
        double width,
        double height)
        where T : UiNode
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
        return child;
    }

    private static void Click(UiManager manager, Point point, MouseButton button)
    {
        manager.ProcessPointerPressed(point, button, KeyModifiers.None);
        manager.ProcessPointerReleased(point, button, KeyModifiers.None);
    }

    private static IReadOnlyList<UiFillRectangleCommand> GetFills(UiScreen screen) =>
        screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().ToArray();

    private static UiImage CreateImage(int width, int height) =>
        UiImage.FromRgba(new byte[checked(width * height * 4)], width, height);

    private static void AssertCenteredWithinHalfPhysicalPixel(
        double expected,
        double actual,
        double scale) =>
        Assert.InRange(Math.Abs(expected - actual) * scale, 0, 0.5 + 1e-9);

    private static void AssertProperty<T>(
        UiProperty<T> property,
        T defaultValue,
        UiPropertyInvalidation invalidation)
    {
        Assert.Equal(typeof(Button), property.OwnerType);
        Assert.Equal(typeof(Button), property.TargetType);
        Assert.Equal(typeof(T), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.False(property.IsReadOnly);
        Assert.Equal(invalidation, property.Invalidation);
    }
}
