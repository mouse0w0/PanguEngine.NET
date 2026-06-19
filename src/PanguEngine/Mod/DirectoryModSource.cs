using System.Diagnostics.CodeAnalysis;

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