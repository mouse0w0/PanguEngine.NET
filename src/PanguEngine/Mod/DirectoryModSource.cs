namespace PanguEngine.Mod;

internal sealed class DirectoryModSource(string path) : ModSource(path)
{
    public override bool Exists(string path) => File.Exists(GetFullPath(path));

    public override Stream Open(string path) => File.OpenRead(GetFullPath(path));

    public override IEnumerable<string> GetAssemblyFileNames()
    {
        return Directory.EnumerateFiles(SourcePath, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>();
    }

    private string GetFullPath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(SourcePath, path.Replace('/', Path.DirectorySeparatorChar)));
        var rootPath = Path.GetFullPath(SourcePath);
        var relativePath = Path.GetRelativePath(rootPath, fullPath);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new ArgumentException("Path must stay within the mod directory.", nameof(path));

        return fullPath;
    }
}