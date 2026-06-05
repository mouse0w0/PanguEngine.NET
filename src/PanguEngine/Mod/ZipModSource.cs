using System.IO.Compression;

namespace PanguEngine.Mod;

internal sealed class ZipModSource(string path) : ModSource(path)
{
    private readonly ZipArchive _archive = new(File.OpenRead(path), ZipArchiveMode.Read, leaveOpen: false);

    public override bool Exists(string path) => _archive.GetEntry(NormalizePath(path)) is not null;

    public override Stream Open(string path)
    {
        var normalized = NormalizePath(path);
        var entry = _archive.GetEntry(normalized)
                    ?? throw new FileNotFoundException($"Mod package entry '{path}' was not found.", path);
        return entry.Open();
    }

    public override IEnumerable<string> GetAssemblyFileNames()
    {
        return _archive.Entries
            .Where(entry => !entry.FullName.Contains('/') &&
                            entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName);
    }

    public override void Dispose()
    {
        _archive.Dispose();
        base.Dispose();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized == ".." ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.Contains("/../", StringComparison.Ordinal))
            throw new ArgumentException("Path must stay within the mod package.", nameof(path));

        return normalized;
    }
}