using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    private bool _isDrawing;

    /// <summary>
    /// Creates a new command list recording the current root subtree.
    /// </summary>
    /// <returns>The commands in stable drawing order.</returns>
    /// <remarks>
    /// An open screen must be laid out before its first draw and after its output size or scale changes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from the wrong thread, the screen is changing lifecycle
    /// or layout state, an update callback is running, command generation is reentered, or drawing code mutates UI state.
    /// </exception>
    public UiDrawCommandList CreateDrawCommandList()
    {
        var commands = new UiDrawCommandList();
        commands.Append(this);
        return commands;
    }

    internal void AppendDrawCommands(List<UiDrawCommand> commands)
    {
        BeginDrawing();
        try
        {
            var transformIndex = UiDrawingContext.PushTransform(commands, Point.Zero, Scale);
            Root?.AppendDrawCommands(commands, 1);
            UiDrawingContext.PopCommand(commands, transformIndex);
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
            VerifyNotPreparingStyleSheets();
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
            if (IsUpdating)
                throw new InvalidOperationException("The UI screen cannot draw during an update callback.");
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
