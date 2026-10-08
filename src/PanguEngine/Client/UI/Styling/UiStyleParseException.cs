namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Reports a CSS syntax, property binding, or value validation failure with an error code and 1-based source span.
/// </summary>
public sealed class UiStyleParseException : Exception
{
    internal UiStyleParseException(
        UiStyleParseError error,
        string? sourceName,
        int line,
        int column,
        int length,
        Exception? innerException = null,
        string? detail = null,
        string? propertyName = null)
        : base(FormatMessage(error, sourceName, line, column, detail, propertyName), innerException)
    {
        Error = error;
        SourceName = sourceName;
        Line = line;
        Column = column;
        Length = length;
    }

    /// <summary>Gets the stable category of the parse failure.</summary>
    public UiStyleParseError Error { get; }

    /// <summary>Gets the optional source name supplied to the parser.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the 1-based line of the failure.</summary>
    public int Line { get; }

    /// <summary>Gets the 1-based column of the failure.</summary>
    public int Column { get; }

    /// <summary>Gets the UTF-16 code unit length of the failing span.</summary>
    public int Length { get; }

    private static string FormatMessage(UiStyleParseError error, string? sourceName, int line, int column,
        string? detail, string? propertyName)
    {
        var location = sourceName is null
            ? $"({line}:{column})"
            : $"{sourceName}({line}:{column})";
        var message = propertyName is null
            ? $"Failed to parse style: {error} at {location}."
            : $"Failed to apply style property '{propertyName}': {error} at {location}.";
        return detail is null ? message : $"{message}{Environment.NewLine}{detail}";
    }
}
