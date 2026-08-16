namespace PanguEngine.Resources;

/// <summary>
/// Provides resources from a directory resource root.
/// </summary>
/// <param name="packageRoot">The package root directory.</param>
public sealed class DirectoryResourceSource(string packageRoot) : IResourceSource
{
    private readonly string _packageRoot = Path.GetFullPath(packageRoot);

    /// <inheritdoc/>
    public IReadOnlyList<string> Namespaces { get; } = GetNamespaces(Path.GetFullPath(packageRoot));

    /// <inheritdoc/>
    public bool Exists(string resourcePath)
    {
        return File.Exists(GetFullPath(resourcePath));
    }

    /// <inheritdoc/>
    public Resource GetResource(string resourcePath)
    {
        var fullPath = GetFullPath(resourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource '{resourcePath}' was not found.", resourcePath);

        return new Resource(resourcePath, this, () => File.OpenRead(fullPath));
    }

    /// <inheritdoc/>
    public Stream Open(string resourcePath)
    {
        return File.OpenRead(GetFullPath(resourcePath));
    }

    /// <inheritdoc/>
    public IEnumerable<Resource> List(string directoryPath, bool recursive = false)
    {
        var fullPath = GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
            return [];

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(fullPath, "*", searchOption)
            .Select(path => new Resource(Path.GetRelativePath(Path.Combine(_packageRoot, "assets"), path)
                    .Replace(Path.DirectorySeparatorChar, '/'),
                this, () => File.OpenRead(path)))
            .ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private string GetFullPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(_packageRoot, ResourcePath.ToPackagePath(relativePath)));
    }

    private static IReadOnlyList<string> GetNamespaces(string packageRoot)
    {
        var assetsRoot = Path.Combine(packageRoot, "assets");
        return Directory.Exists(assetsRoot)
            ? Array.AsReadOnly(Directory.EnumerateDirectories(assetsRoot)
                .Select(path => new DirectoryInfo(path).Name)
                .Order(StringComparer.Ordinal)
                .ToArray())
            : Array.Empty<string>();
    }
}
