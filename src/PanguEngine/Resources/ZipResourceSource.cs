using System.Collections.ObjectModel;
using System.IO.Compression;

namespace PanguEngine.Resources;

/// <summary>
/// Provides resources from a zip resource package.
/// </summary>
public sealed class ZipResourceSource : IResourceSource
{
    private readonly ZipArchive _archive;
    private readonly bool _ownsArchive;

    /// <summary>
    /// Creates a resource source from a zip file.
    /// </summary>
    /// <param name="path">The zip file path.</param>
    public ZipResourceSource(string path)
    {
        _archive = ZipFile.OpenRead(path);
        _ownsArchive = true;
        try
        {
            Namespaces = GetNamespaces(_archive);
        }
        catch
        {
            _archive.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a resource source from an existing zip archive.
    /// </summary>
    /// <param name="archive">The archive that contains package resources.</param>
    internal ZipResourceSource(ZipArchive archive)
    {
        _archive = archive;
        Namespaces = GetNamespaces(_archive);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Namespaces { get; }

    /// <inheritdoc/>
    public bool Exists(string resourcePath)
    {
        return _archive.GetEntry(ResourcePath.ToPackagePath(resourcePath)) is { Name.Length: > 0 };
    }

    /// <inheritdoc/>
    public Resource GetResource(string resourcePath)
    {
        var entryName = ResourcePath.ToPackagePath(resourcePath);
        var entry = _archive.GetEntry(entryName);
        if (entry is null || entry.Name.Length == 0)
            throw new FileNotFoundException($"Resource '{resourcePath}' was not found.", resourcePath);

        return new Resource(resourcePath, this, entry.Open);
    }

    /// <inheritdoc/>
    public Stream Open(string resourcePath)
    {
        return GetResource(resourcePath).Open();
    }

    /// <inheritdoc/>
    public IEnumerable<Resource> List(string directoryPath, bool recursive = false)
    {
        var prefix = ResourcePath.ToPackagePath(directoryPath) + "/";
        return _archive.Entries
            .Where(entry => entry.Name.Length > 0)
            .Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
            .Where(entry => recursive || !entry.FullName[prefix.Length..].Contains('/', StringComparison.Ordinal))
            .Select(entry => new Resource(entry.FullName["assets/".Length..], this, entry.Open))
            .ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsArchive)
            _archive.Dispose();
    }

    private static string? GetNamespace(string entryPath)
    {
        const string prefix = "assets/";
        if (!entryPath.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var relativePath = entryPath.AsSpan(prefix.Length);
        var separator = relativePath.IndexOf('/');
        return separator > 0 ? relativePath[..separator].ToString() : null;
    }

    private static ReadOnlyCollection<string> GetNamespaces(ZipArchive archive)
    {
        return Array.AsReadOnly(archive.Entries
            .Select(entry => GetNamespace(entry.FullName))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }
}
