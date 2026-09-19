using System.Collections;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// Collects ordered drawing commands and balanced state scopes from UI screens.
/// </summary>
/// <remarks>
/// Coordinates on drawing commands are local to their enclosing transform scopes.
/// Consumers must interpret state commands in order to obtain final bounds, clips, and opacity.
/// Append and Clear require the creating thread. Callers must finish recording before consumption
/// and must not modify this list while it is being enumerated or used to build geometry.
/// </remarks>
public sealed class UiDrawCommandList : IReadOnlyList<UiDrawCommand>
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly List<UiDrawCommand> _commands;
    private bool _isAppending;

    /// <summary>
    /// Creates an empty command list that can collect complete screen drawing scopes.
    /// </summary>
    public UiDrawCommandList()
        : this([])
    {
    }

    internal UiDrawCommandList(List<UiDrawCommand> commands)
    {
        _commands = commands;
    }

    /// <summary>
    /// Appends a screen in an isolated drawing scope.
    /// </summary>
    /// <param name="screen">The screen whose current root subtree is recorded.</param>
    /// <remarks>If screen recording fails, all commands currently in this list are discarded.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when the screen is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown on the wrong thread, during another append, or when the screen cannot draw.
    /// </exception>
    public void Append(UiScreen screen)
    {
        VerifyMutationAccess();
        ArgumentNullException.ThrowIfNull(screen);
        _isAppending = true;
        try
        {
            screen.AppendDrawCommands(_commands);
        }
        catch
        {
            _commands.Clear();
            throw;
        }
        finally
        {
            _isAppending = false;
        }
    }

    /// <summary>
    /// Clears the list so it can be reused for a new recording.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown on the wrong thread or during an append.</exception>
    public void Clear()
    {
        VerifyMutationAccess();
        _commands.Clear();
    }

    /// <summary>
    /// Gets the number of drawing and state commands in the list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown during an append.</exception>
    public int Count
    {
        get
        {
            VerifyNotAppending();
            return _commands.Count;
        }
    }

    /// <summary>
    /// Gets the command at an index.
    /// </summary>
    /// <param name="index">The zero-based command index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside this list.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown during an append.</exception>
    public UiDrawCommand this[int index]
    {
        get
        {
            VerifyNotAppending();
            return _commands[index];
        }
    }

    /// <summary>
    /// Returns an enumerator over commands in drawing order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown during an append.</exception>
    public IEnumerator<UiDrawCommand> GetEnumerator()
    {
        VerifyNotAppending();
        return _commands.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void VerifyMutationAccess()
    {
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI command collection requires its owner thread.");
        VerifyNotAppending();
    }

    private void VerifyNotAppending()
    {
        if (_isAppending)
            throw new InvalidOperationException("The UI command list is recording a screen.");
    }
}
