using System.Buffers;
using System.Globalization;
using System.Text;

namespace PanguEngine.Client.UI;

internal sealed class TextEditingState
{
    internal enum InsertKind
    {
        Typing,
        Paste
    }

    internal enum DeleteKind
    {
        Backspace,
        Delete,
        Cut
    }

    internal readonly struct Change
    {
        internal Change(
            string expectedText,
            string text,
            int anchor,
            int caret,
            ChangeKind kind,
            EditGroup group,
            HistoryEntry? entry)
        {
            ExpectedText = expectedText;
            Text = text;
            Anchor = anchor;
            Caret = caret;
            Kind = kind;
            Group = group;
            Entry = entry;
        }

        internal string ExpectedText { get; }
        internal string Text { get; }
        internal int Anchor { get; }
        internal int Caret { get; }
        internal ChangeKind Kind { get; }
        internal EditGroup Group { get; }
        internal HistoryEntry? Entry { get; }
        internal bool ChangesText =>
            !string.Equals(ExpectedText, Text, StringComparison.Ordinal);
    }

    private const int HistoryLimit = 100;

    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private EditGroup _activeGroup;

    internal int Anchor { get; private set; }
    internal int Caret { get; private set; }
    internal int SelectionStart => Math.Min(Anchor, Caret);
    internal int SelectionLength => Math.Abs(Anchor - Caret);
    internal bool CanUndo => _undo.Count != 0;
    internal bool CanRedo => _redo.Count != 0;

    internal void SynchronizeExternalText(string text)
    {
        Anchor = text.Length;
        Caret = text.Length;
        _undo.Clear();
        _redo.Clear();
        EndEditGroup();
    }

    internal void Select(string text, int start, int length)
    {
        if (start < 0 || start > text.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || length > text.Length - start)
            throw new ArgumentOutOfRangeException(nameof(length));

        var stops = GetTextElementStops(text);
        if (Array.BinarySearch(stops, start) < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (Array.BinarySearch(stops, start + length) < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Anchor = start;
        Caret = start + length;
        EndEditGroup();
    }

    internal void SelectAll(string text)
    {
        Anchor = 0;
        Caret = text.Length;
        EndEditGroup();
    }

    internal void ClearSelection()
    {
        Anchor = Caret;
        EndEditGroup();
    }

    internal void MoveByTextElement(string text, int direction, bool extend)
    {
        var stops = GetTextElementStops(text);
        if (!extend && SelectionLength != 0)
        {
            SetCaret(direction < 0 ? SelectionStart : SelectionStart + SelectionLength, extend: false);
            EndEditGroup();
            return;
        }

        var index = Array.BinarySearch(stops, Caret);
        var target = Math.Clamp(index + direction, 0, stops.Length - 1);
        SetCaret(stops[target], extend);
        EndEditGroup();
    }

    internal void MoveByWord(string text, int direction, bool extend)
    {
        if (!extend && SelectionLength != 0)
        {
            SetCaret(direction < 0 ? SelectionStart : SelectionStart + SelectionLength, extend: false);
            EndEditGroup();
            return;
        }

        SetCaret(FindWordBoundary(text, Caret, direction), extend);
        EndEditGroup();
    }

    internal void MoveTo(string text, int index, bool extend)
    {
        if (Array.BinarySearch(GetTextElementStops(text), index) < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        SetCaret(index, extend);
        EndEditGroup();
    }

    internal Change CreateInsert(string text, string value, InsertKind kind)
    {
        var start = SelectionStart;
        var end = start + SelectionLength;
        var candidate = text[..start] + value + text[end..];
        var caret = SnapToTextElementBoundary(candidate, start + value.Length);
        var group = kind == InsertKind.Typing && SelectionLength == 0
            ? EditGroup.Typing
            : EditGroup.None;
        return CreateEditChange(text, candidate, caret, caret, group);
    }

    internal Change CreateDelete(
        string text,
        int direction,
        bool byWord,
        DeleteKind kind)
    {
        var start = SelectionStart;
        var end = start + SelectionLength;
        var group = EditGroup.None;
        if (start == end)
        {
            var boundary = byWord
                ? FindWordBoundary(text, Caret, direction)
                : FindTextElementBoundary(text, Caret, direction);
            start = Math.Min(Caret, boundary);
            end = Math.Max(Caret, boundary);
            if (!byWord)
            {
                group = kind switch
                {
                    DeleteKind.Backspace => EditGroup.Backspace,
                    DeleteKind.Delete => EditGroup.Delete,
                    _ => EditGroup.None
                };
            }
        }

        var candidate = text.Remove(start, end - start);
        start = SnapToTextElementBoundary(candidate, start);
        return CreateEditChange(text, candidate, start, start, group);
    }

    internal bool TryCreateUndo(string text, out Change change)
    {
        if (_undo.Count == 0)
        {
            change = default;
            return false;
        }

        var entry = _undo[^1];
        change = new Change(
            text,
            entry.Before.Text,
            entry.Before.Anchor,
            entry.Before.Caret,
            ChangeKind.Undo,
            EditGroup.None,
            entry);
        return true;
    }

    internal bool TryCreateRedo(string text, out Change change)
    {
        if (_redo.Count == 0)
        {
            change = default;
            return false;
        }

        var entry = _redo[^1];
        change = new Change(
            text,
            entry.After.Text,
            entry.After.Anchor,
            entry.After.Caret,
            ChangeKind.Redo,
            EditGroup.None,
            entry);
        return true;
    }

    internal void Apply(Change change)
    {
        var before = new Snapshot(change.ExpectedText, Anchor, Caret);
        var after = new Snapshot(change.Text, change.Anchor, change.Caret);
        switch (change.Kind)
        {
            case ChangeKind.Selection:
                EndEditGroup();
                break;
            case ChangeKind.Edit:
                ApplyEdit(before, after, change.Group);
                break;
            case ChangeKind.Undo:
                _undo.RemoveAt(_undo.Count - 1);
                _redo.Add(change.Entry!);
                EndEditGroup();
                break;
            case ChangeKind.Redo:
                _redo.RemoveAt(_redo.Count - 1);
                _undo.Add(change.Entry!);
                EndEditGroup();
                break;
        }

        Anchor = change.Anchor;
        Caret = change.Caret;
    }

    internal void EndEditGroup() => _activeGroup = EditGroup.None;

    private void ApplyEdit(Snapshot before, Snapshot after, EditGroup group)
    {
        _redo.Clear();
        if (group != EditGroup.None &&
            group == _activeGroup &&
            _undo.Count != 0 &&
            _undo[^1].After == before)
        {
            _undo[^1] = _undo[^1] with { After = after };
        }
        else
        {
            _undo.Add(new HistoryEntry(before, after));
            if (_undo.Count > HistoryLimit)
                _undo.RemoveAt(0);
        }

        _activeGroup = group;
    }

    private static Change CreateEditChange(
        string text,
        string candidate,
        int anchor,
        int caret,
        EditGroup group)
    {
        var kind = string.Equals(text, candidate, StringComparison.Ordinal)
            ? ChangeKind.Selection
            : ChangeKind.Edit;
        return new Change(text, candidate, anchor, caret, kind, group, entry: null);
    }

    private void SetCaret(int caret, bool extend)
    {
        Caret = caret;
        if (!extend)
            Anchor = caret;
    }

    private static int FindTextElementBoundary(string text, int caret, int direction)
    {
        var stops = GetTextElementStops(text);
        var index = Array.BinarySearch(stops, caret);
        return stops[Math.Clamp(index + direction, 0, stops.Length - 1)];
    }

    private static int SnapToTextElementBoundary(string text, int index)
    {
        var stops = GetTextElementStops(text);
        var stopIndex = Array.BinarySearch(stops, index);
        return stopIndex >= 0 ? index : stops[~stopIndex];
    }

    private static int FindWordBoundary(string text, int caret, int direction)
    {
        var stops = GetTextElementStops(text);
        var index = Array.BinarySearch(stops, caret);
        var elementCount = stops.Length - 1;
        if (direction < 0)
        {
            while (index > 0 && GetWordClass(text, stops[index - 1], stops[index]) == WordClass.Whitespace)
                index--;
            if (index == 0)
                return 0;

            var wordClass = GetWordClass(text, stops[index - 1], stops[index]);
            while (index > 0 && GetWordClass(text, stops[index - 1], stops[index]) == wordClass)
                index--;
            return stops[index];
        }

        if (index == elementCount)
            return text.Length;
        var currentClass = GetWordClass(text, stops[index], stops[index + 1]);
        while (index < elementCount && GetWordClass(text, stops[index], stops[index + 1]) == currentClass)
            index++;
        while (index < elementCount && GetWordClass(text, stops[index], stops[index + 1]) == WordClass.Whitespace)
            index++;
        return stops[index];
    }

    private static WordClass GetWordClass(string text, int start, int end)
    {
        var status = Rune.DecodeFromUtf16(text.AsSpan(start, end - start), out var rune, out _);
        if (status != OperationStatus.Done)
            rune = Rune.ReplacementChar;
        if (Rune.IsWhiteSpace(rune))
            return WordClass.Whitespace;

        var category = Rune.GetUnicodeCategory(rune);
        return category is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.ConnectorPunctuation
                ? WordClass.Word
                : WordClass.Punctuation;
    }

    private static int[] GetTextElementStops(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        if (starts.Length == 0)
            return [0];
        if (starts[^1] == text.Length)
            return starts;

        var stops = new int[starts.Length + 1];
        starts.CopyTo(stops, 0);
        stops[^1] = text.Length;
        return stops;
    }

    internal enum ChangeKind
    {
        Selection,
        Edit,
        Undo,
        Redo
    }

    internal enum EditGroup
    {
        None,
        Typing,
        Backspace,
        Delete
    }

    private enum WordClass
    {
        Whitespace,
        Word,
        Punctuation
    }

    internal readonly record struct Snapshot(string Text, int Anchor, int Caret);
    internal sealed record HistoryEntry(Snapshot Before, Snapshot After);
}
