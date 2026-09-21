namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides an immutable, ordered snapshot of style rules created from C# or parsed CSS text.
/// </summary>
public sealed class UiStyleSheet
{
    /// <summary>Initializes a style sheet from an ordered set of rules.</summary>
    /// <param name="rules">The rules in declaration order.</param>
    /// <param name="sourceName">The optional source name of the sheet.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
    public UiStyleSheet(IEnumerable<UiStyleRule> rules, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = Array.AsReadOnly(rules.ToArray());
        SourceName = sourceName;
    }

    /// <summary>Gets the rules in declaration order.</summary>
    public IReadOnlyList<UiStyleRule> Rules { get; }

    /// <summary>Gets the optional source name of the sheet.</summary>
    public string? SourceName { get; }

    /// <summary>Parses CSS syntax into an immutable style sheet whose values are bound to matching node types.</summary>
    /// <param name="text">The CSS source text.</param>
    /// <param name="sourceName">The optional source name recorded on the sheet and diagnostics.</param>
    /// <returns>An immutable style sheet built from the parsed rules.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="UiStyleParseException">Thrown when the text violates the CSS grammar.</exception>
    public static UiStyleSheet Parse(string text, string? sourceName = null)
        => UiStyleParser.Parse(text, sourceName);

    /// <summary>Parses CSS syntax from a stream into an immutable style sheet awaiting matching node types.</summary>
    /// <param name="stream">The stream of UTF-8 CSS bytes, read from the current position to the end.</param>
    /// <param name="sourceName">The optional source name recorded on the sheet and diagnostics.</param>
    /// <returns>An immutable style sheet built from the parsed rules.</returns>
    /// <remarks>The stream is decoded with strict UTF-8, accepts a leading BOM, and is never closed or rewound by the parser.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="UiStyleParseException">Thrown when the text violates the CSS grammar or is not valid UTF-8.</exception>
    /// <exception cref="IOException">Thrown when reading the stream fails.</exception>
    public static UiStyleSheet Parse(Stream stream, string? sourceName = null)
        => UiStyleParser.Parse(stream, sourceName);
}
