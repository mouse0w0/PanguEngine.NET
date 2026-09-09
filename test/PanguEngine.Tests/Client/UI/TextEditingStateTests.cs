using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class TextEditingStateTests
{
    [Fact]
    public void SelectionInsertionAndDeletionUseUtf16Offsets()
    {
        var state = new TextEditingState();
        var text = "abcd";
        state.SynchronizeExternalText(text);
        state.Select(text, 1, 2);

        Apply(ref text, state, state.CreateInsert(text, "X", TextEditingState.InsertKind.Typing));

        Assert.Equal("aXd", text);
        Assert.Equal(2, state.Anchor);
        Assert.Equal(2, state.Caret);
        Assert.Equal(0, state.SelectionLength);

        Apply(ref text, state, state.CreateDelete(text, -1, byWord: false, TextEditingState.DeleteKind.Backspace));
        Assert.Equal("ad", text);
        Apply(ref text, state, state.CreateDelete(text, 1, byWord: false, TextEditingState.DeleteKind.Delete));
        Assert.Equal("a", text);
    }

    [Fact]
    public void EqualReplacementCommitsSelectionOnlyWithoutHistory()
    {
        var state = new TextEditingState();
        var text = "same";
        state.SynchronizeExternalText(text);
        state.Select(text, 0, text.Length);

        var change = state.CreateInsert(text, "same", TextEditingState.InsertKind.Typing);
        Apply(ref text, state, change);

        Assert.False(change.ChangesText);
        Assert.Equal(4, state.Caret);
        Assert.Equal(0, state.SelectionLength);
        Assert.False(state.CanUndo);
    }

    [Fact]
    public void CharacterMovementDoesNotSplitExtendedTextElements()
    {
        var state = new TextEditingState();
        const string text = "A\U0001F600e\u0301\u2708\uFE0F\U0001F469\u200D\U0001F4BBZ";
        state.SynchronizeExternalText(text);
        var positions = new List<int>();

        while (state.Caret != 0)
        {
            state.MoveByTextElement(text, -1, extend: false);
            positions.Add(state.Caret);
        }

        Assert.Equal([12, 7, 5, 3, 1, 0], positions);
        state.MoveByTextElement(text, 1, extend: true);
        state.MoveByTextElement(text, 1, extend: true);
        Assert.Equal(0, state.Anchor);
        Assert.Equal(3, state.Caret);
        Assert.Equal(3, state.SelectionLength);
    }

    [Fact]
    public void SelectRejectsOffsetsInsideTextElements()
    {
        var state = new TextEditingState();
        const string text = "A\U0001F600e\u0301";
        state.SynchronizeExternalText(text);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Select(text, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Select(text, 1, 1));
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(
            () => state.Select(text, -1, 1)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(
            () => state.Select(text, text.Length + 1, 0)).ParamName);
        Assert.Equal("length", Assert.Throws<ArgumentOutOfRangeException>(
            () => state.Select(text, 0, text.Length + 1)).ParamName);
    }

    [Fact]
    public void EditsKeepTheCaretOnResultingTextElementBoundaries()
    {
        var state = new TextEditingState();
        var text = "\u0301";
        state.SynchronizeExternalText(text);
        state.MoveTo(text, 0, extend: false);

        Apply(ref text, state, state.CreateInsert(text, "a", TextEditingState.InsertKind.Typing));

        Assert.Equal("a\u0301", text);
        Assert.Equal(2, state.Caret);

        text = "\U0001F1E6X\U0001F1E7";
        state.SynchronizeExternalText(text);
        state.Select(text, 2, 1);
        Apply(ref text, state, state.CreateDelete(
            text,
            -1,
            byWord: false,
            TextEditingState.DeleteKind.Backspace));

        Assert.Equal("\U0001F1E6\U0001F1E7", text);
        Assert.Equal(4, state.Caret);
    }

    [Fact]
    public void WordMovementUsesRightAndLeftAdjacentClasses()
    {
        var state = new TextEditingState();
        const string text = "one  two,...三";
        state.SynchronizeExternalText(text);
        state.MoveTo(text, 0, extend: false);
        var right = new List<int>();
        var left = new List<int>();

        while (state.Caret != text.Length)
        {
            state.MoveByWord(text, 1, extend: false);
            right.Add(state.Caret);
        }
        while (state.Caret != 0)
        {
            state.MoveByWord(text, -1, extend: false);
            left.Add(state.Caret);
        }

        Assert.Equal([5, 8, 12, 13], right);
        Assert.Equal([12, 8, 5, 0], left);
    }

    [Fact]
    public void WordDeletionUsesTheSameBoundariesAsMovement()
    {
        var state = new TextEditingState();
        var text = "one  two,...三";
        state.SynchronizeExternalText(text);
        state.MoveTo(text, 5, extend: false);

        Apply(ref text, state, state.CreateDelete(text, -1, byWord: true, TextEditingState.DeleteKind.Backspace));
        Assert.Equal("two,...三", text);
        Apply(ref text, state, state.CreateDelete(text, 1, byWord: true, TextEditingState.DeleteKind.Delete));
        Assert.Equal(",...三", text);
    }

    [Fact]
    public void ConsecutiveTypingAndDeletionFormUndoGroups()
    {
        var state = new TextEditingState();
        var text = string.Empty;
        state.SynchronizeExternalText(text);
        Apply(ref text, state, state.CreateInsert(text, "a", TextEditingState.InsertKind.Typing));
        Apply(ref text, state, state.CreateInsert(text, "b", TextEditingState.InsertKind.Typing));
        Apply(ref text, state, state.CreateInsert(text, "c", TextEditingState.InsertKind.Typing));

        Assert.True(state.TryCreateUndo(text, out var undoTyping));
        Apply(ref text, state, undoTyping);
        Assert.Equal(string.Empty, text);
        Assert.True(state.TryCreateRedo(text, out var redoTyping));
        Apply(ref text, state, redoTyping);
        Assert.Equal("abc", text);

        Apply(ref text, state, state.CreateDelete(text, -1, byWord: false, TextEditingState.DeleteKind.Backspace));
        Apply(ref text, state, state.CreateDelete(text, -1, byWord: false, TextEditingState.DeleteKind.Backspace));
        Assert.True(state.TryCreateUndo(text, out var undoDeletion));
        Apply(ref text, state, undoDeletion);
        Assert.Equal("abc", text);
    }

    [Fact]
    public void MovementAndSelectionOnlyChangesEndTheCurrentEditGroup()
    {
        var state = new TextEditingState();
        var text = string.Empty;
        state.SynchronizeExternalText(text);
        Apply(ref text, state, state.CreateInsert(text, "a", TextEditingState.InsertKind.Typing));
        state.MoveByTextElement(text, -1, extend: false);
        state.MoveByTextElement(text, 1, extend: false);
        Apply(ref text, state, state.CreateInsert(text, "b", TextEditingState.InsertKind.Typing));
        state.Select(text, 1, 1);
        Apply(ref text, state, state.CreateInsert(text, "b", TextEditingState.InsertKind.Typing));
        Apply(ref text, state, state.CreateInsert(text, "c", TextEditingState.InsertKind.Typing));

        Assert.True(state.TryCreateUndo(text, out var undoC));
        Apply(ref text, state, undoC);
        Assert.Equal("ab", text);
        Assert.True(state.TryCreateUndo(text, out var undoB));
        Apply(ref text, state, undoB);
        Assert.Equal("a", text);
        Assert.True(state.TryCreateUndo(text, out var undoA));
        Apply(ref text, state, undoA);
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void NewEditClearsRedoAndHistoryKeepsOneHundredGroups()
    {
        var state = new TextEditingState();
        var text = string.Empty;
        state.SynchronizeExternalText(text);
        for (var index = 0; index < 101; index++)
            Apply(ref text, state, state.CreateInsert(text, "x", TextEditingState.InsertKind.Paste));

        var undoCount = 0;
        while (state.TryCreateUndo(text, out var undo))
        {
            Apply(ref text, state, undo);
            undoCount++;
        }

        Assert.Equal(100, undoCount);
        Assert.Equal("x", text);
        Assert.True(state.TryCreateRedo(text, out var redo));
        Apply(ref text, state, redo);
        Apply(ref text, state, state.CreateInsert(text, "y", TextEditingState.InsertKind.Paste));
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void ExternalTextClearsHistoryAndMovesCaretToEnd()
    {
        var state = new TextEditingState();
        var text = "a";
        state.SynchronizeExternalText(text);
        Apply(ref text, state, state.CreateInsert(text, "b", TextEditingState.InsertKind.Typing));

        state.SynchronizeExternalText("replacement");

        Assert.Equal(11, state.Anchor);
        Assert.Equal(11, state.Caret);
        Assert.False(state.CanUndo);
        Assert.False(state.CanRedo);
    }

    private static void Apply(
        ref string text,
        TextEditingState state,
        TextEditingState.Change change)
    {
        Assert.Equal(text, change.ExpectedText);
        text = change.Text;
        state.Apply(change);
    }
}
