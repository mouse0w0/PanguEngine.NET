using System.ComponentModel;
using System.Reflection;
using System.Text;
using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Desktop;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;
using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TextBoxTests
{
    [Fact]
    public void InheritedLineHeightResizesTextAndSelectionTogether()
    {
        using var context = new TextTestContext();
        var root = new Panel();
        var textBox = new TextBox { Text = "Hello" };
        root.Children.Add(textBox);
        var scene = new GameUIScene(root) { UseLayoutRounding = false };
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(400, 200), 0);
        Assert.True(textBox.Focus());
        textBox.Select(0, 1);
        var initial = Assert.Single(scene.CreateDrawCommandList().OfType<DrawTextCommand>());

        root.LineHeight = 1.5;

        Assert.False(textBox.IsMeasureValid);
        manager.UpdateFrame(new Size(400, 200), 0);
        var commands = scene.CreateDrawCommandList();
        var text = Assert.Single(commands.OfType<DrawTextCommand>());
        var selection = Assert.Single(commands.OfType<FillRectangleCommand>()
            .Where(command => command.Color == textBox.SelectionBackgroundColor));
        Assert.NotSame(initial.Layout, text.Layout);
        Assert.Equal(initial.Layout.Height * 1.5, text.Layout.Height, 6);
        Assert.Equal(text.Layout.Height, selection.Bounds.Height);
        manager.Destroy();
    }

    [Fact]
    public void PublicSurfaceAndDefaultsMatchTheTextBoxContract()
    {
        var textBox = new TextBox();
        textBox.GetStyleRoot().UpdateStyles();

        Assert.True(typeof(TextBox).IsSealed);
        Assert.Equal(typeof(Control), typeof(TextBox).BaseType);
        AssertProperty(TextBox.TextProperty, string.Empty);
        AssertProperty(TextBox.PlaceholderProperty, string.Empty);
        Assert.Equal(typeof(UIElement), TextBox.FontFamilyProperty.OwnerType);
        Assert.Equal(typeof(UIElement), TextBox.ColorProperty.OwnerType);
        Assert.Equal(string.Empty, UIElement.FontFamilyProperty.DefaultValue);
        Assert.Equal(new Color(255, 255, 255), UIElement.ColorProperty.DefaultValue);
        Assert.Equal(new Color(242, 244, 247), textBox.Color);
        AssertProperty(TextBox.PlaceholderColorProperty, new Color(139, 148, 160));
        AssertProperty(TextBox.SelectionBackgroundColorProperty, new Color(47, 100, 160));
        AssertProperty(TextBox.CaretColorProperty, new Color(242, 244, 247));
        AssertProperty(TextBox.IsReadOnlyProperty, false);
        Assert.True(textBox.Focusable);
        Assert.Equal(160d, textBox.MinWidth);
        Assert.Equal(new Thickness(8, 6), textBox.Padding);
        Assert.Equal(new SolidColorBrush(31, 35, 41), textBox.Background);
        Assert.Equal(new SolidColorBrush(92, 103, 116), textBox.BorderBrush);
        Assert.Equal(new Thickness(1), textBox.BorderThickness);
        Assert.False(textBox.ClipToBounds);
        Assert.Empty(textBox.ReadOnlyChildren);

        foreach (var name in new[] { "PasswordChar", "MaxLength", "Submitted", "Editor", "Controller" })
        {
            Assert.Null(typeof(TextBox).GetMember(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).SingleOrDefault());
        }
    }

    [Fact]
    public void BaseStylesSupplyDefaultsAndClearingThemRestoresDescriptorValues()
    {
        var node = new TextBox();
        var scene = new UIScene(node);
        scene.Root!.UpdateStyles();
        var properties = new Property[]
        {
            UIElement.MinWidthProperty, Region.PaddingProperty, Region.BackgroundProperty,
            Region.BorderBrushProperty, Region.BorderThicknessProperty,
            UIElement.ColorProperty, TextBox.PlaceholderColorProperty,
            TextBox.SelectionBackgroundColorProperty, TextBox.CaretColorProperty
        };
        foreach (var property in properties)
        {
            Assert.All(node.GetStyleValueSources(property), source =>
            {
                Assert.Equal(StyleOrigin.Base, source.Origin);
                Assert.Equal("pangu/ui/default.css", source.SheetSourceName);
                Assert.False(source.IsMaskedByLocalValue);
            });
            Assert.NotEmpty(node.GetStyleValueSources(property));
        }

        Assert.Empty(node.GetStyleValueSources(UIElement.FontSizeProperty));
        Assert.Equal(16d, node.FontSize);

        scene.SetBaseStyleSheets([]);
        scene.Root!.UpdateStyles();

        Assert.Equal(UIElement.MinWidthProperty.DefaultValue, node.MinWidth);
        Assert.Equal(Region.PaddingProperty.DefaultValue, node.Padding);
        Assert.Equal(Region.BackgroundProperty.DefaultValue, node.Background);
        Assert.Equal(Region.BorderBrushProperty.DefaultValue, node.BorderBrush);
        Assert.Equal(Region.BorderThicknessProperty.DefaultValue, node.BorderThickness);
        Assert.True(node.Focusable);
        Assert.All(properties, property => Assert.Empty(node.GetStyleValueSources(property)));
    }

    [Fact]
    public void StateStylesPreferFocusOverHoverAndDisabledOverBoth()
    {
        var node = new TextBox();
        node.SetHovered(true);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(139, 148, 160), node.BorderBrush);
        node.SetFocused(true);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(84, 169, 255), node.BorderBrush);
        node.IsEnabled = false;
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(52, 58, 65), node.BorderBrush);
        Assert.Equal(new Color(139, 148, 160), node.Color);
        Assert.Equal(new Thickness(1), node.BorderThickness);
        Assert.Equal(new Thickness(8, 6), node.Padding);
        node.IsEnabled = true;
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(84, 169, 255), node.BorderBrush);
        node.SetFocused(false);
        node.SetHovered(false);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(92, 103, 116), node.BorderBrush);
    }

    [Fact]
    public void AuthorStylesOverrideDefaultsAndLocalValuesRestoreAfterClear()
    {
        var node = new TextBox();
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             TextBox {
                                 min-width: 90px;
                                 padding: 2px;
                                 background-color: #010203;
                                 border-color: #040506;
                                 border-thickness: 2px;
                                 font-size: 20px;
                                 color: #070809;
                                 placeholder-color: #101112;
                                 selection-background-color: #131415;
                                 caret-color: #161718;
                             }
                             """)
        ]);
        node.SetHovered(true);
        node.SetFocused(true);
        node.IsEnabled = false;
        scene.Root!.UpdateStyles();

        Assert.Equal(90d, node.MinWidth);
        Assert.Equal(new Thickness(2), node.Padding);
        Assert.Equal(new SolidColorBrush(1, 2, 3), node.Background);
        Assert.Equal(new SolidColorBrush(4, 5, 6), node.BorderBrush);
        Assert.Equal(new Thickness(2), node.BorderThickness);
        Assert.Equal(20d, node.FontSize);
        Assert.Equal(new Color(7, 8, 9), node.Color);
        Assert.Equal(new Color(16, 17, 18), node.PlaceholderColor);
        Assert.Equal(new Color(19, 20, 21), node.SelectionBackgroundColor);
        Assert.Equal(new Color(22, 23, 24), node.CaretColor);

        node.Background = new SolidColorBrush(30, 31, 32);
        node.SetFocused(false);
        Assert.Equal(new SolidColorBrush(30, 31, 32), node.Background);
        node.ClearValue(Region.BackgroundProperty);
        Assert.Equal(new SolidColorBrush(1, 2, 3), node.Background);
    }

    [Fact]
    public void FocusDoesNotDrawASeparateFrameWhenBorderIsLocallyDisabled()
    {
        using var context = new TextTestContext();
        var (manager, scene, node) = OpenTextBox();
        node.BorderThickness = Thickness.Zero;
        manager.UpdateFrame(new Size(240, 80), 0);
        Assert.True(node.Focus());

        Assert.DoesNotContain(scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => command.Color == new Color(84, 169, 255));
    }

    [Fact]
    public void DesiredWidthDoesNotDependOnTextOrPlaceholderLength()
    {
        using var context = new TextTestContext();
        var textBox = new TextBox();
        textBox.GetStyleRoot().UpdateStyles();

        textBox.Measure(Size.Infinite);
        var expectedWidth = textBox.DesiredSize.Width;
        textBox.Text = "A long value that exceeds the default text box width";
        textBox.Measure(Size.Infinite);

        Assert.Equal(160d, expectedWidth);
        Assert.Equal(expectedWidth, textBox.DesiredSize.Width);

        textBox.Text = string.Empty;
        textBox.Placeholder = "A long placeholder that exceeds the default text box width";
        textBox.Measure(Size.Infinite);

        Assert.Equal(expectedWidth, textBox.DesiredSize.Width);
    }

    [Fact]
    public void SelectionApiUsesTextElementBoundaries()
    {
        var textBox = new TextBox { Text = "A\U0001F600e\u0301Z" };

        textBox.Select(1, 4);

        Assert.Equal(5, textBox.CaretIndex);
        Assert.Equal(1, textBox.SelectionStart);
        Assert.Equal(4, textBox.SelectionLength);
        Assert.Equal("\U0001F600e\u0301", textBox.SelectedText);
        Assert.Throws<ArgumentOutOfRangeException>(() => textBox.Select(2, 0));
        textBox.ClearSelection();
        Assert.Equal(5, textBox.SelectionStart);
        Assert.Equal(0, textBox.SelectionLength);
        textBox.SelectAll();
        Assert.Equal(textBox.Text.Length, textBox.SelectionLength);
    }

    [Fact]
    public void TextInputFiltersToOneLineAndReadOnlyStillHandlesInput()
    {
        using var context = new TextTestContext();
        var (manager, _, textBox) = OpenTextBox();
        Assert.True(textBox.Focus());
        var handled = new List<bool>();
        textBox.TextInput += (_, args) => handled.Add(args.Handled);

        manager.ProcessTextInput("ab\tcd\r\nlater");
        textBox.IsReadOnly = true;
        manager.ProcessTextInput("ignored");

        Assert.Equal("ab cd", textBox.Text);
        Assert.Equal([false, false], handled);
        Assert.Equal(5, textBox.CaretIndex);
    }

    [Fact]
    public void KeyboardCommandsEditNavigateAndLeaveEnterUnhandled()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("one two");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        scene.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

        manager.ProcessKeyDown(Key.Home, KeyModifiers.Shift);
        Assert.Equal(0, textBox.CaretIndex);
        Assert.Equal(7, textBox.SelectionLength);
        manager.ProcessKeyDown(Key.End, KeyModifiers.None);
        Assert.Equal(7, textBox.CaretIndex);
        Assert.Equal(0, textBox.SelectionLength);
        manager.ProcessKeyDown(Key.A, KeyModifiers.Control);
        Assert.Equal(7, textBox.SelectionLength);
        manager.ProcessKeyDown(Key.Left, KeyModifiers.None);
        Assert.Equal(0, textBox.CaretIndex);
        manager.ProcessKeyDown(Key.Right, KeyModifiers.Control);
        Assert.Equal(4, textBox.CaretIndex);
        manager.ProcessKeyDown(Key.End, KeyModifiers.Shift);
        Assert.Equal("two", textBox.SelectedText);
        manager.ProcessKeyDown(Key.Backspace, KeyModifiers.None, isRepeat: true);
        Assert.Equal("one ", textBox.Text);
        manager.ProcessKeyDown(Key.Z, KeyModifiers.Control);
        Assert.Equal("one two", textBox.Text);
        manager.ProcessKeyDown(Key.Y, KeyModifiers.Control);
        Assert.Equal("one ", textBox.Text);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Tab, KeyModifiers.None);

        Assert.Equal([Key.Enter, Key.Tab], rootKeys);
    }

    [Fact]
    public void TextBoxBuiltInBindingRunsAfterItsKeyDownHandlerMarksHandled()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("value");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        textBox.KeyDown += (_, args) => args.Handled = true;
        scene.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);

        Assert.Equal(0, textBox.CaretIndex);
        Assert.Empty(rootKeys);
    }

    [Fact]
    public void ExtendedKeyboardBindingsNavigateDeleteAndRedo()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("one two");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        scene.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

        manager.ProcessKeyDown(Key.Backspace, KeyModifiers.Shift);
        Assert.Equal("one two", textBox.Text);
        Assert.Equal([Key.Backspace], rootKeys);

        manager.ProcessKeyDown(Key.Left, KeyModifiers.Shift);
        Assert.Equal("o", textBox.SelectedText);
        manager.ProcessKeyDown(Key.Right, KeyModifiers.Shift);
        Assert.Equal(0, textBox.SelectionLength);
        manager.ProcessKeyDown(Key.Left, KeyModifiers.Control);
        Assert.Equal(4, textBox.CaretIndex);
        manager.ProcessKeyDown(Key.Left, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal("one ", textBox.SelectedText);
        manager.ProcessKeyDown(Key.Right, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal(0, textBox.SelectionLength);
        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Equal(5, textBox.CaretIndex);

        textBox.Select(0, 0);
        manager.ProcessKeyDown(Key.Delete, KeyModifiers.None);
        Assert.Equal("ne two", textBox.Text);
        textBox.Undo();
        Assert.Equal("one two", textBox.Text);

        textBox.Select(textBox.Text.Length, 0);
        manager.ProcessKeyDown(Key.Backspace, KeyModifiers.Control);
        Assert.Equal("one ", textBox.Text);
        textBox.Undo();
        textBox.Select(0, 0);
        manager.ProcessKeyDown(Key.Delete, KeyModifiers.Control);
        Assert.Equal("two", textBox.Text);
        manager.ProcessKeyDown(Key.Z, KeyModifiers.Control);
        Assert.Equal("one two", textBox.Text);
        manager.ProcessKeyDown(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal("two", textBox.Text);
    }

    [Fact]
    public void AltAndSuperDoNotInvokeEditingShortcuts()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("value");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        scene.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

        manager.ProcessKeyDown(Key.A, KeyModifiers.Control | KeyModifiers.Alt);
        manager.ProcessKeyDown(Key.A, KeyModifiers.Control | KeyModifiers.Super);
        manager.ProcessKeyDown(Key.Backspace, KeyModifiers.Alt);

        Assert.Equal("value", textBox.Text);
        Assert.Equal(0, textBox.SelectionLength);
        Assert.Equal([Key.A, Key.A, Key.Backspace], rootKeys);
    }

    [Fact]
    public void ClipboardCommandsPreserveOriginalProgrammaticLineBreaks()
    {
        using var context = new TextTestContext();
        var (manager, _, textBox) = OpenTextBox("a\r\nb\tc");
        var clipboard = new TestClipboard();
        textBox.Select(1, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => textBox.Select(2, 0));

        textBox.Copy(clipboard);
        Assert.Equal("\r\nb", clipboard.Text);
        textBox.Cut(clipboard);
        Assert.Equal("a\tc", textBox.Text);
        textBox.Undo();
        Assert.Equal("a\r\nb\tc", textBox.Text);
        clipboard.Text = "X\tY\nignored";
        textBox.SelectAll();
        textBox.Paste(clipboard);

        Assert.Equal("X Y", textBox.Text);
        manager.Close();
    }

    [Fact]
    public void ClipboardFailureDoesNotCommitCut()
    {
        using var context = new TextTestContext();
        var (_, _, textBox) = OpenTextBox("value");
        var expected = new InvalidOperationException("clipboard");
        var clipboard = new TestClipboard { SetException = expected };
        textBox.SelectAll();

        var actual = Assert.Throws<InvalidOperationException>(() => textBox.Cut(clipboard));

        Assert.Same(expected, actual);
        Assert.Equal("value", textBox.Text);
        Assert.Equal(5, textBox.SelectionLength);
        Assert.False(textBox.CanUndo);

        expected = new InvalidOperationException("clipboard read");
        clipboard = new TestClipboard { GetException = expected };
        actual = Assert.Throws<InvalidOperationException>(() => textBox.Paste(clipboard));

        Assert.Same(expected, actual);
        Assert.Equal("value", textBox.Text);
        Assert.Equal(5, textBox.SelectionLength);
        Assert.False(textBox.CanUndo);
    }

    [Fact]
    public void ClipboardCommandsRespectReadOnlyState()
    {
        using var context = new TextTestContext();
        var (_, _, textBox) = OpenTextBox("value");
        var clipboard = new TestClipboard();
        textBox.SelectAll();
        textBox.Copy(clipboard);
        Assert.Equal("value", clipboard.Text);
        textBox.IsReadOnly = true;
        textBox.Cut(clipboard);
        clipboard.Text = "replacement";
        textBox.Paste(clipboard);

        Assert.Equal("value", textBox.Text);
    }

    [Fact]
    public void EqualTextAssignmentPreservesUndoHistory()
    {
        using var context = new TextTestContext();
        var (manager, _, textBox) = OpenTextBox("a");
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("b");
        Assert.True(textBox.CanUndo);

        textBox.Text = textBox.Text;

        Assert.Equal("ab", textBox.Text);
        Assert.True(textBox.CanUndo);
    }

    [Fact]
    public void OneWayBindingAllowsEditingWithoutChangingSource()
    {
        using var context = new TextTestContext();
        var source = new Text { Content = "bound" };
        var textBox = new TextBox();
        textBox.Bind(TextBox.TextProperty, source, Text.ContentProperty);
        var (manager, _, _) = OpenTextBox(textBox: textBox);
        Assert.True(textBox.Focus());

        manager.ProcessTextInput("x");

        Assert.Equal("boundx", textBox.Text);
        Assert.Equal("bound", source.Content);
        Assert.Equal(6, textBox.CaretIndex);
        Assert.True(textBox.CanUndo);
        Assert.True(textBox.IsBound(TextBox.TextProperty));
        source.Content = "updated";
        Assert.Equal("updated", textBox.Text);
    }

    [Fact]
    public void PropertyNotificationFailureKeepsCommittedEditingState()
    {
        using var context = new TextTestContext();
        var (manager, _, textBox) = OpenTextBox("a");
        Assert.True(textBox.Focus());
        var expected = new InvalidOperationException("notification");
        textBox.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, TextBox.TextProperty))
                throw expected;
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.ProcessTextInput("b"));

        Assert.Same(expected, actual);
        Assert.Equal("ab", textBox.Text);
        Assert.Equal(2, textBox.CaretIndex);
        Assert.True(textBox.CanUndo);
    }

    [Fact]
    public void ExternalTextIsSynchronizedBeforePropertyNotification()
    {
        var textBox = new TextBox { Text = "before" };
        textBox.Select(0, 2);
        var observed = (Caret: -1, Length: -1, CanUndo: true);
        textBox.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, TextBox.TextProperty))
                observed = (textBox.CaretIndex, textBox.SelectionLength, textBox.CanUndo);
        };

        textBox.Text = "after";

        Assert.Equal((5, 0, false), observed);
    }

    [Fact]
    public void TwoWaySourceFailureKeepsCommittedEditingState()
    {
        using var context = new TextTestContext();
        var expected = new InvalidOperationException("source");
        var source = new ThrowingTextSource("a", expected);
        var textBox = new TextBox();
        textBox.BindTwoWay(TextBox.TextProperty, source, value => value.Value);
        var (manager, _, _) = OpenTextBox(textBox: textBox);
        Assert.True(textBox.Focus());
        source.ThrowOnSet = true;

        var actual = Assert.Throws<InvalidOperationException>(() => manager.ProcessTextInput("b"));

        Assert.Same(expected, actual);
        Assert.Equal("ab", textBox.Text);
        Assert.Equal(2, textBox.CaretIndex);
        Assert.True(textBox.CanUndo);
    }

    [Fact]
    public void MeasureAndDrawKeepProgrammaticTextOnOneLine()
    {
        using var context = new TextTestContext();
        var (_, scene, textBox) = OpenTextBox("a\r\nb\tc");
        Assert.True(textBox.Focus());
        textBox.Select(0, 1);

        var commands = scene.CreateDrawCommandList();
        var text = Assert.Single(commands.OfType<DrawTextCommand>());
        var selection = Assert.Single(commands
            .OfType<FillRectangleCommand>()
            .Where(command => command.Color == textBox.SelectionBackgroundColor));

        var clip = Assert.Single(commands.OfType<PushClipCommand>()).Clip;
        Assert.Single(text.Layout.Lines);
        Assert.Equal(textBox.ContentBounds, clip);
        Assert.InRange(selection.Bounds.X, clip.X, clip.X + clip.Width);
        Assert.InRange(selection.Bounds.Y, clip.Y, clip.Y + clip.Height);
        Assert.Equal(textBox.Color, text.Color);
        Assert.True(textBox.DesiredSize.Width >= 160);
    }

    [Fact]
    public void PointerClickAndDragRespectTextElementStopsAndFocus()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("A\U0001F600B");
        var text = Assert.Single(scene.CreateDrawCommandList().OfType<DrawTextCommand>());

        manager.ProcessPointerPressed(text.Origin, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(text.Origin.X + text.Layout.Width + 20, text.Origin.Y));
        manager.ProcessPointerReleased(new Point(text.Origin.X + text.Layout.Width + 20, text.Origin.Y),
            MouseButton.Left, KeyModifiers.None);

        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(textBox.Text.Length, textBox.SelectionLength);

        textBox.Focusable = false;
        scene.ClearFocus();
        textBox.ClearSelection();
        manager.ProcessPointerPressed(text.Origin, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(text.Origin.X + text.Layout.Width, text.Origin.Y));
        manager.ProcessPointerReleased(new Point(text.Origin.X + text.Layout.Width, text.Origin.Y), MouseButton.Left,
            KeyModifiers.None);
        Assert.Equal(0, textBox.SelectionLength);
    }

    [Fact]
    public void ShiftClickAcrossProjectedCrLfUsesSourceTextElementStops()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("a\r\nb");
        Assert.True(textBox.Focus());
        textBox.Select(1, 0);
        var text = Assert.Single(scene.CreateDrawCommandList().OfType<DrawTextCommand>());
        var secondProjectedSpace = text.Layout.Lines
            .SelectMany(line => line.GlyphRuns)
            .SelectMany(run => run.Glyphs)
            .Single(glyph => glyph.Cluster == 2);
        var pointer = new Point(
            text.Origin.X + secondProjectedSpace.X + secondProjectedSpace.XAdvance / 2,
            text.Origin.Y + text.Layout.Height / 2);

        manager.ProcessPointerPressed(pointer, MouseButton.Left, KeyModifiers.Shift);
        manager.ProcessPointerReleased(pointer, MouseButton.Left, KeyModifiers.Shift);

        Assert.Equal(1, textBox.SelectionStart);
        Assert.Equal(3, textBox.CaretIndex);
        Assert.Equal("\r\n", textBox.SelectedText);
    }

    [Fact]
    public void PointerMoveDuringInvalidLayoutDoesNotUseStaleCaretStops()
    {
        using var context = new TextTestContext();
        var (manager, _, textBox) = OpenTextBox("ab");
        var pointer = new Point(
            textBox.ContentBounds.X,
            textBox.ContentBounds.Y + textBox.ContentBounds.Height / 2);
        manager.ProcessPointerPressed(pointer, MouseButton.Left, KeyModifiers.None);
        textBox.Select(1, 0);

        manager.ProcessTextInput("\U0001F600");
        manager.ProcessPointerMoved(new Point(pointer.X + 20, pointer.Y));

        Assert.False(textBox.IsArrangeValid);
        Assert.Equal("a\U0001F600b", textBox.Text);
        Assert.Equal(3, textBox.CaretIndex);
    }

    [Fact]
    public void LongTextScrollsCaretIntoTheContentClip()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox("A long value that exceeds the viewport");
        textBox.Width = 80;
        manager.UpdateFrame(new Size(240, 80), 0);
        Assert.True(textBox.Focus());

        var commands = scene.CreateDrawCommandList();
        var text = Assert.Single(commands.OfType<DrawTextCommand>());
        var caret = Assert.Single(commands
            .OfType<FillRectangleCommand>()
            .Where(command => command.Color == textBox.CaretColor));

        Assert.True(text.Origin.X < textBox.ContentBounds.X);
        Assert.InRange(caret.Bounds.X, textBox.ContentBounds.X, textBox.ContentBounds.X + textBox.ContentBounds.Width);
        Assert.True(caret.Bounds.X + caret.Bounds.Width <= textBox.ContentBounds.X + textBox.ContentBounds.Width);
        Assert.Equal(textBox.ContentBounds, Assert.Single(commands.OfType<PushClipCommand>()).Clip);

        var leftOfContent = new Point(
            textBox.ContentBounds.X - 1,
            textBox.ContentBounds.Y + textBox.ContentBounds.Height / 2);
        manager.ProcessPointerPressed(leftOfContent, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(leftOfContent, MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, textBox.CaretIndex);
        text = Assert.Single(scene.CreateDrawCommandList().OfType<DrawTextCommand>());
        Assert.Equal(textBox.ContentBounds.X, text.Origin.X, 12);
    }

    [Fact]
    public void PlaceholderAndStateStylesUseStableDrawingOrder()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox();
        textBox.Placeholder = "hint";
        manager.UpdateFrame(new Size(240, 80), 0);
        Assert.True(textBox.Focus());
        scene.Root!.UpdateStyles();

        var commands = scene.CreateDrawCommandList().ToArray();
        var placeholderIndex = Array.FindIndex(commands, command =>
            command is DrawTextCommand text && text.Color == textBox.PlaceholderColor);
        var caretIndex = Array.FindIndex(commands, command =>
            command is FillRectangleCommand fill && fill.Color == textBox.CaretColor);
        var focusIndex = Array.FindIndex(commands, command =>
            command is FillRectangleCommand fill && fill.Color == new Color(84, 169, 255));

        Assert.True(placeholderIndex >= 0);
        Assert.True(caretIndex > placeholderIndex);
        Assert.True(focusIndex >= 0);
        Assert.True(focusIndex < placeholderIndex);

        textBox.IsEnabled = false;
        scene.Root!.UpdateStyles();
        commands = scene.CreateDrawCommandList().ToArray();
        var overlayIndex = Array.FindLastIndex(commands, command =>
            command is FillRectangleCommand fill && fill.Color == new Color(0, 0, 0, 112));
        Assert.Equal(-1, overlayIndex);
        Assert.Equal(new SolidColorBrush(27, 30, 35), textBox.Background);
        Assert.Equal(new SolidColorBrush(52, 58, 65), textBox.BorderBrush);
        Assert.Equal(new Color(92, 103, 116), textBox.PlaceholderColor);
        Assert.Equal(textBox.PlaceholderColor, Assert.Single(commands.OfType<DrawTextCommand>()).Color);
        Assert.DoesNotContain(commands, command =>
            command is FillRectangleCommand fill && fill.Color == new Color(84, 169, 255));
    }

    [Fact]
    public void LostFocusEndsTypingGroupAndHidesTheSelection()
    {
        using var context = new TextTestContext();
        var (manager, scene, textBox) = OpenTextBox();
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("a");
        scene.ClearFocus();
        manager.UpdateFrame(new Size(240, 80), 0);
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("b");

        textBox.Undo();

        Assert.Equal("a", textBox.Text);
        manager.UpdateFrame(new Size(240, 80), 0);
        textBox.SelectAll();
        Assert.Contains(
            scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => command.Color == textBox.SelectionBackgroundColor);
        scene.ClearFocus();
        Assert.DoesNotContain(
            scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => command.Color == textBox.SelectionBackgroundColor);
    }

    private static (UIManager Manager, GameUIScene Scene, TextBox TextBox) OpenTextBox(
        string text = "",
        TextBox? textBox = null)
    {
        var manager = new UIManager();
        var root = new Canvas();
        textBox ??= new TextBox { Text = text };
        textBox.Width = 200;
        textBox.Height = 40;
        root.Children.Add(textBox);
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(240, 80), 0);
        return (manager, scene, textBox);
    }

    private static void AssertProperty<T>(
        Property<T> property,
        T defaultValue)
    {
        Assert.Equal(typeof(TextBox), property.OwnerType);
        Assert.Equal(typeof(TextBox), property.TargetType);
        Assert.Equal(typeof(T), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.False(property.IsReadOnly);
    }

    private sealed class TestClipboard : Clipboard
    {
        private string _text = string.Empty;

        internal Exception? GetException { get; init; }
        internal Exception? SetException { get; init; }

        internal string Text
        {
            get => _text;
            set => _text = value;
        }

        protected override IReadOnlyList<ClipboardFormat> GetFormatsCore() =>
            _text.Length == 0 ? [] : [ClipboardFormat.Text];

        protected override bool TryGetTextCore(out string text)
        {
            if (GetException is not null)
                throw GetException;
            text = _text;
            return text.Length != 0;
        }

        protected override bool TryGetDataCore(ClipboardFormat format, out byte[] data)
        {
            if (format == ClipboardFormat.Text && TryGetTextCore(out var text))
            {
                data = Encoding.UTF8.GetBytes(text);
                return true;
            }

            data = [];
            return false;
        }

        protected override void SetTextCore(string text)
        {
            if (SetException is not null)
                throw SetException;
            _text = text;
        }

        protected override void SetContentCore(ClipboardContent content)
        {
            _text = string.Empty;
            foreach (var entry in content.CopyEntries())
            {
                if (entry.Format != ClipboardFormat.Text)
                    continue;
                _text = Encoding.UTF8.GetString(entry.Data);
                return;
            }
        }

        protected override void ClearCore() => _text = string.Empty;
    }

    private sealed class ThrowingTextSource(string value, Exception setException) : INotifyPropertyChanged
    {
        private string _value = value;

        internal bool ThrowOnSet { get; set; }

        public string Value
        {
            get => _value;
            set
            {
                if (ThrowOnSet)
                    throw setException;
                if (_value == value)
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}