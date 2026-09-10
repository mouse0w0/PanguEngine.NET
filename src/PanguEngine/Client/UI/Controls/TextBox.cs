using System.Diagnostics;
using System.Globalization;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Input;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides an editable single-line plain text control.
/// </summary>
public sealed class TextBox : Control
{
    /// <summary>
    /// Identifies the <see cref="Text"/> property.
    /// </summary>
    public static readonly UiProperty<string> TextProperty =
        UiProperty.Register<TextBox, string>(
            nameof(Text),
            string.Empty,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Placeholder"/> property.
    /// </summary>
    public static readonly UiProperty<string> PlaceholderProperty =
        UiProperty.Register<TextBox, string>(
            nameof(Placeholder),
            string.Empty,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Font"/> property.
    /// </summary>
    public static readonly UiProperty<Font> FontProperty =
        UiProperty.Register<TextBox, Font>(
            nameof(Font),
            new Font(string.Empty),
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly UiProperty<double> FontSizeProperty =
        UiProperty.Register<TextBox, double>(
            nameof(FontSize),
            16d,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly UiProperty<Color> ForegroundProperty =
        UiProperty.Register<TextBox, Color>(
            nameof(Foreground),
            new Color(242, 244, 247),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="PlaceholderForeground"/> property.
    /// </summary>
    public static readonly UiProperty<Color> PlaceholderForegroundProperty =
        UiProperty.Register<TextBox, Color>(
            nameof(PlaceholderForeground),
            new Color(139, 148, 160),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="SelectionBackground"/> property.
    /// </summary>
    public static readonly UiProperty<Color> SelectionBackgroundProperty =
        UiProperty.Register<TextBox, Color>(
            nameof(SelectionBackground),
            new Color(47, 100, 160),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="CaretColor"/> property.
    /// </summary>
    public static readonly UiProperty<Color> CaretColorProperty =
        UiProperty.Register<TextBox, Color>(
            nameof(CaretColor),
            new Color(242, 244, 247),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="IsReadOnly"/> property.
    /// </summary>
    public static readonly UiProperty<bool> IsReadOnlyProperty =
        UiProperty.Register<TextBox, bool>(
            nameof(IsReadOnly),
            false,
            UiPropertyInvalidation.Render);

    private static readonly UiKeyBindings<TextBox> KeyBindings =
        new UiKeyBindings<TextBox>()
            .AddBinding(
                Key.Left,
                static (textBox, _) => textBox.MoveByTextElement(-1, extend: false))
            .AddBinding(
                Key.Left,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveByTextElement(-1, extend: true))
            .AddBinding(
                Key.Right,
                static (textBox, _) => textBox.MoveByTextElement(1, extend: false))
            .AddBinding(
                Key.Right,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveByTextElement(1, extend: true))
            .AddBinding(
                Key.Home,
                static (textBox, _) => textBox.MoveTo(0, extend: false))
            .AddBinding(
                Key.Home,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveTo(0, extend: true))
            .AddBinding(
                Key.End,
                static (textBox, _) => textBox.MoveTo(textBox.Text.Length, extend: false))
            .AddBinding(
                Key.End,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveTo(textBox.Text.Length, extend: true))
            .AddBinding(
                Key.Backspace,
                static (textBox, _) => textBox.Delete(
                    -1,
                    byWord: false,
                    TextEditingState.DeleteKind.Backspace))
            .AddBinding(
                Key.Delete,
                static (textBox, _) => textBox.Delete(
                    1,
                    byWord: false,
                    TextEditingState.DeleteKind.Delete))
            .AddBinding(
                Key.Delete,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.Cut(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.Insert,
                KeyModifiers.Shift,
                static (textBox, _) => textBox.Paste(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.Left,
                KeyModifiers.Control,
                static (textBox, _) => textBox.MoveByWord(-1, extend: false))
            .AddBinding(
                Key.Left,
                KeyModifiers.Control | KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveByWord(-1, extend: true))
            .AddBinding(
                Key.Right,
                KeyModifiers.Control,
                static (textBox, _) => textBox.MoveByWord(1, extend: false))
            .AddBinding(
                Key.Right,
                KeyModifiers.Control | KeyModifiers.Shift,
                static (textBox, _) => textBox.MoveByWord(1, extend: true))
            .AddBinding(
                Key.Backspace,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Delete(
                    -1,
                    byWord: true,
                    TextEditingState.DeleteKind.Backspace))
            .AddBinding(
                Key.Delete,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Delete(
                    1,
                    byWord: true,
                    TextEditingState.DeleteKind.Delete))
            .AddBinding(
                Key.A,
                KeyModifiers.Control,
                static (textBox, _) => textBox.SelectAll())
            .AddBinding(
                Key.C,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Copy(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.Insert,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Copy(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.X,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Cut(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.V,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Paste(textBox.KeyBindingClipboard))
            .AddBinding(
                Key.Z,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Undo())
            .AddBinding(
                Key.Z,
                KeyModifiers.Control | KeyModifiers.Shift,
                static (textBox, _) => textBox.Redo())
            .AddBinding(
                Key.Y,
                KeyModifiers.Control,
                static (textBox, _) => textBox.Redo());

    private readonly TextEditingState _editingState = new();
    private CaretStop[] _caretStops = [new(0, 0)];
    private TextEditingState.Change? _pendingChange;
    private Clipboard? _keyBindingClipboardOverride;
    private TextLayout? _layout;
    private double _caretWidth = 1;
    private double _horizontalOffset;
    private double _textY;
    private long _caretPhaseStart = Stopwatch.GetTimestamp();
    private bool _isDraggingSelection;
    private bool _drawLayout;
    private bool _layoutIsPlaceholder;
    private bool _hasKeyBindingClipboardOverride;

    /// <summary>
    /// Initializes a text box with its default focus and decoration values.
    /// </summary>
    public TextBox()
    {
        Focusable = true;
        MinWidth = 160;
        Padding = new Thickness(8, 6);
        Background = new SolidColorBrush(31, 35, 41);
        BorderBrush = new SolidColorBrush(92, 103, 116);
        BorderThickness = new Thickness(1);
    }

    /// <summary>
    /// Gets or sets the plain UTF-16 text value.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the text displayed while <see cref="Text"/> is empty.
    /// </summary>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font request.
    /// </summary>
    public Font Font
    {
        get => GetValue(FontProperty);
        set => SetValue(FontProperty, value);
    }

    /// <summary>
    /// Gets or sets the text size in logical pixels.
    /// </summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied text color.
    /// </summary>
    public Color Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied placeholder text color.
    /// </summary>
    public Color PlaceholderForeground
    {
        get => GetValue(PlaceholderForegroundProperty);
        set => SetValue(PlaceholderForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied selection background color.
    /// </summary>
    public Color SelectionBackground
    {
        get => GetValue(SelectionBackgroundProperty);
        set => SetValue(SelectionBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied caret color.
    /// </summary>
    public Color CaretColor
    {
        get => GetValue(CaretColorProperty);
        set => SetValue(CaretColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether user input can modify the text.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets the UTF-16 offset of the selection extent.
    /// </summary>
    public int CaretIndex => _editingState.Caret;

    /// <summary>
    /// Gets the lowest UTF-16 offset in the current selection.
    /// </summary>
    public int SelectionStart => _editingState.SelectionStart;

    /// <summary>
    /// Gets the UTF-16 length of the current selection.
    /// </summary>
    public int SelectionLength => _editingState.SelectionLength;

    /// <summary>
    /// Gets the currently selected text.
    /// </summary>
    public string SelectedText => Text.Substring(SelectionStart, SelectionLength);

    /// <summary>
    /// Gets whether an edit can be undone.
    /// </summary>
    public bool CanUndo => _editingState.CanUndo;

    /// <summary>
    /// Gets whether an undone edit can be redone.
    /// </summary>
    public bool CanRedo => _editingState.CanRedo;

    /// <summary>
    /// Selects a UTF-16 range on text element boundaries.
    /// </summary>
    /// <param name="start">The selection start.</param>
    /// <param name="length">The selection length.</param>
    public void Select(int start, int length)
    {
        VerifyEditingAccess();
        _editingState.Select(Text, start, length);
        SelectionChanged();
    }

    /// <summary>
    /// Selects the entire text value.
    /// </summary>
    public void SelectAll()
    {
        VerifyEditingAccess();
        _editingState.SelectAll(Text);
        SelectionChanged();
    }

    /// <summary>
    /// Collapses the selection at the current caret position.
    /// </summary>
    public void ClearSelection()
    {
        VerifyEditingAccess();
        _editingState.ClearSelection();
        SelectionChanged();
    }

    /// <summary>
    /// Copies the selected text to the client clipboard.
    /// </summary>
    public void Copy() => Copy(ClientEngine.Current?.Clipboard);

    internal void Copy(Clipboard? clipboard)
    {
        VerifyEditingAccess();
        if (clipboard is null || SelectionLength == 0)
            return;
        clipboard.SetText(SelectedText);
    }

    /// <summary>
    /// Copies and removes the selected text when editing is enabled.
    /// </summary>
    public void Cut() => Cut(ClientEngine.Current?.Clipboard);

    internal void Cut(Clipboard? clipboard)
    {
        VerifyEditingAccess();
        if (IsReadOnly || clipboard is null || SelectionLength == 0)
            return;

        clipboard.SetText(SelectedText);
        CommitChange(_editingState.CreateDelete(
            Text,
            -1,
            byWord: false,
            TextEditingState.DeleteKind.Cut));
    }

    /// <summary>
    /// Replaces the selection with text from the client clipboard.
    /// </summary>
    public void Paste() => Paste(ClientEngine.Current?.Clipboard);

    internal void Paste(Clipboard? clipboard)
    {
        VerifyEditingAccess();
        if (IsReadOnly || clipboard is null)
            return;

        if (!clipboard.TryGetText(out var clipboardText))
            return;
        var text = FilterInput(clipboardText);
        if (text.Length == 0)
            return;
        CommitChange(_editingState.CreateInsert(Text, text, TextEditingState.InsertKind.Paste));
    }

    /// <summary>
    /// Undoes the most recent edit group.
    /// </summary>
    public void Undo()
    {
        VerifyEditingAccess();
        if (!IsReadOnly && _editingState.TryCreateUndo(Text, out var change))
            CommitChange(change);
    }

    /// <summary>
    /// Redoes the most recently undone edit group.
    /// </summary>
    public void Redo()
    {
        VerifyEditingAccess();
        if (!IsReadOnly && _editingState.TryCreateRedo(Text, out var change))
            CommitChange(change);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, TextProperty))
        {
            var change = (UiPropertyChangedEventArgs<string>)eventArgs;
            if (_pendingChange is { } pending &&
                string.Equals(pending.ExpectedText, change.OldValue, StringComparison.Ordinal) &&
                string.Equals(pending.Text, change.NewValue, StringComparison.Ordinal))
            {
                _editingState.Apply(pending);
            }
            else
                _editingState.SynchronizeExternalText(change.NewValue);

            ResetCaretPhase();
        }
        else if (ReferenceEquals(eventArgs.Property, IsReadOnlyProperty))
            ResetCaretPhase();

        base.OnPropertyChanged(eventArgs);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var text = Text;
        var placeholder = Placeholder;
        string layoutText;
        _layoutIsPlaceholder = text.Length == 0 && placeholder.Length != 0;
        _drawLayout = text.Length != 0 || placeholder.Length != 0;
        if (text.Length != 0)
            layoutText = CreateDisplayProjection(text);
        else if (placeholder.Length != 0)
            layoutText = CreateDisplayProjection(placeholder);
        else
            layoutText = " ";

        _layout = TextServices.TextLayoutEngine.Layout(new TextLayoutRequest(
            layoutText,
            Font,
            FontSize,
            availableSize.Width,
            1,
            TextWrapping.NoWrap,
            TextAlignment.Left));
        _caretWidth = GetCaretWidth();
        _caretStops = text.Length == 0
            ? [new CaretStop(0, 0)]
            : CreateCaretStops(text, _layout);

        var naturalWidth = text.Length != 0
            ? _layout.Width + _caretWidth
            : Math.Max(_drawLayout ? _layout.Width : 0, _caretWidth);
        var width = double.IsPositiveInfinity(availableSize.Width)
            ? naturalWidth
            : Math.Min(naturalWidth, availableSize.Width);
        return new Size(width, _layout.Height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var layout = _layout!;
        _textY = contentBounds.Y + (contentBounds.Height - layout.Height) / 2;
        EnsureCaretVisible(contentBounds);
    }

    /// <inheritdoc />
    protected override void DrawCore(UiDrawingContext context)
    {
        base.DrawCore(context);
        var layout = _layout!;
        var contentBounds = ContentBounds;
        using (context.PushClip(contentBounds))
        {
            if (IsFocused && SelectionLength != 0)
            {
                var start = GetCaretX(SelectionStart);
                var end = GetCaretX(SelectionStart + SelectionLength);
                context.FillRectangle(
                    new Rect(
                        contentBounds.X + start - _horizontalOffset,
                        _textY,
                        end - start,
                        layout.Height),
                    SelectionBackground);
            }

            if (_drawLayout)
            {
                context.DrawText(
                    new Point(
                        contentBounds.X - (_layoutIsPlaceholder ? 0 : _horizontalOffset),
                        _textY),
                    layout,
                    FontSize,
                    _layoutIsPlaceholder ? PlaceholderForeground : Foreground);
            }

            if (IsEnabled && IsFocused && !IsReadOnly && SelectionLength == 0 && IsCaretVisible())
            {
                context.FillRectangle(
                    new Rect(
                        contentBounds.X + GetCaretX(CaretIndex) - _horizontalOffset,
                        _textY,
                        _caretWidth,
                        layout.Height),
                    CaretColor);
            }
        }

        if (!IsEnabled)
            context.FillRectangle(DecorationBounds, new Color(0, 0, 0, 112));
        else if (IsFocused)
            DrawFocusFrame(context);
    }

    /// <inheritdoc />
    protected override void OnTextInput(UiTextInputEventArgs eventArgs)
    {
        base.OnTextInput(eventArgs);
        if (!IsFocused)
            return;

        eventArgs.Handled = true;
        if (IsReadOnly)
            return;
        var text = FilterInput(eventArgs.Text);
        if (text.Length != 0)
        {
            CommitChange(_editingState.CreateInsert(
                Text,
                text,
                TextEditingState.InsertKind.Typing));
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (IsFocused && KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !IsFocused)
            return;

        var caret = HitTestCaret(eventArgs.GetPosition(this).X);
        if ((eventArgs.Modifiers & KeyModifiers.Shift) != 0)
            _editingState.MoveTo(Text, caret, extend: true);
        else
            _editingState.Select(Text, caret, 0);
        _isDraggingSelection = true;
        SelectionChanged();
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(UiPointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (!_isDraggingSelection || !IsPressed)
        {
            _isDraggingSelection = false;
            return;
        }

        if (!IsArrangeValid)
        {
            eventArgs.Handled = true;
            return;
        }

        _editingState.MoveTo(Text, HitTestCaret(eventArgs.GetPosition(this).X), extend: true);
        SelectionChanged();
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left || !_isDraggingSelection)
            return;

        _isDraggingSelection = false;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(UiFocusChangedEventArgs eventArgs)
    {
        ResetCaretPhase();
        EnsureCaretVisibleIfArranged();
        base.OnGotFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(UiFocusChangedEventArgs eventArgs)
    {
        _isDraggingSelection = false;
        _editingState.EndEditGroup();
        base.OnLostFocus(eventArgs);
    }

    internal bool TryHandleKey(Key key, KeyModifiers modifiers, Clipboard? clipboard)
    {
        var previousClipboard = _keyBindingClipboardOverride;
        var hadPreviousOverride = _hasKeyBindingClipboardOverride;
        _keyBindingClipboardOverride = clipboard;
        _hasKeyBindingClipboardOverride = true;
        try
        {
            var eventArgs = new UiKeyEventArgs(
                this,
                key,
                modifiers,
                isRepeat: false);
            return KeyBindings.TryHandle(this, eventArgs, KeyAction.Press);
        }
        finally
        {
            _keyBindingClipboardOverride = previousClipboard;
            _hasKeyBindingClipboardOverride = hadPreviousOverride;
        }
    }

    private Clipboard? KeyBindingClipboard =>
        _hasKeyBindingClipboardOverride
            ? _keyBindingClipboardOverride
            : ClientEngine.Current?.Clipboard;

    private void MoveByTextElement(int direction, bool extend)
    {
        _editingState.MoveByTextElement(Text, direction, extend);
        SelectionChanged();
    }

    private void MoveByWord(int direction, bool extend)
    {
        _editingState.MoveByWord(Text, direction, extend);
        SelectionChanged();
    }

    private void MoveTo(int index, bool extend)
    {
        _editingState.MoveTo(Text, index, extend);
        SelectionChanged();
    }

    private void Delete(int direction, bool byWord, TextEditingState.DeleteKind kind)
    {
        if (IsReadOnly)
            return;
        CommitChange(_editingState.CreateDelete(Text, direction, byWord, kind));
    }

    private void CommitChange(TextEditingState.Change change)
    {
        if (!change.ChangesText)
        {
            _editingState.Apply(change);
            SelectionChanged();
            return;
        }

        _pendingChange = change;
        try
        {
            Text = change.Text;
        }
        finally
        {
            _pendingChange = null;
        }
    }

    private void SelectionChanged()
    {
        ResetCaretPhase();
        EnsureCaretVisibleIfArranged();
    }

    private void VerifyEditingAccess() => Screen?.VerifyTreeAccess();

    private void EnsureCaretVisibleIfArranged()
    {
        if (IsArrangeValid)
            EnsureCaretVisible(ContentBounds);
    }

    private void EnsureCaretVisible(Rect contentBounds)
    {
        var layoutWidth = _layout?.Width ?? 0;
        var maximumOffset = Math.Max(0, layoutWidth + _caretWidth - contentBounds.Width);
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, maximumOffset);
        var caretX = GetCaretX(CaretIndex);
        if (caretX < _horizontalOffset)
            _horizontalOffset = caretX;
        else if (caretX + _caretWidth > _horizontalOffset + contentBounds.Width)
            _horizontalOffset = caretX + _caretWidth - contentBounds.Width;
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, maximumOffset);
    }

    private int HitTestCaret(double localX)
    {
        if (_caretStops.Length == 1)
            return 0;
        var contentBounds = ContentBounds;
        if (localX <= contentBounds.X)
            return _caretStops[0].Index;
        if (localX >= contentBounds.X + contentBounds.Width)
            return _caretStops[^1].Index;

        var x = localX - contentBounds.X + _horizontalOffset;
        for (var index = 1; index < _caretStops.Length; index++)
        {
            var midpoint = (_caretStops[index - 1].X + _caretStops[index].X) / 2;
            if (x < midpoint)
                return _caretStops[index - 1].Index;
        }

        return _caretStops[^1].Index;
    }

    private double GetCaretX(int index)
    {
        var stopIndex = Array.BinarySearch(
            _caretStops,
            new CaretStop(index, 0),
            CaretStopIndexComparer.Instance);
        return _caretStops[stopIndex].X;
    }

    private double GetCaretWidth()
    {
        var screen = Screen;
        return screen?.UseLayoutRounding ?? true
            ? UiLayoutHelper.RoundLayoutValue(1, screen?.Scale ?? 1)
            : 1;
    }

    private void ResetCaretPhase() => _caretPhaseStart = Stopwatch.GetTimestamp();

    private bool IsCaretVisible()
    {
        var halfPeriod = Stopwatch.Frequency / 2;
        return (Stopwatch.GetTimestamp() - _caretPhaseStart) / halfPeriod % 2 == 0;
    }

    private void DrawFocusFrame(UiDrawingContext context)
    {
        var screen = Screen;
        var thickness = screen?.UseLayoutRounding ?? true
            ? UiLayoutHelper.RoundLayoutValue(1d, screen?.Scale ?? 1)
            : 1d;
        if (thickness == 0)
            return;

        var bounds = DecorationBounds;
        var innerX = bounds.X + Math.Min(thickness, bounds.Width);
        var innerY = bounds.Y + Math.Min(thickness, bounds.Height);
        var innerWidth = Math.Max(0, bounds.Width - thickness - thickness);
        var innerHeight = Math.Max(0, bounds.Height - thickness - thickness);
        var color = new Color(84, 169, 255);
        context.FillRectangle(new Rect(bounds.X, bounds.Y, bounds.Width, innerY - bounds.Y), color);
        context.FillRectangle(
            new Rect(
                innerX + innerWidth,
                innerY,
                bounds.X + bounds.Width - (innerX + innerWidth),
                innerHeight),
            color);
        context.FillRectangle(
            new Rect(
                bounds.X,
                innerY + innerHeight,
                bounds.Width,
                bounds.Y + bounds.Height - (innerY + innerHeight)),
            color);
        context.FillRectangle(new Rect(bounds.X, innerY, innerX - bounds.X, innerHeight), color);
    }

    private static string FilterInput(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n' or '\u2028' or '\u2029'))
                continue;
            text = text[..index];
            break;
        }

        return text.Replace('\t', ' ');
    }

    private static string CreateDisplayProjection(string text) =>
        string.Create(text.Length, text, static (destination, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                destination[index] = source[index] is '\r' or '\n' or '\u2028' or '\u2029' or '\t'
                    ? ' '
                    : source[index];
            }
        });

    private static CaretStop[] CreateCaretStops(string text, TextLayout layout)
    {
        var sourcePositions = new SortedDictionary<int, double>();
        foreach (var line in layout.Lines)
        {
            foreach (var run in line.GlyphRuns)
            {
                foreach (var glyph in run.Glyphs)
                {
                    if (!sourcePositions.TryGetValue(glyph.Cluster, out var x) || glyph.X < x)
                        sourcePositions[glyph.Cluster] = glyph.X;
                }
            }
        }

        sourcePositions[0] = 0;
        sourcePositions[text.Length] = layout.Width;

        var clusterIndices = sourcePositions.Keys.ToArray();
        var clusterPositions = sourcePositions.Values.ToArray();
        var elementIndices = StringInfo.ParseCombiningCharacters(text);
        var sourceStops = new int[elementIndices.Length + 1];
        elementIndices.CopyTo(sourceStops, 0);
        sourceStops[^1] = text.Length;
        var result = new CaretStop[sourceStops.Length];
        var previousX = 0d;
        for (var stopIndex = 0; stopIndex < sourceStops.Length; stopIndex++)
        {
            var sourceIndex = sourceStops[stopIndex];
            var exactCluster = Array.BinarySearch(clusterIndices, sourceIndex);
            double x;
            if (exactCluster >= 0)
                x = clusterPositions[exactCluster];
            else
            {
                var upperCluster = ~exactCluster;
                var lowerCluster = upperCluster - 1;
                var internalStopCount = 0;
                var rank = 0;
                foreach (var candidate in sourceStops)
                {
                    if (candidate <= clusterIndices[lowerCluster] || candidate >= clusterIndices[upperCluster])
                        continue;
                    internalStopCount++;
                    if (candidate <= sourceIndex)
                        rank++;
                }

                var fraction = rank / (double)(internalStopCount + 1);
                x = clusterPositions[lowerCluster] +
                    (clusterPositions[upperCluster] - clusterPositions[lowerCluster]) * fraction;
            }

            x = Math.Max(previousX, x);
            result[stopIndex] = new CaretStop(sourceIndex, x);
            previousX = x;
        }

        return result;
    }

    private readonly record struct CaretStop(int Index, double X);

    private sealed class CaretStopIndexComparer : IComparer<CaretStop>
    {
        internal static readonly CaretStopIndexComparer Instance = new();

        public int Compare(CaretStop x, CaretStop y) => x.Index.CompareTo(y.Index);
    }
}