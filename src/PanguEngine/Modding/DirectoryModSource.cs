using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Modding;

/// <summary>
/// Reads mod files from a directory.
/// </summary>
/// <param name="path">The directory path.</param>
internal sealed class DirectoryModSource(string path) : ModSource(path)
{
    /// <inheritdoc />
    public override bool Exists(string path) => File.Exists(GetFullPath(path));

    /// <inheritdoc />
    public override Stream Open(string path) => File.OpenRead(GetFullPath(path));

    /// <inheritdoc />
    public override IEnumerable<string> GetAssemblyFileNames()
    {
        return Directory.EnumerateFiles(SourcePath, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>();
    }

    /// <inheritdoc />
    public override bool TryGetFilePath(string path, [NotNullWhen(true)] out string? filePath)
    {
        filePath = GetFullPath(path);
        return File.Exists(filePath);
    }

    private string GetFullPath(string path)
    {
        return Path.GetFullPath(Path.Combine(SourcePath, path));
    }
}