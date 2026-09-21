namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides shared ASCII identifier validation for style class, id and type names.
/// </summary>
internal static class UiStyleIdentifier
{
    /// <summary>
    /// Determines whether the supplied value is a valid ASCII identifier.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>true when the value is a non-empty ASCII identifier; otherwise false.</returns>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        if (!IsStartCharacter(value[0]))
            return false;
        for (var i = 1; i < value.Length; i++)
        {
            if (!IsPartCharacter(value[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Throws when the supplied value is not a valid ASCII identifier.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name used in the exception.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a valid ASCII identifier.</exception>
    public static void ThrowIfInvalid(string? value, string? parameterName)
    {
        if (!IsValid(value))
            throw new ArgumentException("A style identifier must be a non-empty ASCII identifier.", parameterName);
    }

    private static bool IsStartCharacter(char c) =>
        char.IsAsciiLetter(c) || c == '_' || c == '-';

    private static bool IsPartCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
}
