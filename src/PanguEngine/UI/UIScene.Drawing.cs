using PanguEngine.UI.Drawing;

namespace PanguEngine.UI;

public partial class UIScene
{
    /// <summary>
    /// Creates a new command list recording the current root subtree.
    /// </summary>
    /// <returns>The commands in stable drawing order.</returns>
    /// <remarks>
    /// An open scene must be laid out before its first draw and after its output size or scale changes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene is changing lifecycle or layout state, an update callback is running,
    /// command generation is reentered, or drawing code mutates UI state.
    /// Also thrown in Debug builds when an open scene is accessed from outside the host UI thread.
    /// </exception>
    public DrawCommandList CreateDrawCommandList()
    {
        var commands = new DrawCommandList();
        commands.Append(this);
        return commands;
    }

    internal void AppendDrawCommands(List<DrawCommand> commands)
    {
        BeginDrawing();
        try
        {
            var transformIndex = DrawingContext.PushTransform(commands, Point.Zero, Scale);
            Root?.AppendDrawCommands(commands, 1);
            DrawingContext.PopCommand(commands, transformIndex);
        }
        finally
        {
            EndDrawing();
        }
    }

    internal bool IsDrawing { get; private set; }

    internal void VerifyTreeMutationAccess()
    {
        if (IsOpen || _isClosing)
            VerifyOwnerThreadCore();
        VerifyTreeMutationStateCore();
    }

    internal void VerifyTreeMutationState() => VerifyTreeMutationStateCore();

    private void VerifyTreeMutationStateCore()
    {
        VerifyNotPreparingStyleSheets();
        if (IsDrawing)
        {
            throw new InvalidOperationException(
                "The UI scene tree cannot change while drawing commands are generated.");
        }
    }

    private void BeginDrawing()
    {
        if (IsOpen || _isClosing)
            VerifyOwnerThreadCore();
        if (_isTransitioning)
            throw new InvalidOperationException("The UI scene cannot draw during a lifecycle transition.");
        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI scene cannot draw while layout is updating.");
        if (IsUpdating)
            throw new InvalidOperationException("The UI scene cannot draw during an update callback.");
        if (IsDrawing)
            throw new InvalidOperationException("The UI scene is already generating drawing commands.");

        IsDrawing = true;
        _operationDepth++;
    }

    private void EndDrawing()
    {
        _operationDepth--;
        IsDrawing = false;
    }
}
