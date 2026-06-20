using System.IO.Compression;
using System.Text;
using PanguEngine.Registries;
using PanguEngine.Resources;

namespace PanguEngine.Tests.Resources;

public sealed class ResourceManagerTests
{
    [Fact]
    public void DirectorySourceMapsResourceKeyToNamespacePathUnderAssetsRoot()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "pangu", "shaders"));
        File.WriteAllText(Path.Combine(assetsRoot, "pangu", "shaders", "basic.vert"), "vertex", Encoding.UTF8);
        using var source = new DirectoryResourceSource(directory.Path);

        using var stream = source.Open("pangu/shaders/basic.vert");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal("vertex", reader.ReadToEnd());
    }

    [Fact]
    public void ZipSourceReadsAssetsFromAssetsRoot()
    {
        using var directory = TestDirectory.Create();
        var zipPath = Path.Combine(directory.Path, "pack.zip");
        using (var file = File.Create(zipPath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            WriteEntry(archive, "assets/pangu/textures/stone.txt", "stone");
        using var source = new ZipResourceSource(zipPath);

        using var stream = source.Open("pangu/textures/stone.txt");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal("stone", reader.ReadToEnd());
    }

    [Fact]
    public void ResourceManagerUsesFirstSourceForReads()
    {
        using var directory = TestDirectory.Create();
        var highRoot = Path.Combine(directory.Path, "high");
        var lowRoot = Path.Combine(directory.Path, "low");
        var highAssetsRoot = Path.Combine(highRoot, "assets");
        var lowAssetsRoot = Path.Combine(lowRoot, "assets");
        Directory.CreateDirectory(Path.Combine(highAssetsRoot, "pangu"));
        Directory.CreateDirectory(Path.Combine(lowAssetsRoot, "pangu"));
        File.WriteAllText(Path.Combine(highAssetsRoot, "pangu", "same.txt"), "high", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "same.txt"), "low", Encoding.UTF8);
        using var manager = new ResourceManager([
            new DirectoryResourceSource(highRoot),
            new DirectoryResourceSource(lowRoot)
        ]);

        var text = manager.ReadAllText("pangu/same.txt");

        Assert.Equal("high", text);
    }

    [Fact]
    public void ResourceManagerAcceptsRelativeResourcePath()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "shaders"));
        File.WriteAllText(Path.Combine(assetsRoot, "shaders", "basic.vert"), "vertex", Encoding.UTF8);
        using var manager = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.True(manager.Exists("shaders/basic.vert"));
        Assert.Equal("vertex", manager.ReadAllText("shaders/basic.vert"));
        using var stream = manager.Open("shaders/basic.vert");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("vertex", reader.ReadToEnd());
    }

    [Fact]
    public void ResourceManagerAcceptsSingleSegmentResourcePath()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllText(Path.Combine(assetsRoot, "basic.vert"), "vertex", Encoding.UTF8);
        using var manager = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.True(manager.Exists("basic.vert"));
        Assert.Equal("vertex", manager.ReadAllText("basic.vert"));
    }

    [Fact]
    public void ResourceManagerListDeduplicatesBySourcePriorityAndPreservesOrder()
    {
        using var directory = TestDirectory.Create();
        var highRoot = Path.Combine(directory.Path, "high");
        var lowRoot = Path.Combine(directory.Path, "low");
        var highAssetsRoot = Path.Combine(highRoot, "assets");
        var lowAssetsRoot = Path.Combine(lowRoot, "assets");
        Directory.CreateDirectory(Path.Combine(highAssetsRoot, "pangu", "blocks"));
        Directory.CreateDirectory(Path.Combine(lowAssetsRoot, "pangu", "blocks"));
        File.WriteAllText(Path.Combine(highAssetsRoot, "pangu", "blocks", "stone.txt"), "high", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "blocks", "dirt.txt"), "low", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "blocks", "stone.txt"), "low", Encoding.UTF8);
        using var manager = new ResourceManager([
            new DirectoryResourceSource(highRoot),
            new DirectoryResourceSource(lowRoot)
        ]);

        var resources = manager.List("pangu/blocks").ToArray();
        var keys = resources.Select(resource => resource.Path).ToArray();

        Assert.Equal(["pangu/blocks/stone.txt", "pangu/blocks/dirt.txt"], keys);
        Assert.Equal("high", resources.Single(resource => resource.Path == "pangu/blocks/stone.txt").ReadAllText());
    }

    [Fact]
    public void ResourceManagerGetResourceStackReturnsResourcesBySourcePriority()
    {
        using var directory = TestDirectory.Create();
        var highRoot = Path.Combine(directory.Path, "high");
        var lowRoot = Path.Combine(directory.Path, "low");
        var highAssetsRoot = Path.Combine(highRoot, "assets");
        var lowAssetsRoot = Path.Combine(lowRoot, "assets");
        Directory.CreateDirectory(Path.Combine(highAssetsRoot, "pangu"));
        Directory.CreateDirectory(Path.Combine(lowAssetsRoot, "pangu"));
        File.WriteAllText(Path.Combine(highAssetsRoot, "pangu", "same.txt"), "high", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "same.txt"), "low", Encoding.UTF8);
        using var manager = new ResourceManager([
            new DirectoryResourceSource(highRoot),
            new DirectoryResourceSource(lowRoot)
        ]);

        var stack = manager.GetResourceStack("pangu/same.txt");

        Assert.Equal(["high", "low"], stack.Select(resource => resource.ReadAllText()).ToArray());
    }

    [Fact]
    public void ResourceManagerGetResourceStackReturnsEmptyForMissingResource()
    {
        using var manager = new ResourceManager([]);

        var stack = manager.GetResourceStack("pangu/missing.txt");

        Assert.Empty(stack);
    }

    [Fact]
    public void ResourceManagerListResourceStacksGroupsResourcesByPath()
    {
        using var directory = TestDirectory.Create();
        var highRoot = Path.Combine(directory.Path, "high");
        var lowRoot = Path.Combine(directory.Path, "low");
        var highAssetsRoot = Path.Combine(highRoot, "assets");
        var lowAssetsRoot = Path.Combine(lowRoot, "assets");
        Directory.CreateDirectory(Path.Combine(highAssetsRoot, "pangu", "blocks"));
        Directory.CreateDirectory(Path.Combine(lowAssetsRoot, "pangu", "blocks"));
        File.WriteAllText(Path.Combine(highAssetsRoot, "pangu", "blocks", "stone.txt"), "high stone", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "blocks", "stone.txt"), "low stone", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "blocks", "dirt.txt"), "low dirt", Encoding.UTF8);
        using var manager = new ResourceManager([
            new DirectoryResourceSource(highRoot),
            new DirectoryResourceSource(lowRoot)
        ]);

        var stacks = manager.ListResourceStacks("pangu/blocks");

        Assert.Equal(["high stone", "low stone"], stacks["pangu/blocks/stone.txt"]
            .Select(resource => resource.ReadAllText())
            .ToArray());
        Assert.Equal(["low dirt"], stacks["pangu/blocks/dirt.txt"]
            .Select(resource => resource.ReadAllText())
            .ToArray());
    }

    [Fact]
    public void ResourceKeepsBoundSourceAfterManagerSourcesChange()
    {
        using var directory = TestDirectory.Create();
        var highRoot = Path.Combine(directory.Path, "high");
        var lowRoot = Path.Combine(directory.Path, "low");
        var highAssetsRoot = Path.Combine(highRoot, "assets");
        var lowAssetsRoot = Path.Combine(lowRoot, "assets");
        Directory.CreateDirectory(Path.Combine(highAssetsRoot, "pangu"));
        Directory.CreateDirectory(Path.Combine(lowAssetsRoot, "pangu"));
        File.WriteAllText(Path.Combine(highAssetsRoot, "pangu", "same.txt"), "high", Encoding.UTF8);
        File.WriteAllText(Path.Combine(lowAssetsRoot, "pangu", "same.txt"), "low", Encoding.UTF8);
        var lowSource = new DirectoryResourceSource(lowRoot);
        using var manager = new ResourceManager([lowSource]);
        var resource = manager.GetResource("pangu/same.txt");

        manager.SetSources([new DirectoryResourceSource(highRoot), lowSource]);

        Assert.Equal("low", resource.ReadAllText());
        Assert.Equal("high", manager.ReadAllText("pangu/same.txt"));
    }

    [Fact]
    public void ResourceReadsTextAndBytesFromBoundSource()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "pangu"));
        var expected = Encoding.UTF8.GetBytes("text");
        File.WriteAllBytes(Path.Combine(assetsRoot, "pangu", "text.txt"), expected);
        using var manager = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        var resource = manager.GetResource("pangu/text.txt");

        Assert.Equal("text", resource.ReadAllText());
        Assert.Equal(expected, resource.ReadAllBytes());
    }

    [Fact]
    public void SetSourcesReplacesResourceSourceOrder()
    {
        using var directory = TestDirectory.Create();
        var firstRoot = Path.Combine(directory.Path, "first");
        var secondRoot = Path.Combine(directory.Path, "second");
        var firstAssetsRoot = Path.Combine(firstRoot, "assets");
        var secondAssetsRoot = Path.Combine(secondRoot, "assets");
        Directory.CreateDirectory(Path.Combine(firstAssetsRoot, "pangu"));
        Directory.CreateDirectory(Path.Combine(secondAssetsRoot, "pangu"));
        File.WriteAllText(Path.Combine(firstAssetsRoot, "pangu", "same.txt"), "first", Encoding.UTF8);
        File.WriteAllText(Path.Combine(secondAssetsRoot, "pangu", "same.txt"), "second", Encoding.UTF8);
        using var manager = new ResourceManager([new DirectoryResourceSource(firstRoot)]);

        manager.SetSources([new DirectoryResourceSource(secondRoot)]);

        Assert.Equal("second", manager.ReadAllText("pangu/same.txt"));
        Assert.Single(manager.Sources);
    }

    [Fact]
    public void SourceListSupportsDirectAndRecursiveFilesOnly()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "pangu", "blocks", "nested"));
        File.WriteAllText(Path.Combine(assetsRoot, "pangu", "blocks", "stone.txt"), "stone", Encoding.UTF8);
        File.WriteAllText(Path.Combine(assetsRoot, "pangu", "blocks", "nested", "dirt.txt"), "dirt", Encoding.UTF8);
        using var source = new DirectoryResourceSource(directory.Path);
        var direct = source.List("pangu/blocks").Select(resource => resource.Path).Order(StringComparer.Ordinal)
            .ToArray();
        var recursive = source.List("pangu/blocks", recursive: true)
            .Select(resource => resource.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["pangu/blocks/stone.txt"], direct);
        Assert.Equal(["pangu/blocks/nested/dirt.txt", "pangu/blocks/stone.txt"], recursive);
    }

    [Fact]
    public void SourceListSupportsSingleSegmentDirectoryPath()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "pangu", "nested"));
        File.WriteAllText(Path.Combine(assetsRoot, "pangu", "root.txt"), "root", Encoding.UTF8);
        File.WriteAllText(Path.Combine(assetsRoot, "pangu", "nested", "child.txt"), "child", Encoding.UTF8);
        using var source = new DirectoryResourceSource(directory.Path);

        var direct = source.List("pangu").Select(resource => resource.Path).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["pangu/root.txt"], direct);
    }

    [Fact]
    public void ResourceManagerOpenMissingResourceThrowsFileNotFoundException()
    {
        using var directory = TestDirectory.Create();
        using var manager = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        Assert.False(manager.Exists(ResourceKey.Create("pangu", "missing.txt")));
        Assert.Throws<FileNotFoundException>(() => manager.Open(ResourceKey.Create("pangu", "missing.txt")));
    }

    [Fact]
    public void ReadAllBytesReturnsBinaryAssetContent()
    {
        using var directory = TestDirectory.Create();
        var assetsRoot = Path.Combine(directory.Path, "assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "pangu", "binary"));
        var expected = new byte[] { 0, 1, 2, 128, 255 };
        File.WriteAllBytes(Path.Combine(assetsRoot, "pangu", "binary", "data.bin"), expected);
        using var manager = new ResourceManager([new DirectoryResourceSource(directory.Path)]);

        var bytes = manager.ReadAllBytes("pangu/binary/data.bin");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void ZipResourceSourceClosesFileWhenArchiveOpenFails()
    {
        using var directory = TestDirectory.Create();
        var zipPath = Path.Combine(directory.Path, "invalid.zip");
        File.WriteAllText(zipPath, "not a zip", Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => new ZipResourceSource(zipPath));
        File.Delete(zipPath);

        Assert.False(File.Exists(zipPath));
    }

    [Fact]
    public void ZipResourceSourceDoesNotDisposeExternalArchive()
    {
        using var directory = TestDirectory.Create();
        var zipPath = Path.Combine(directory.Path, "pack.zip");
        using (var file = File.Create(zipPath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            WriteEntry(archive, "assets/pangu/text.txt", "text");
        using var stream = File.OpenRead(zipPath);
        using var archiveSource = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        using var source = new ZipResourceSource(archiveSource);

        source.Dispose();

        Assert.NotNull(archiveSource.GetEntry("assets/pangu/text.txt"));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
        }

        public string Path { get; }

        public static TestDirectory Create() =>
            new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}