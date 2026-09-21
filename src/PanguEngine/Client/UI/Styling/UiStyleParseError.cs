namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Identifies the category of a CSS syntax or property binding failure.
/// </summary>
public enum UiStyleParseError
{
    /// <summary>The input was not valid UTF-8.</summary>
    InvalidEncoding,

    /// <summary>The input violated the minimum grammar.</summary>
    InvalidSyntax,

    /// <summary>A block comment was not terminated before the end of input.</summary>
    UnterminatedComment,

    /// <summary>A selector referenced a pseudo state that is not built in.</summary>
    UnknownPseudoState,

    /// <summary>A selector specified more than one id.</summary>
    DuplicateId,

    /// <summary>A declaration value could not be converted to the property type.</summary>
    InvalidValue,

    /// <summary>A declaration value was not terminated by a semicolon.</summary>
    MissingSemicolon,

    /// <summary>Input remained after a complete style sheet.</summary>
    TrailingToken
}
