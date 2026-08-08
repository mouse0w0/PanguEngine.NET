namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    private bool _isDrawing;

    /// <summary>
    /// Creates an immutable snapshot of drawing commands for the current root subtree.
    /// </summary>
    /// <returns>The commands in stable drawing order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from the wrong thread, the screen is changing lifecycle
    /// or layout state, command generation is reentered, or drawing code mutates UI state.
    /// </exception>
    public UiDrawCommandList CreateDrawCommandList()
    {
        BeginDrawing();
        try
        {
            var root = Root;
            if (root is null)
                return UiDrawCommandList.Empty;

            var commands = new List<UiDrawCommand>();
            root.AppendDrawCommands(
                commands,
                new UiDrawingState(0, 0, null, false, 1));
            return commands.Count == 0
                ? UiDrawCommandList.Empty
                : new UiDrawCommandList(commands);
        }
        finally
        {
            EndDrawing();
        }
    }

    internal bool IsDrawing
    {
        get
        {
            lock (_stateSync)
                return _isDrawing;
        }
    }

    internal void VerifyTreeMutationAccess()
    {
        lock (_stateSync)
        {
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (_isDrawing)
            {
                throw new InvalidOperationException(
                    "The UI screen tree cannot change while drawing commands are generated.");
            }
        }
    }

    private void BeginDrawing()
    {
        lock (_stateSync)
        {
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (_isTransitioning)
                throw new InvalidOperationException("The UI screen cannot draw during a lifecycle transition.");
            if (IsUpdatingLayout)
                throw new InvalidOperationException("The UI screen cannot draw while layout is updating.");
            if (_isDrawing)
                throw new InvalidOperationException("The UI screen is already generating drawing commands.");

            _isDrawing = true;
            _operationDepth++;
        }
    }

    private void EndDrawing()
    {
        lock (_stateSync)
        {
            _operationDepth--;
            _isDrawing = false;
        }
    }
}
