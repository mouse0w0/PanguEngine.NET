using System.IO.Compression;

namespace PanguEngine.Mod;

internal sealed class ZipModSource(string path) : ModSource(path)
{
    public ZipArchive Archive { get; } = new(File.OpenRead(path), ZipArchiveMode.Read, leaveOpen: false);

    public override bool Exists(string path) => Archive.GetEntry(path) is not null;

    public override Stream Open(string path)
    {
        var entry = Archive.GetEntry(path)
                    ?? throw new FileNotFoundException($"Mod package entry '{path}' was not found.", path);
        return entry.Open();
    }

    public override IEnumerable<string> GetAssemblyFileNames()
    {
        return Archive.Entries
            .Where(entry => !entry.FullName.Contains('/') &&
                            entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName);
    }

    public override void Dispose()
    {
        Archive.Dispose();
        base.Dispose();
    }
}