using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Mod;

namespace PanguEngine.Tests.Mod;

public sealed class ModManagerTests
{
    [Fact]
    public void LoadTreatsMissingModsDirectoryAsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var manager = new ModManager(path, NullLogger.Instance);

        manager.Load();

        Assert.Empty(manager.LoadedMods);
    }

    [Fact]
    public void LoadUsesExplicitModWhenModsDirectoryIsMissing()
    {
        using var directory = TestDirectory.Create();
        var modsDirectory = Path.Combine(directory.Path, "MissingMods");
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        var modDirectory = CreateModDirectory(directory.Path, "explicit_mod", "test_mod", assemblyFile,
            typeof(TestModEntry).FullName!);
        File.Copy(assemblyPath, Path.Combine(modDirectory, assemblyFile), overwrite: true);
        Directory.CreateDirectory(Path.Combine(modDirectory, "assets", "textures"));
        File.WriteAllText(Path.Combine(modDirectory, "assets", "textures", "stone.txt"), "stone");

        var manager = new ModManager(modsDirectory, NullLogger.Instance, [modDirectory]);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.Equal("test_mod", mod.Id);
            Assert.Equal("0.1.0", mod.Version);
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadReportsAllDuplicateIdsBeforeLoadingAssemblies()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "a.zip", "same_mod", "A.dll", "A.Entry");
        CreateModZip(directory.Path, "b.zip", "same_mod", "B.dll", "B.Entry");
        CreateModZip(directory.Path, "c.zip", "other_mod", "C.dll", "C.Entry");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("same_mod", exception.Message);
        Assert.Contains("a.zip", exception.Message);
        Assert.Contains("b.zip", exception.Message);
    }

    [Fact]
    public void LoadReportsDuplicateIdsAcrossDefaultAndExplicitMods()
    {
        using var directory = TestDirectory.Create();
        var modsDirectory = Path.Combine(directory.Path, "Mods");
        var explicitDirectory = Path.Combine(directory.Path, "Explicit");
        Directory.CreateDirectory(modsDirectory);
        Directory.CreateDirectory(explicitDirectory);
        CreateModZip(modsDirectory, "a.zip", "same_mod", "A.dll", "A.Entry");
        CreateModZip(explicitDirectory, "b.zip", "same_mod", "B.dll", "B.Entry");

        var manager = new ModManager(modsDirectory, NullLogger.Instance,
            [Path.Combine(explicitDirectory, "b.zip")]);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("same_mod", exception.Message);
        Assert.Contains("a.zip", exception.Message);
        Assert.Contains("b.zip", exception.Message);
    }

    [Fact]
    public void LoadReportsDuplicateIdsAcrossZipAndDirectoryMods()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "a.zip", "same_mod", "A.dll", "A.Entry");
        CreateModDirectory(directory.Path, "same_mod_folder", "same_mod", "B.dll", "B.Entry");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("same_mod", exception.Message);
        Assert.Contains("a.zip", exception.Message);
        Assert.Contains("same_mod_folder", exception.Message);
    }

    [Fact]
    public void LoadReportsAllAssemblyLoadFailures()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "a.zip", "first_mod", "A.dll", "A.Entry");
        CreateModZip(directory.Path, "b.zip", "second_mod", "B.dll", "B.Entry");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("first_mod", exception.Message);
        Assert.Contains("second_mod", exception.Message);
    }

    [Fact]
    public void LoadCreatesEntryAndExposesModInfo()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        using (var file = File.Create(Path.Combine(directory.Path, "test_mod.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "test_mod",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(TestModEntry).FullName}}"
                                              }
                                              """);
            WriteEntry(archive, "assets/textures/stone.txt", "stone");
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.Equal("test_mod", mod.Id);
            Assert.Equal("0.1.0", mod.Version);
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadCreatesEntryFromDirectoryMod()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        var modDirectory = CreateModDirectory(directory.Path, "test_mod", "test_mod", assemblyFile,
            typeof(TestModEntry).FullName!);
        File.Copy(assemblyPath, Path.Combine(modDirectory, assemblyFile), overwrite: true);
        Directory.CreateDirectory(Path.Combine(modDirectory, "assets", "textures"));
        File.WriteAllText(Path.Combine(modDirectory, "assets", "textures", "stone.txt"), "stone");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.Equal("test_mod", mod.Id);
            Assert.Equal("0.1.0", mod.Version);
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void DirectoryModAssemblyLoadsFromFilePath()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        var modDirectory = CreateModDirectory(directory.Path, "test_mod", "test_mod", assemblyFile,
            typeof(TestModEntry).FullName!);
        var modAssemblyPath = Path.Combine(modDirectory, assemblyFile);
        File.Copy(assemblyPath, modAssemblyPath, overwrite: true);

        using var source = new DirectoryModSource(modDirectory);
        var loadContext = new ModAssemblyLoadContext("test_mod", source);

        var assembly = loadContext.LoadMainAssembly(assemblyFile);

        Assert.Equal(Path.GetFullPath(modAssemblyPath), Path.GetFullPath(assembly.Location));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static void CreateModZip(string directory, string fileName, string id, string assembly, string entry)
    {
        using var file = File.Create(Path.Combine(directory, fileName));
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        WriteEntry(archive, "mod.json", $$"""
                                          {
                                            "id": "{{id}}",
                                            "version": "0.1.0",
                                            "assembly": "{{assembly}}",
                                            "entry": "{{entry}}"
                                          }
                                          """);
        WriteEntry(archive, assembly, string.Empty);
    }

    private static string CreateModDirectory(string directory, string folderName, string id, string assembly,
        string entry)
    {
        var modDirectory = Path.Combine(directory, folderName);
        Directory.CreateDirectory(modDirectory);
        File.WriteAllText(Path.Combine(modDirectory, "mod.json"), $$"""
                                                                    {
                                                                      "id": "{{id}}",
                                                                      "version": "0.1.0",
                                                                      "assembly": "{{assembly}}",
                                                                      "entry": "{{entry}}"
                                                                    }
                                                                    """);
        if (!File.Exists(Path.Combine(modDirectory, assembly)))
            File.WriteAllText(Path.Combine(modDirectory, assembly), string.Empty);
        return modDirectory;
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

public sealed class TestModEntry : IMod
{
    public void Configure(ModContext context)
    {
        if (context.Info.Id != "test_mod")
            throw new InvalidOperationException("Unexpected mod id.");

        using var stream = context.Assets.Open("textures/stone.txt");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        if (reader.ReadToEnd() != "stone")
            throw new InvalidOperationException("Unexpected asset content.");
    }
}