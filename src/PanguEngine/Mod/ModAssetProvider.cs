namespace PanguEngine.Mod;

public sealed class ModAssetProvider
{
    private readonly ModSource _source;

    internal ModAssetProvider(ModSource source)
    {
        _source = source;
    }

    public Stream Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Asset path cannot be empty.", nameof(path));

        var normalized = NormalizeAssetPath(path);
        var packagePath = $"assets/{normalized}";
        return _source.Exists(packagePath)
            ? _source.Open(packagePath)
            : throw new FileNotFoundException($"Asset '{path}' was not found.", path);
    }

    private static string NormalizeAssetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains("../") || normalized == ".." ||
            normalized.StartsWith("../", StringComparison.Ordinal))
            throw new ArgumentException("Asset path must be relative to the assets directory.", nameof(path));

        return normalized;
    }
}