using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Modding;
using PanguEngine.Resources;
using PanguEngine.Versioning;

namespace PanguEngine.Tests.Modding;

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

    [Fact(Skip = "Directory mod assemblies loaded from file paths remain locked until process exit.")]
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
            Assert.Equal("test_mod", mod.Info.Id);
            Assert.Equal(SemVersion.Parse("0.1.0"), mod.Info.Version);
            Assert.True(mod.Info.Version >= SemVersion.Parse("0.1.0"));
            Assert.Empty(mod.Info.Dependencies);
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
        var modsDirectory = Path.Combine(directory.Path, "mods");
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
    public void LoadReportsMissingRequiredDependencyBeforeLoadingAssemblies()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"base_mod\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.Contains("base_mod", exception.Message);
        Assert.DoesNotContain("Failed to load mod", exception.Message);
    }

    [Fact]
    public void LoadReportsDependencyVersionMismatch()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "base.zip", "base_mod", "Base.dll", "Base.Entry");
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"base_mod\", \"version_range\": \"[1.0.0,)\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.Contains("base_mod", exception.Message);
        Assert.Contains("[1.0.0,)", exception.Message);
    }

    [Fact]
    public void LoadTreatsBlankDependencyVersionAsNoVersionRange()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "base.zip", "base_mod", "Base.dll", "Base.Entry");
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"base_mod\", \"version_range\": \" \" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.Contains("base_mod", exception.Message);
        Assert.DoesNotContain("version_range", exception.Message);
    }

    [Fact]
    public void LoadIgnoresMissingOptionalDependency()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"optional_mod\", \"optional\": true }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.DoesNotContain("optional_mod", exception.Message);
    }

    [Fact]
    public void LoadReportsOptionalDependencyVersionMismatchWhenDependencyExists()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "optional.zip", "optional_mod", "Optional.dll", "Optional.Entry");
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"optional_mod\", \"version_range\": \"[1.0.0,)\", \"optional\": true }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.Contains("optional_mod", exception.Message);
        Assert.Contains("[1.0.0,)", exception.Message);
    }

    [Fact]
    public void LoadReportsInvalidDependencyManifestFields()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"Invalid-Id\", \"version_range\": \"not-a-range\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("Invalid-Id", exception.Message);
        Assert.Contains("version_range", exception.Message);
    }

    [Fact]
    public void LoadReportsDuplicateDependencyAndSelfDependency()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [" +
            "{ \"id\": \"base_mod\" }," +
            "{ \"id\": \"base_mod\" }," +
            "{ \"id\": \"dependent_mod\" }]"
        );

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("base_mod", exception.Message);
        Assert.True(exception.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.True(exception.Message.Contains("depend on itself", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadUsesSemVersionRangeForMultipleRangeSegments()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "base.zip", "base_mod", "Base.dll", "Base.Entry");
        CreateModZip(directory.Path, "dependent.zip", "dependent_mod", "Dependent.dll", "Dependent.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"base_mod\", \"version_range\": \"(,0.1.0],[1.0.0,)\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.Contains("dependent_mod", exception.Message);
        Assert.Contains("base_mod", exception.Message);
        Assert.DoesNotContain("version_range", exception.Message);
    }

    [Fact]
    public void LoadOrdersModsByDependenciesBeforeIdOrder()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);

        using (var file = File.Create(Path.Combine(directory.Path, "a_dependent.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "a_dependent",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(AnyModEntry).FullName}}",
                                                "dependencies": [{ "id": "z_base" }]
                                              }
                                              """);
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        using (var file = File.Create(Path.Combine(directory.Path, "z_base.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "z_base",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(AnyModEntry).FullName}}"
                                              }
                                              """);
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            Assert.Equal(new[] { "z_base", "a_dependent" }, manager.LoadedMods.Select(mod => mod.Info.Id));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadOrdersExistingOptionalDependenciesBeforeDependents()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);

        using (var file = File.Create(Path.Combine(directory.Path, "a_dependent.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "a_dependent",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(AnyModEntry).FullName}}",
                                                "dependencies": [{ "id": "z_optional", "version_range": "[0.1.0,)", "optional": true }]
                                              }
                                              """);
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        using (var file = File.Create(Path.Combine(directory.Path, "z_optional.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "z_optional",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(AnyModEntry).FullName}}"
                                              }
                                              """);
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var loadedMods = manager.LoadedMods.ToArray();
            Assert.Equal(new[] { "z_optional", "a_dependent" }, loadedMods.Select(mod => mod.Info.Id));

            var dependency = Assert.Single(loadedMods[1].Info.Dependencies);
            Assert.Equal("z_optional", dependency.Id);
            Assert.Equal("[0.1.0,)", dependency.VersionRange?.ToString());
            Assert.True(dependency.Optional);
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadReportsDependencyCyclesBeforeLoadingAssemblies()
    {
        using var directory = TestDirectory.Create();
        CreateModZip(directory.Path, "a.zip", "a_mod", "A.dll", "A.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"b_mod\" }]");
        CreateModZip(directory.Path, "b.zip", "b_mod", "B.dll", "B.Entry",
            ",\n  \"dependencies\": [{ \"id\": \"a_mod\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);

        var exception = Assert.Throws<ModLoadException>(() => manager.Load());

        Assert.True(exception.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("a_mod", exception.Message);
        Assert.Contains("b_mod", exception.Message);
        Assert.DoesNotContain("Failed to load mod", exception.Message);
    }

    [Fact]
    public void LoadAllowsModToUseAssemblyFromDirectDependency()
    {
        using var directory = TestDirectory.Create();
        var dependencyAssembly =
            CreateMarkerModAssembly(directory.Path, "DirectDependencyLibrary", ("Value", "direct"));
        var callerAssembly = CreateCallerModAssembly(directory.Path, "DirectCallerMod", dependencyAssembly, "Value");

        CreateGeneratedModZip(directory.Path, "dependency.zip", "dependency_mod", dependencyAssembly,
            "Generated.ModEntry");
        CreateGeneratedModZip(directory.Path, "caller.zip", "caller_mod", callerAssembly, "Generated.CallerMod",
            ",\n  \"dependencies\": [{ \"id\": \"dependency_mod\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            Assert.Equal(new[] { "dependency_mod", "caller_mod" }, manager.LoadedMods.Select(mod => mod.Info.Id));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadAllowsModToUseAssemblyFromTransitiveDependency()
    {
        using var directory = TestDirectory.Create();
        var leafAssembly = CreateMarkerModAssembly(directory.Path, "TransitiveLeafLibrary", ("Value", "leaf"));
        var middleAssembly = CreateMarkerModAssembly(directory.Path, "TransitiveMiddleMod");
        var callerAssembly = CreateCallerModAssembly(directory.Path, "TransitiveCallerMod", leafAssembly, "Value");

        CreateGeneratedModZip(directory.Path, "leaf.zip", "leaf_mod", leafAssembly, "Generated.ModEntry");
        CreateGeneratedModZip(directory.Path, "middle.zip", "middle_mod", middleAssembly, "Generated.ModEntry",
            ",\n  \"dependencies\": [{ \"id\": \"leaf_mod\" }]");
        CreateGeneratedModZip(directory.Path, "caller.zip", "caller_mod", callerAssembly, "Generated.CallerMod",
            ",\n  \"dependencies\": [{ \"id\": \"middle_mod\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            Assert.Equal(new[] { "leaf_mod", "middle_mod", "caller_mod" },
                manager.LoadedMods.Select(mod => mod.Info.Id));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadPrefersCurrentModAssemblyBeforeDependencyAssembly()
    {
        using var directory = TestDirectory.Create();
        var dependencyDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "dependency")).FullName;
        var callerDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "caller")).FullName;
        var dependencySharedAssembly = CreateMarkerModAssembly(dependencyDirectory, "OwnPrioritySharedLibrary");
        var callerSharedAssembly =
            CreateMarkerModAssembly(callerDirectory, "OwnPrioritySharedLibrary", ("LocalOnly", "local"));
        var callerAssembly = CreateCallerModAssembly(directory.Path, "OwnPriorityCallerMod", callerSharedAssembly,
            "LocalOnly");

        CreateGeneratedModZip(directory.Path, "dependency.zip", "dependency_mod", dependencySharedAssembly,
            "Generated.ModEntry");
        CreateGeneratedModZip(directory.Path, "caller.zip", "caller_mod", callerAssembly, "Generated.CallerMod",
            ",\n  \"dependencies\": [{ \"id\": \"dependency_mod\" }]", callerSharedAssembly);

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            Assert.Equal(new[] { "dependency_mod", "caller_mod" }, manager.LoadedMods.Select(mod => mod.Info.Id));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadUsesDependencyOrderWhenAssembliesHaveSameName()
    {
        using var directory = TestDirectory.Create();
        var firstDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "first")).FullName;
        var secondDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "second")).FullName;
        var firstSharedAssembly =
            CreateMarkerModAssembly(firstDirectory, "DependencyOrderSharedLibrary", ("FirstOnly", "first"));
        var secondSharedAssembly = CreateMarkerModAssembly(secondDirectory, "DependencyOrderSharedLibrary");
        var callerAssembly = CreateCallerModAssembly(directory.Path, "DependencyOrderCallerMod", firstSharedAssembly,
            "FirstOnly");

        CreateGeneratedModZip(directory.Path, "first.zip", "first_mod", firstSharedAssembly, "Generated.ModEntry");
        CreateGeneratedModZip(directory.Path, "second.zip", "second_mod", secondSharedAssembly, "Generated.ModEntry");
        CreateGeneratedModZip(directory.Path, "caller.zip", "caller_mod", callerAssembly, "Generated.CallerMod",
            ",\n  \"dependencies\": [{ \"id\": \"first_mod\" }, { \"id\": \"second_mod\" }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            Assert.Equal(new[] { "first_mod", "second_mod", "caller_mod" },
                manager.LoadedMods.Select(mod => mod.Info.Id));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadDoesNotDelegateToMissingOptionalDependency()
    {
        using var directory = TestDirectory.Create();
        var missingOptionalAssembly =
            CreateMarkerModAssembly(directory.Path, "MissingOptionalLibrary", ("Value", "missing"));
        var callerAssembly = CreateCallerModAssembly(directory.Path, "MissingOptionalCallerMod",
            missingOptionalAssembly,
            "Value");
        CreateGeneratedModZip(directory.Path, "caller.zip", "caller_mod", callerAssembly, "Generated.CallerMod",
            ",\n  \"dependencies\": [{ \"id\": \"optional_mod\", \"optional\": true }]");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            var exception = Assert.Throws<ModLoadException>(() => manager.Load());

            Assert.Contains("caller_mod", exception.Message);
            Assert.DoesNotContain("optional_mod", exception.Message);
        }
        finally
        {
            manager.Shutdown();
        }
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
            WriteEntry(archive, "assets/test_mod/textures/stone.txt", "stone");
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.Equal("test_mod", mod.Info.Id);
            Assert.Equal(SemVersion.Parse("0.1.0"), mod.Info.Version);
            Assert.True(mod.Info.Version >= SemVersion.Parse("0.1.0"));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact]
    public void LoadExposesZipResourceSourceForZipMods()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(AnyModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        using (var file = File.Create(Path.Combine(directory.Path, "test_mod.zip")))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            WriteEntry(archive, "mod.json", $$"""
                                              {
                                                "id": "test_mod",
                                                "version": "0.1.0",
                                                "assembly": "{{assemblyFile}}",
                                                "entry": "{{typeof(AnyModEntry).FullName}}"
                                              }
                                              """);
            WriteEntry(archive, "assets/test_mod/textures/stone.txt", "stone");
            WriteEntry(archive, "assets/test_mod/textures/nested/dirt.txt", "dirt");
            archive.CreateEntryFromFile(assemblyPath, assemblyFile);
        }

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.IsType<ZipResourceSource>(mod.Resources);
            Assert.Equal("stone", ReadText(mod.Resources.Open("test_mod/textures/stone.txt")));
            Assert.Equal(
                ["test_mod/textures/nested/dirt.txt", "test_mod/textures/stone.txt"],
                mod.Resources.List("test_mod/textures", recursive: true)
                    .Select(resource => resource.Path)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact(Skip = "Directory mod assemblies loaded from file paths remain locked until process exit.")]
    public void LoadCreatesEntryFromDirectoryMod()
    {
        using var directory = TestDirectory.Create();
        var assemblyPath = typeof(TestModEntry).Assembly.Location;
        var assemblyFile = Path.GetFileName(assemblyPath);
        var modDirectory = CreateModDirectory(directory.Path, "test_mod", "test_mod", assemblyFile,
            typeof(TestModEntry).FullName!);
        File.Copy(assemblyPath, Path.Combine(modDirectory, assemblyFile), overwrite: true);
        Directory.CreateDirectory(Path.Combine(modDirectory, "assets", "test_mod", "textures"));
        File.WriteAllText(Path.Combine(modDirectory, "assets", "test_mod", "textures", "stone.txt"), "stone");

        var manager = new ModManager(directory.Path, NullLogger.Instance);
        try
        {
            manager.Load();

            var mod = Assert.Single(manager.LoadedMods);
            Assert.Equal("test_mod", mod.Info.Id);
            Assert.Equal(SemVersion.Parse("0.1.0"), mod.Info.Version);
            Assert.True(mod.Info.Version >= SemVersion.Parse("0.1.0"));
        }
        finally
        {
            manager.Shutdown();
        }
    }

    [Fact(Skip = "Directory mod assemblies loaded from file paths remain locked until process exit.")]
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
        var loadContext = new ModAssemblyLoadContext("test_mod", source, []);
        var assembly = loadContext.LoadOwnAssembly(assemblyFile);

        Assert.Equal(Path.GetFullPath(modAssemblyPath), Path.GetFullPath(assembly.Location));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteFile(ZipArchive archive, string entryName, string path)
    {
        archive.CreateEntryFromFile(path, entryName);
    }

    private static string CreateMarkerModAssembly(string directory, string assemblyName,
        params (string Name, string Value)[] methods)
    {
        var assemblyPath = Path.Combine(directory, assemblyName + ".dll");
        var assemblyBuilder = new PersistedAssemblyBuilder(new AssemblyName(assemblyName), typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

        var entryBuilder =
            moduleBuilder.DefineType("Generated.ModEntry", TypeAttributes.Public | TypeAttributes.Sealed);
        entryBuilder.AddInterfaceImplementation(typeof(IMod));
        var configureBuilder = entryBuilder.DefineMethod(nameof(IMod.Configure),
            MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
            typeof(void),
            [typeof(ModContainer)]);
        var configureIl = configureBuilder.GetILGenerator();
        configureIl.Emit(OpCodes.Ret);
        entryBuilder.DefineMethodOverride(configureBuilder, typeof(IMod).GetMethod(nameof(IMod.Configure))!);
        entryBuilder.CreateType();

        var markerBuilder = moduleBuilder.DefineType("Generated.Marker",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        foreach (var method in methods)
        {
            var methodBuilder = markerBuilder.DefineMethod(method.Name,
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(string),
                Type.EmptyTypes);
            var il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldstr, method.Value);
            il.Emit(OpCodes.Ret);
        }

        markerBuilder.CreateType();
        using var stream = File.Create(assemblyPath);
        assemblyBuilder.Save(stream);
        return assemblyPath;
    }

    private static string CreateCallerModAssembly(string directory, string assemblyName, string dependencyAssemblyPath,
        string dependencyMethodName)
    {
        var assemblyPath = Path.Combine(directory, assemblyName + ".dll");
        var dependencyLoadContext = new AssemblyLoadContext($"DependencyMetadata:{assemblyName}", isCollectible: true);
        try
        {
            using var dependencyStream = File.OpenRead(dependencyAssemblyPath);
            var dependencyAssembly = dependencyLoadContext.LoadFromStream(dependencyStream);
            var dependencyMethod = dependencyAssembly.GetType("Generated.Marker")!.GetMethod(dependencyMethodName)!;
            var assemblyBuilder = new PersistedAssemblyBuilder(new AssemblyName(assemblyName), typeof(object).Assembly);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
            var typeBuilder =
                moduleBuilder.DefineType("Generated.CallerMod", TypeAttributes.Public | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(typeof(IMod));
            var methodBuilder = typeBuilder.DefineMethod(nameof(IMod.Configure),
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                typeof(void),
                [typeof(ModContainer)]);
            var il = methodBuilder.GetILGenerator();
            il.EmitCall(OpCodes.Call, dependencyMethod, null);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(methodBuilder, typeof(IMod).GetMethod(nameof(IMod.Configure))!);
            typeBuilder.CreateType();
            using var stream = File.Create(assemblyPath);
            assemblyBuilder.Save(stream);
            return assemblyPath;
        }
        finally
        {
            dependencyLoadContext.Unload();
        }
    }

    private static void CreateGeneratedModZip(string directory, string fileName, string id, string assemblyPath,
        string entry, string dependencies = "", params string[] additionalAssemblyPaths)
    {
        using var file = File.Create(Path.Combine(directory, fileName));
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        var assemblyFileName = Path.GetFileName(assemblyPath);
        WriteEntry(archive, "mod.json", $$"""
                                          {
                                            "id": "{{id}}",
                                            "version": "0.1.0",
                                            "assembly": "{{assemblyFileName}}",
                                            "entry": "{{entry}}"{{dependencies}}
                                          }
                                          """);
        WriteFile(archive, assemblyFileName, assemblyPath);
        foreach (var additionalAssemblyPath in additionalAssemblyPaths)
            WriteFile(archive, Path.GetFileName(additionalAssemblyPath), additionalAssemblyPath);
    }

    private static void CreateModZip(string directory, string fileName, string id, string assembly, string entry,
        string dependencies = "")
    {
        using var file = File.Create(Path.Combine(directory, fileName));
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        WriteEntry(archive, "mod.json", $$"""
                                          {
                                            "id": "{{id}}",
                                            "version": "0.1.0",
                                            "assembly": "{{assembly}}",
                                            "entry": "{{entry}}"{{dependencies}}
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

    private static string ReadText(Stream stream)
    {
        using var owned = stream;
        using var reader = new StreamReader(owned, Encoding.UTF8);
        return reader.ReadToEnd();
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
    public void Configure(ModContainer container)
    {
        if (container.Info.Id != "test_mod")
            throw new InvalidOperationException("Unexpected mod id.");

        using var stream = container.Resources.Open($"{container.Info.Id}/textures/stone.txt");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        if (reader.ReadToEnd() != "stone")
            throw new InvalidOperationException("Unexpected asset content.");
    }
}

public sealed class AnyModEntry : IMod
{
    public void Configure(ModContainer container)
    {
    }
}