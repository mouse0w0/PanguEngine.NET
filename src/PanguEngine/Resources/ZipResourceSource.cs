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
        _archive = OpenArchive(path);
        _ownsArchive = true;
    }

    /// <summary>
    /// Creates a resource source from an existing zip archive.
    /// </summary>
    /// <param name="archive">The archive that contains package resources.</param>
    internal ZipResourceSource(ZipArchive archive)
    {
        _archive = archive;
    }

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

    private static ZipArchive OpenArchive(string path)
    {
        var stream = File.OpenRead(path);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}