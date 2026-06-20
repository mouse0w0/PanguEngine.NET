namespace PanguEngine.Modding;

/// <summary>
/// Represents an error raised while loading or configuring mods.
/// </summary>
public sealed class ModLoadException : Exception
{
    /// <summary>
    /// Initializes a mod load exception with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ModLoadException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a mod load exception with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ModLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}