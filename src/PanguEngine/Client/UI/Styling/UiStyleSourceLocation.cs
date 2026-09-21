namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Describes an immutable source span for a parsed style declaration.
/// </summary>
public sealed class UiStyleSourceLocation
{
    /// <summary>Initializes a source location.</summary>
    /// <param name="sourceName">The optional source name of the style sheet.</param>
    /// <param name="line">The 1-based line of the span start.</param>
    /// <param name="column">The 1-based column of the span start.</param>
    /// <param name="length">The UTF-16 code unit length of the span.</param>
    public UiStyleSourceLocation(string? sourceName, int line, int column, int length)
    {
        SourceName = sourceName;
        Line = line;
        Column = column;
        Length = length;
    }

    /// <summary>Gets the optional source name of the style sheet.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the 1-based line of the span start.</summary>
    public int Line { get; }

    /// <summary>Gets the 1-based column of the span start.</summary>
    public int Column { get; }

    /// <summary>Gets the UTF-16 code unit length of the span.</summary>
    public int Length { get; }
}
