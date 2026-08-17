using System.Collections;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides an immutable snapshot of ordered UI drawing commands.
/// </summary>
public sealed class UiDrawCommandList : IReadOnlyList<UiDrawCommand>
{
    private readonly UiDrawCommand[] _commands;

    internal UiDrawCommandList(List<UiDrawCommand> commands, double scale)
    {
        _commands = [.. commands];
        Scale = scale;
    }

    internal double Scale { get; }

    /// <summary>
    /// Gets the number of commands in this snapshot.
    /// </summary>
    public int Count => _commands.Length;

    /// <summary>
    /// Gets the command at an index.
    /// </summary>
    /// <param name="index">The zero-based command index.</param>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside this snapshot.
    /// </exception>
    public UiDrawCommand this[int index] => _commands[index];

    /// <summary>
    /// Returns an enumerator over commands in drawing order.
    /// </summary>
    public IEnumerator<UiDrawCommand> GetEnumerator() =>
        ((IEnumerable<UiDrawCommand>)_commands).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _commands.GetEnumerator();
}
