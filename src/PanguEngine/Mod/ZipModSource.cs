using System.IO.Compression;

namespace PanguEngine.Mod;

/// <summary>
/// Reads mod files from a zip archive.
/// </summary>
/// <param name="path">The zip archive path.</param>
internal sealed class ZipModSource(string path) : ModSource(path)
{
    /// <summary>
    /// Gets the opened zip archive.
    /// </summary>
    public ZipArchive Archive { get; } = new(File.OpenRead(path), ZipArchiveMode.Read, leaveOpen: false);

    /// <inheritdoc />
    public override bool Exists(string path) => Archive.GetEntry(path) is not null;

    /// <inheritdoc />
    public override Stream Open(string path)
    {
        var entry = Archive.GetEntry(path)
                    ?? throw new FileNotFoundException($"Mod package entry '{path}' was not found.", path);
        return entry.Open();
    }

    /// <inheritdoc />
    public override IEnumerable<string> GetAssemblyFileNames()
    {
        return Archive.Entries
            .Where(entry => !entry.FullName.Contains('/') &&
                            entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Archive.Dispose();
        base.Dispose();
    }
}