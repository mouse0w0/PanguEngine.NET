using System.Collections.ObjectModel;

namespace PanguEngine.Desktop;

/// <summary>
/// Describes a named set of file extensions shown by a file dialog.
/// </summary>
public sealed class FileDialogFilter
{
    /// <summary>
    /// Gets the user-visible filter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the file extensions accepted by this filter.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Creates a named file dialog filter.
    /// </summary>
    /// <param name="name">The user-visible filter name.</param>
    /// <param name="extensions">
    /// Bare file extensions such as <c>png</c> or <c>tar.gz</c>, or a single <c>*</c> for all files.
    /// </param>
    public FileDialogFilter(string name, params string[] extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(extensions);
        if (extensions.Length == 0)
            throw new ArgumentException("At least one file extension is required.", nameof(extensions));
        if (extensions.Contains("*") && extensions is not ["*"])
            throw new ArgumentException("The all-files pattern must be the only extension.", nameof(extensions));

        foreach (var extension in extensions)
        {
            if (!IsValidExtension(extension))
                throw new ArgumentException($"'{extension}' is not a valid bare file extension.", nameof(extensions));
        }

        Name = name;
        Extensions = new ReadOnlyCollection<string>(extensions.ToArray());
    }

    private static bool IsValidExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;
        if (extension == "*")
            return true;
        if (extension[0] == '.')
            return false;

        foreach (var character in extension)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')
                return false;
        }

        return true;
    }
}
