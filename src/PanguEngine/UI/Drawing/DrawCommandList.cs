using System.Collections;

namespace PanguEngine.UI.Drawing;

/// <summary>
/// Collects ordered drawing commands and balanced state scopes from UI scenes.
/// </summary>
/// <remarks>
/// Coordinates on drawing commands are local to their enclosing transform scopes.
/// Consumers must interpret state commands in order to obtain final bounds, clips, and opacity.
/// Callers must finish recording before consumption and synchronize any handoff between threads.
/// This list must not be modified while it is being enumerated or used to build geometry.
/// </remarks>
public sealed class DrawCommandList : IReadOnlyList<DrawCommand>
{
    private readonly List<DrawCommand> _commands;
    private bool _isAppending;

    /// <summary>
    /// Creates an empty command list that can collect complete scene drawing scopes.
    /// </summary>
    public DrawCommandList()
        : this([])
    {
    }

    internal DrawCommandList(List<DrawCommand> commands)
    {
        _commands = commands;
    }

    /// <summary>
    /// Appends a scene in an isolated drawing scope.
    /// </summary>
    /// <param name="scene">The scene whose current root subtree is recorded.</param>
    /// <remarks>If scene recording fails, all commands currently in this list are discarded.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when the scene is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown during another append, when the scene cannot draw, or when a Debug scene thread diagnostic rejects access.
    /// </exception>
    public void Append(UIScene scene)
    {
        VerifyNotAppending();
        ArgumentNullException.ThrowIfNull(scene);
        _isAppending = true;
        try
        {
            scene.AppendDrawCommands(_commands);
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
    /// <exception cref="InvalidOperationException">Thrown during an append.</exception>
    public void Clear()
    {
        VerifyNotAppending();
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
    public DrawCommand this[int index]
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
    public IEnumerator<DrawCommand> GetEnumerator()
    {
        VerifyNotAppending();
        return _commands.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void VerifyNotAppending()
    {
        if (_isAppending)
            throw new InvalidOperationException("The UI command list is recording a scene.");
    }
}
