namespace PanguEngine.Registry;

/// <summary>
/// Identifies a named engine resource using a namespace and path.
/// </summary>
public readonly record struct ResourceKey
{
    /// <summary>The namespace portion of the key.</summary>
    public string Namespace { get; }

    /// <summary>The path portion of the key.</summary>
    public string Path { get; }

    /// <summary>
    /// Creates a resource key from a namespace and path.
    /// </summary>
    /// <param name="namespace">The namespace portion of the key.</param>
    /// <param name="path">The path portion of the key.</param>
    public ResourceKey(string @namespace, string path)
    {
        ValidateNamespace(@namespace);
        ValidatePath(path);
        Namespace = @namespace;
        Path = path;
    }

    /// <summary>
    /// Parses a resource key from its string representation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed resource key.</returns>
    public static ResourceKey Parse(string text)
    {
        return TryParse(text, out var key)
            ? key
            : throw new FormatException($"Invalid resource key '{text}'.");
    }

    /// <summary>
    /// Attempts to parse a resource key from its string representation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="key">The parsed resource key when parsing succeeds.</param>
    /// <returns>Whether parsing succeeded.</returns>
    public static bool TryParse(string? text, out ResourceKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(text)) return false;

        var separator = text.IndexOf(':');
        if (separator <= 0 || separator != text.LastIndexOf(':') || separator == text.Length - 1)
            return false;

        var ns = text[..separator];
        var path = text[(separator + 1)..];
        if (!IsValidNamespace(ns) || !IsValidPath(path))
            return false;

        key = new ResourceKey(ns, path);
        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Namespace}:{Path}";

    /// <summary>
    /// Gets whether the resource key is valid.
    /// </summary>
    /// <param name="key">The key to inspect.</param>
    /// <returns>Whether the key is valid.</returns>
    internal static bool IsValid(ResourceKey key) =>
        IsValidNamespace(key.Namespace) && IsValidPath(key.Path);

    private static void ValidateNamespace(string value)
    {
        if (!IsValidNamespace(value))
            throw new ArgumentException("Invalid resource namespace.", nameof(value));
    }

    private static void ValidatePath(string value)
    {
        if (!IsValidPath(value))
            throw new ArgumentException("Invalid resource path.", nameof(value));
    }

    private static bool IsValidNamespace(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (var c in value)
        {
            if (!IsNamespaceChar(c)) return false;
        }

        return true;
    }

    private static bool IsValidPath(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (var c in value)
        {
            if (!IsPathChar(c)) return false;
        }

        return true;
    }

    private static bool IsNamespaceChar(char c) =>
        c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.';

    private static bool IsPathChar(char c) =>
        IsNamespaceChar(c) || c == '/';
}