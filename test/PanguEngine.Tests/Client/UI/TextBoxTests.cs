using System.Reflection;
using System.ComponentModel;
using System.Text;
using PanguEngine.Client;
using PanguEngine.Client.UI;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TextBoxTests
{
    [Fact]
    public void PublicSurfaceAndDefaultsMatchTheTextBoxContract()
    {
        var textBox = new TextBox();

        Assert.True(typeof(TextBox).IsSealed);
        Assert.Equal(typeof(Control), typeof(TextBox).BaseType);
        AssertProperty(TextBox.TextProperty, string.Empty, UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(TextBox.PlaceholderProperty, string.Empty, UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(TextBox.FontProperty, new Font(string.Empty), UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(TextBox.FontSizeProperty, 16d, UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);
        AssertProperty(TextBox.ForegroundProperty, new Color(242, 244, 247), UiPropertyInvalidation.Render);
        AssertProperty(TextBox.PlaceholderForegroundProperty, new Color(139, 148, 160), UiPropertyInvalidation.Render);
        AssertProperty(TextBox.SelectionBackgroundProperty, new Color(47, 100, 160), UiPropertyInvalidation.Render);
        AssertProperty(TextBox.CaretColorProperty, new Color(242, 244, 247), UiPropertyInvalidation.Render);
        AssertProperty(TextBox.IsReadOnlyProperty, false, UiPropertyInvalidation.Render);
        Assert.True(textBox.Focusable);
        Assert.Equal(160d, textBox.MinWidth);
        Assert.Equal(new Thickness(8, 6), textBox.Padding);
        Assert.Equal(new SolidColorBrush(31, 35, 41), textBox.Background);
        Assert.Equal(new SolidColorBrush(92, 103, 116), textBox.BorderBrush);
        Assert.Equal(new Thickness(1), textBox.BorderThickness);
        Assert.False(textBox.ClipToBounds);
        Assert.Empty(textBox.Children);

        foreach (var name in new[] { "PasswordChar", "MaxLength", "Submitted", "Editor", "Controller" })
        {
            Assert.Null(typeof(TextBox).GetMember(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).SingleOrDefault());
        }
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
        using var context = new UiTextTestContext();
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
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("one two");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        screen.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

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
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("value");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        textBox.KeyDown += (_, args) => args.Handled = true;
        screen.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

        manager.ProcessKeyDown(Key.Home, KeyModifiers.None);

        Assert.Equal(0, textBox.CaretIndex);
        Assert.Empty(rootKeys);
    }

    [Fact]
    public void ExtendedKeyboardBindingsNavigateDeleteAndRedo()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("one two");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        screen.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

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
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("value");
        Assert.True(textBox.Focus());
        var rootKeys = new List<Key>();
        screen.Root!.KeyDown += (_, args) => rootKeys.Add(args.Key);

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
        using var context = new UiTextTestContext();
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
        using var context = new UiTextTestContext();
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
    public void ClipboardShortcutsAndReadOnlyCommandsFollowTheEditingContract()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("value");
        var clipboard = new TestClipboard();
        Assert.True(textBox.Focus());

        textBox.SelectAll();
        Assert.True(textBox.TryHandleKey(Key.Insert, KeyModifiers.Control, clipboard));
        Assert.Equal("value", clipboard.Text);
        Assert.True(textBox.TryHandleKey(Key.Delete, KeyModifiers.Shift, clipboard));
        Assert.Equal(string.Empty, textBox.Text);
        Assert.True(textBox.TryHandleKey(Key.Insert, KeyModifiers.Shift, clipboard));
        Assert.Equal("value", textBox.Text);
        textBox.SelectAll();
        Assert.True(textBox.TryHandleKey(Key.C, KeyModifiers.Control, clipboard));
        Assert.True(textBox.TryHandleKey(Key.X, KeyModifiers.Control, clipboard));
        Assert.Equal(string.Empty, textBox.Text);
        clipboard.Text = "replacement";
        Assert.True(textBox.TryHandleKey(Key.V, KeyModifiers.Control, clipboard));
        Assert.Equal("replacement", textBox.Text);

        var ancestorKeys = new List<Key>();
        screen.Root!.KeyDown += (_, args) => ancestorKeys.Add(args.Key);
        textBox.IsReadOnly = true;
        textBox.SelectAll();
        manager.ProcessKeyDown(Key.Backspace, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Delete, KeyModifiers.None);
        Assert.True(textBox.TryHandleKey(Key.X, KeyModifiers.Control, clipboard));
        Assert.True(textBox.TryHandleKey(Key.V, KeyModifiers.Control, clipboard));
        manager.ProcessKeyDown(Key.Z, KeyModifiers.Control);
        manager.ProcessKeyDown(Key.Y, KeyModifiers.Control);
        Assert.True(textBox.TryHandleKey(Key.Delete, KeyModifiers.Shift, clipboard));
        Assert.True(textBox.TryHandleKey(Key.Insert, KeyModifiers.Shift, clipboard));

        Assert.Equal("replacement", textBox.Text);
        Assert.Empty(ancestorKeys);
        textBox.ClearSelection();
        manager.Update(new Size(240, 80));
        Assert.DoesNotContain(
            screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>(),
            command => command.Color == textBox.CaretColor);
    }

    [Fact]
    public void ClipboardMethodsWithoutClientClipboardAndEqualTextAssignmentAreNoOps()
    {
        using var context = new UiTextTestContext();
        var (manager, _, textBox) = OpenTextBox("a");
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("b");
        Assert.True(textBox.CanUndo);
        textBox.Text = textBox.Text;
        textBox.SelectAll();

        textBox.Copy();
        textBox.Cut();
        textBox.Paste();

        Assert.Equal("ab", textBox.Text);
        Assert.Equal(2, textBox.SelectionLength);
        Assert.True(textBox.CanUndo);
    }

    [Fact]
    public void OneWayBindingRejectsEditBeforeStateCommit()
    {
        using var context = new UiTextTestContext();
        var source = new Text { Content = "bound" };
        var textBox = new TextBox();
        textBox.Bind(TextBox.TextProperty, source, Text.ContentProperty);
        var (manager, _, _) = OpenTextBox(textBox: textBox);
        Assert.True(textBox.Focus());

        Assert.Throws<InvalidOperationException>(() => manager.ProcessTextInput("x"));

        Assert.Equal("bound", textBox.Text);
        Assert.Equal(5, textBox.CaretIndex);
        Assert.False(textBox.CanUndo);
    }

    [Fact]
    public void PropertyNotificationFailureKeepsCommittedEditingState()
    {
        using var context = new UiTextTestContext();
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
        using var context = new UiTextTestContext();
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
        using var context = new UiTextTestContext();
        var (_, screen, textBox) = OpenTextBox("a\r\nb\tc");
        Assert.True(textBox.Focus());
        textBox.Select(0, 1);

        var commands = screen.CreateDrawCommandList();
        var text = Assert.Single(commands.OfType<UiDrawTextCommand>());
        var selection = Assert.Single(commands
            .OfType<UiFillRectangleCommand>()
            .Where(command => command.Color == textBox.SelectionBackground));

        Assert.Single(text.Layout.Lines);
        Assert.Equal(textBox.ContentBounds, selection.Clip);
        Assert.Equal(textBox.Foreground, text.Color);
        Assert.True(textBox.DesiredSize.Width >= 160);
    }

    [Fact]
    public void PointerClickAndDragRespectTextElementStopsAndFocus()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("A\U0001F600B");
        var text = Assert.Single(screen.CreateDrawCommandList().OfType<UiDrawTextCommand>());

        manager.ProcessPointerPressed(text.Origin, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(text.Origin.X + text.Layout.Width + 20, text.Origin.Y));
        manager.ProcessPointerReleased(new Point(text.Origin.X + text.Layout.Width + 20, text.Origin.Y), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(textBox.Text.Length, textBox.SelectionLength);

        textBox.Focusable = false;
        screen.ClearFocus();
        textBox.ClearSelection();
        manager.ProcessPointerPressed(text.Origin, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(text.Origin.X + text.Layout.Width, text.Origin.Y));
        manager.ProcessPointerReleased(new Point(text.Origin.X + text.Layout.Width, text.Origin.Y), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, textBox.SelectionLength);
    }

    [Fact]
    public void ShiftClickAcrossProjectedCrLfUsesSourceTextElementStops()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("a\r\nb");
        Assert.True(textBox.Focus());
        textBox.Select(1, 0);
        var text = Assert.Single(screen.CreateDrawCommandList().OfType<UiDrawTextCommand>());
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
        using var context = new UiTextTestContext();
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
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox("A long value that exceeds the viewport");
        textBox.Width = 80;
        manager.Update(new Size(240, 80));
        Assert.True(textBox.Focus());

        var commands = screen.CreateDrawCommandList();
        var text = Assert.Single(commands.OfType<UiDrawTextCommand>());
        var caret = Assert.Single(commands
            .OfType<UiFillRectangleCommand>()
            .Where(command => command.Color == textBox.CaretColor));

        Assert.True(text.Origin.X < textBox.ContentBounds.X);
        Assert.InRange(caret.Bounds.X, textBox.ContentBounds.X, textBox.ContentBounds.X + textBox.ContentBounds.Width);
        Assert.True(caret.Bounds.X + caret.Bounds.Width <= textBox.ContentBounds.X + textBox.ContentBounds.Width);
        Assert.Equal(textBox.ContentBounds, caret.Clip);

        var leftOfContent = new Point(
            textBox.ContentBounds.X - 1,
            textBox.ContentBounds.Y + textBox.ContentBounds.Height / 2);
        manager.ProcessPointerPressed(leftOfContent, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(leftOfContent, MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, textBox.CaretIndex);
        text = Assert.Single(screen.CreateDrawCommandList().OfType<UiDrawTextCommand>());
        Assert.Equal(textBox.ContentBounds.X, text.Origin.X, 12);
    }

    [Fact]
    public void PlaceholderAndStateLayersUseStableDrawingOrder()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox();
        textBox.Placeholder = "hint";
        manager.Update(new Size(240, 80));
        Assert.True(textBox.Focus());

        var commands = screen.CreateDrawCommandList().ToArray();
        var placeholderIndex = Array.FindIndex(commands, command =>
            command is UiDrawTextCommand text && text.Color == textBox.PlaceholderForeground);
        var caretIndex = Array.FindIndex(commands, command =>
            command is UiFillRectangleCommand fill && fill.Color == textBox.CaretColor);
        var focusIndex = Array.FindIndex(commands, command =>
            command is UiFillRectangleCommand fill && fill.Color == new Color(84, 169, 255));

        Assert.True(placeholderIndex >= 0);
        Assert.True(caretIndex > placeholderIndex);
        Assert.True(focusIndex > caretIndex);

        textBox.IsEnabled = false;
        commands = screen.CreateDrawCommandList().ToArray();
        var overlayIndex = Array.FindLastIndex(commands, command =>
            command is UiFillRectangleCommand fill && fill.Color == new Color(0, 0, 0, 112));
        Assert.True(overlayIndex > placeholderIndex);
        Assert.DoesNotContain(commands, command =>
            command is UiFillRectangleCommand fill && fill.Color == new Color(84, 169, 255));
    }

    [Fact]
    public void LostFocusEndsTypingGroupAndHidesTheSelection()
    {
        using var context = new UiTextTestContext();
        var (manager, screen, textBox) = OpenTextBox();
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("a");
        screen.ClearFocus();
        manager.Update(new Size(240, 80));
        Assert.True(textBox.Focus());
        manager.ProcessTextInput("b");

        textBox.Undo();

        Assert.Equal("a", textBox.Text);
        manager.Update(new Size(240, 80));
        textBox.SelectAll();
        Assert.Contains(
            screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>(),
            command => command.Color == textBox.SelectionBackground);
        screen.ClearFocus();
        Assert.DoesNotContain(
            screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>(),
            command => command.Color == textBox.SelectionBackground);
    }

    private static (UiManager Manager, UiScreen Screen, TextBox TextBox) OpenTextBox(
        string text = "",
        TextBox? textBox = null)
    {
        var manager = new UiManager();
        var root = new Canvas();
        textBox ??= new TextBox { Text = text };
        textBox.Width = 200;
        textBox.Height = 40;
        root.Children.Add(textBox);
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(240, 80));
        return (manager, screen, textBox);
    }

    private static void AssertProperty<T>(
        UiProperty<T> property,
        T defaultValue,
        UiPropertyInvalidation invalidation)
    {
        Assert.Equal(typeof(TextBox), property.OwnerType);
        Assert.Equal(typeof(TextBox), property.TargetType);
        Assert.Equal(typeof(T), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.False(property.IsReadOnly);
        Assert.Equal(invalidation, property.Invalidation);
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

        protected override void DestroyCore()
        {
        }
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
