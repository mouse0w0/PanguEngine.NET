using PanguEngine.Client.Game;
using PanguEngine.Input;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientInputStateTests
{
    [Fact]
    public void KeyStateTracksPressAndRelease()
    {
        var input = CreateInput(out _);

        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        Assert.True(input.IsKeyDown(Key.W));

        input.HandleKeyUp(new KeyEventArgs(Key.W, KeyAction.Release, KeyModifiers.None));
        Assert.False(input.IsKeyDown(Key.W));
    }

    [Fact]
    public void LeftClickCapturesMouseAndUsesClickAsBaseline()
    {
        var input = CreateInput(out var cursorStates);
        var deltas = new List<Vector2D<float>>();
        input.MouseDelta += deltas.Add;

        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        input.HandleMouseMove(new MouseMoveEventArgs(13, 18));

        Assert.True(input.IsMouseCaptured);
        Assert.Equal([CursorState.Disabled], cursorStates);
        Assert.Equal([new Vector2D<float>(3, -2)], deltas);
    }

    [Fact]
    public void LeftClickRequestRequiresCapturedMouseAndIsConsumedOnce()
    {
        var input = CreateInput(out _);

        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));

        Assert.False(input.ConsumeLeftClickRequest());

        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));

        Assert.True(input.ConsumeLeftClickRequest());
        Assert.False(input.ConsumeLeftClickRequest());
    }

    [Fact]
    public void LeftClickWhileCapturedDoesNotResetMouseBaseline()
    {
        var input = CreateInput(out _);
        var deltas = new List<Vector2D<float>>();
        input.MouseDelta += deltas.Add;
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        input.HandleMouseMove(new MouseMoveEventArgs(13, 18));

        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 100, 100));
        input.HandleMouseMove(new MouseMoveEventArgs(15, 17));

        Assert.Equal(
            [new Vector2D<float>(3, -2), new Vector2D<float>(2, -1)],
            deltas);
    }

    [Fact]
    public void FirstMoveAfterCaptureWithoutClickPositionOnlyEstablishesBaseline()
    {
        var input = CreateInput(out _);
        var deltas = new List<Vector2D<float>>();
        input.MouseDelta += deltas.Add;

        input.CaptureMouse();
        input.HandleMouseMove(new MouseMoveEventArgs(20, 30));
        input.HandleMouseMove(new MouseMoveEventArgs(24, 28));

        Assert.Equal([new Vector2D<float>(4, -2)], deltas);
    }

    [Fact]
    public void EscapeReleasesMouseAndStopsMouseDelta()
    {
        var input = CreateInput(out var cursorStates);
        var deltaCount = 0;
        input.MouseDelta += _ => deltaCount++;
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 1, 2));

        input.HandleKeyDown(new KeyEventArgs(Key.Escape, KeyAction.Press, KeyModifiers.None));
        input.HandleMouseMove(new MouseMoveEventArgs(3, 4));

        Assert.False(input.IsMouseCaptured);
        Assert.Equal([CursorState.Disabled, CursorState.Normal], cursorStates);
        Assert.Equal(0, deltaCount);
    }

    [Fact]
    public void LosingFocusClearsKeysAndReleasesMouse()
    {
        var input = CreateInput(out var cursorStates);
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 1, 2));

        input.HandleFocusChanged(false);

        Assert.False(input.IsKeyDown(Key.W));
        Assert.False(input.IsMouseCaptured);
        Assert.Equal(CursorState.Normal, cursorStates[^1]);
    }

    [Fact]
    public void RightClickRequestIsConsumedOnce()
    {
        var input = CreateInput(out _);

        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Right, 0, 0));

        Assert.True(input.ConsumeRightClickRequest());
        Assert.False(input.ConsumeRightClickRequest());
    }

    [Fact]
    public void SuspendForUiClearsGameStateAndReturnsPriorCapture()
    {
        var input = CreateInput(out var cursorStates);
        var deltas = new List<Vector2D<float>>();
        input.MouseDelta += deltas.Add;
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Right, 10, 20));
        input.HandleMouseMove(new MouseMoveEventArgs(13, 18));
        deltas.Clear();

        var first = input.SuspendForUi();
        var second = input.SuspendForUi();
        input.HandleMouseMove(new MouseMoveEventArgs(30, 40));

        Assert.True(first);
        Assert.False(second);
        Assert.False(input.IsKeyDown(Key.W));
        Assert.False(input.ConsumeLeftClickRequest());
        Assert.False(input.ConsumeRightClickRequest());
        Assert.False(input.IsMouseCaptured);
        Assert.Empty(deltas);
        Assert.Equal(CursorState.Normal, cursorStates[^1]);
    }

    [Fact]
    public void SuspendForUiKeepsUncapturedMouseNormal()
    {
        var input = CreateInput(out var cursorStates);

        var wasMouseCaptured = input.SuspendForUi();

        Assert.False(wasMouseCaptured);
        Assert.False(input.IsMouseCaptured);
        Assert.Equal([CursorState.Normal], cursorStates);
    }

    [Fact]
    public void DestroyReleasesMouseAndClearsState()
    {
        var input = CreateInput(out var cursorStates);
        var deltaCount = 0;
        input.MouseDelta += _ => deltaCount++;
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 1, 2));

        input.Destroy();
        input.Destroy();
        input.HandleMouseMove(new MouseMoveEventArgs(3, 4));

        Assert.False(input.IsKeyDown(Key.W));
        Assert.Equal(0, deltaCount);
        Assert.Equal(CursorState.Normal, cursorStates[^1]);
    }

    private static ClientInputState CreateInput(out List<CursorState> cursorStates)
    {
        cursorStates = [];
        var capturedStates = cursorStates;
        return new ClientInputState(capturedStates.Add);
    }
}
