using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PanguEngine.Collections;
using PanguEngine.Resources;
using PanguEngine.Versioning;

namespace PanguEngine.Modding;

/// <summary>
/// Discovers, validates, loads, and unloads mods.
/// </summary>
/// <param name="modsDirectory">The default mods directory.</param>
/// <param name="logger">The logger used for mod loading.</param>
/// <param name="explicitModPaths">Additional mod paths to load.</param>
public sealed class ModManager(
    string modsDirectory,
    ILogger logger,
    IReadOnlyList<string>? explicitModPaths = null)
{
    private static readonly TimeSpan SlowLifecycleStageWarningThreshold = TimeSpan.FromSeconds(1);

    private readonly List<ModContainer> _containers = [];

    /// <summary>
    /// The loaded mods.
    /// </summary>
    public IReadOnlyList<ModContainer> LoadedMods => _containers.ToArray();

    /// <summary>
    /// Loads all discovered mods.
    /// </summary>
    public void Load()
    {
        var candidates = DiscoverCandidates();
        var descriptors = ReadDescriptors(candidates);
        ValidateDependencies(descriptors);
        var sortedDescriptors = SortByDependencies(descriptors);
        LoadDescriptors(sortedDescriptors);
    }

    internal void RunConfigure()
    {
        RunLifecycleStage(nameof(IMod.Configure),
            static mod => new ModConfigureContext(mod),
            static (mod, context) => mod.Configure(context));
    }

    internal void RunCommonSetup()
    {
        RunLifecycleStage(nameof(IMod.CommonSetup),
            static mod => new ModCommonSetupContext(mod),
            static (mod, context) => mod.CommonSetup(context));
    }

    internal void RunClientSetup()
    {
        RunLifecycleStage(nameof(IMod.ClientSetup),
            static mod => new ModClientSetupContext(mod),
            static (mod, context) => mod.ClientSetup(context));
    }

    internal void RunDedicatedServerSetup()
    {
        RunLifecycleStage(nameof(IMod.DedicatedServerSetup),
            static mod => new ModDedicatedServerSetupContext(mod),
            static (mod, context) => mod.DedicatedServerSetup(context));
    }

    internal void RunReady()
    {
        RunLifecycleStage(nameof(IMod.Ready),
            static mod => new ModReadyContext(mod),
            static (mod, context) => mod.Ready(context));
    }

    /// <summary>
    /// Shuts down loaded mods and clears the loaded mod list.
    /// </summary>
    public void Shutdown()
    {
        foreach (var container in _containers)
            container.Destroy();

        _containers.Clear();
    }

    private List<ModCandidate> DiscoverCandidates()
    {
        var candidates = new List<ModCandidate>();
        if (Directory.Exists(modsDirectory))
        {
            candidates.AddRange(Directory.EnumerateFiles(modsDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(path => new ModCandidate(path, ModSourceKind.Zip)));
            candidates.AddRange(Directory.EnumerateDirectories(modsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => File.Exists(Path.Combine(path, "mod.json")))
                .Select(path => new ModCandidate(path, ModSourceKind.Directory)));
        }

        if (explicitModPaths is not null)
            candidates.AddRange(explicitModPaths.Select(CreateExplicitCandidate));

        return candidates;
    }

    private static List<ModDescriptor> ReadDescriptors(IReadOnlyList<ModCandidate> candidates)
    {
        var descriptors = new List<ModDescriptor>();
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var source = OpenSource(candidate);
                var info = ReadInfo(candidate, source, errors);
                descriptors.Add(new ModDescriptor(candidate, info));
            }
            catch (Exception ex)
            {
                errors.Add($"{candidate.DisplayName}: {ex.Message}");
            }
        }

        AddDuplicateIdErrors(descriptors, errors);

        if (errors.Count > 0)
            throw new ModLoadException(string.Join(Environment.NewLine, errors));

        return descriptors;
    }

    private static ModInfo ReadInfo(ModCandidate candidate, ModSource source, List<string> errors)
    {
        ModManifest manifest;
        using (var stream = source.Open("mod.json"))
        {
            manifest = JsonSerializer.Deserialize<ModManifest>(stream)
                       ?? throw new ModLoadException("mod.json is empty.");
        }

        ValidateManifest(candidate, source, manifest, errors);
        var id = manifest.Id!;
        if (!SemVersion.TryParse(manifest.Version, out var version))
            throw new ModLoadException($"{id}: version '{manifest.Version}' is invalid.");

        var dependencies = (manifest.Dependencies ?? [])
            .Select(dependency => ParseDependency(id, dependency))
            .ToArray();

        return new ModInfo(id, version, manifest.Assembly!, manifest.Entry!, dependencies);
    }

    private static ModDependencyInfo ParseDependency(string modId, ModDependencyManifest dependency)
    {
        SemVersionRange? range = null;
        if (!string.IsNullOrWhiteSpace(dependency.VersionRange) &&
            !SemVersionRange.TryParse(dependency.VersionRange, out range))
            throw new ModLoadException(
                $"{modId}: dependency '{dependency.Id}' version_range '{dependency.VersionRange}' is invalid.");

        return new ModDependencyInfo(dependency.Id!, range, dependency.Optional);
    }

    private static void ValidateManifest(ModCandidate candidate, ModSource source, ModManifest manifest,
        List<string> errors)
    {
        if (!IsValidModId(manifest.Id)) errors.Add($"{candidate.DisplayName}: id is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.Assembly)) errors.Add($"{candidate.DisplayName}: assembly is required.");
        if (string.IsNullOrWhiteSpace(manifest.Entry)) errors.Add($"{candidate.DisplayName}: entry is required.");
        if (!string.IsNullOrWhiteSpace(manifest.Assembly) && !IsFileName(manifest.Assembly))
            errors.Add($"{candidate.DisplayName}: assembly must be a file name.");
        if (!string.IsNullOrWhiteSpace(manifest.Assembly) && IsFileName(manifest.Assembly) &&
            !source.Exists(manifest.Assembly))
            errors.Add($"{candidate.DisplayName}: {manifest.Assembly} was not found.");
    }

    private static void AddDuplicateIdErrors(IReadOnlyList<ModDescriptor> descriptors, List<string> errors)
    {
        foreach (var group in descriptors.Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.Info.Id))
                     .GroupBy(descriptor => descriptor.Info.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var files = string.Join(", ", group.Select(descriptor => descriptor.Candidate.DisplayName));
            errors.Add($"Duplicate mod id '{group.Key}': {files}");
        }
    }

    private static void ValidateDependencies(IReadOnlyList<ModDescriptor> descriptors)
    {
        var errors = new List<string>();
        var descriptorsById = descriptors.ToDictionary(descriptor => descriptor.Info.Id, StringComparer.Ordinal);

        foreach (var descriptor in descriptors.OrderBy(descriptor => descriptor.Info.Id, StringComparer.Ordinal))
        {
            var modId = descriptor.Info.Id;
            var dependencyIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var dependency in descriptor.Info.Dependencies)
            {
                var dependencyId = dependency.Id;
                var dependencyIdIsValid = IsValidModId(dependencyId);
                if (!dependencyIdIsValid)
                    errors.Add($"{modId}: dependency id '{dependencyId}' is invalid.");

                var range = dependency.VersionRange;

                if (!dependencyIdIsValid)
                    continue;

                var normalizedDependencyId = dependencyId!;
                if (!dependencyIds.Add(normalizedDependencyId))
                    errors.Add($"{modId}: duplicate dependency '{normalizedDependencyId}'.");

                if (string.Equals(modId, normalizedDependencyId, StringComparison.Ordinal))
                    errors.Add($"{modId}: mod '{modId}' cannot depend on itself.");

                if (!descriptorsById.TryGetValue(normalizedDependencyId, out var target))
                {
                    if (!dependency.Optional)
                        errors.Add($"{modId}: dependency '{normalizedDependencyId}' is missing.");

                    continue;
                }

                if (range is null)
                    continue;

                var targetVersion = target.Info.Version;
                if (!range.Contains(targetVersion))
                    errors.Add(
                        $"{modId}: dependency '{normalizedDependencyId}' version {targetVersion} does not satisfy {range}.");
            }
        }

        if (errors.Count > 0)
            throw new ModLoadException(string.Join(Environment.NewLine, errors));
    }

    private static ModDescriptor[] SortByDependencies(IReadOnlyList<ModDescriptor> descriptors)
    {
        var descriptorsById = descriptors.ToDictionary(descriptor => descriptor.Info.Id, StringComparer.Ordinal);
        var graph = new DirectedGraph<string>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors.OrderBy(descriptor => descriptor.Info.Id, StringComparer.Ordinal))
            graph.AddNode(descriptor.Info.Id);

        foreach (var descriptor in descriptors.OrderBy(descriptor => descriptor.Info.Id, StringComparer.Ordinal))
        {
            var modId = descriptor.Info.Id;
            foreach (var dependency in descriptor.Info.Dependencies)
            {
                if (!IsValidModId(dependency.Id))
                    continue;

                var dependencyId = dependency.Id!;
                if (!descriptorsById.ContainsKey(dependencyId))
                    continue;

                graph.AddEdge(dependencyId, modId);
            }
        }

        if (!graph.TryTopologicalSort(out var result))
        {
            var cycle = string.Join(", ", result.RemainingNodes);
            throw new ModLoadException($"Dependency cycle detected: {cycle}");
        }

        return result.OrderedNodes.Select(modId => descriptorsById[modId]).ToArray();
    }

    private static bool IsValidModId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var c in value)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.')
                continue;
            return false;
        }

        return true;
    }

    private static bool IsFileName(string value) =>
        value == Path.GetFileName(value) && !value.Contains('/') && !value.Contains('\\');

    private void LoadDescriptors(IReadOnlyList<ModDescriptor> descriptors)
    {
        var errors = new List<string>();
        var loadedContexts = new Dictionary<string, ModAssemblyLoadContext>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            try
            {
                var container = LoadDescriptor(descriptor, loadedContexts);
                loadedContexts[container.Info.Id] = container.LoadContext;
            }
            catch (ModLoadException ex)
            {
                errors.Add(ex.Message);
            }
        }

        if (errors.Count > 0)
            throw new ModLoadException(string.Join(Environment.NewLine, errors));
    }

    private void RunLifecycleStage<TContext>(
        string stageName,
        Func<ModContainer, TContext> createContext,
        Action<IMod, TContext> invoke)
        where TContext : ModLifecycleContext
    {
        foreach (var mod in _containers)
        {
            var context = createContext(mod);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    invoke(mod.Instance, context);
                }
                finally
                {
                    stopwatch.Stop();
                    if (stopwatch.Elapsed > SlowLifecycleStageWarningThreshold)
                        logger.LogWarning(
                            "Mod lifecycle stage {StageName} for mod '{ModId}' took {ElapsedMilliseconds:F0} ms",
                            stageName, mod.Info.Id, stopwatch.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                throw new ModLoadException(
                    $"Failed to run {stageName} for mod '{mod.Info.Id}' from '{mod.SourcePath}'.",
                    ex);
            }
        }
    }

    private ModContainer LoadDescriptor(ModDescriptor descriptor,
        Dictionary<string, ModAssemblyLoadContext> loadedContexts)
    {
        var info = descriptor.Info;
        var id = info.Id;
        var assemblyName = info.Assembly;
        var entryName = info.Entry;
        ModSource? source = null;

        try
        {
            source = OpenSource(descriptor.Candidate);
            var dependencies = new List<ModAssemblyLoadContext>();
            foreach (var dependency in info.Dependencies)
            {
                if (!IsValidModId(dependency.Id))
                    continue;

                var dependencyId = dependency.Id!;
                if (loadedContexts.TryGetValue(dependencyId, out var dependencyContext))
                    dependencies.Add(dependencyContext);
            }

            var loadContext = new ModAssemblyLoadContext(id, source, dependencies);
            var assembly = loadContext.LoadOwnAssembly(assemblyName);
            var entryType = assembly.GetType(entryName, throwOnError: true)!;
            var resources = CreateResourceSource(source);
            var modLogger = CreateModLogger(id);
            if (!typeof(IMod).IsAssignableFrom(entryType))
                throw new ModLoadException($"Mod '{id}' entry '{entryName}' must implement {nameof(IMod)}.");

            var entry = Activator.CreateInstance(entryType) as IMod
                        ?? throw new ModLoadException($"Mod '{id}' entry '{entryName}' could not be created.");

            var container = new ModContainer(info, source, loadContext, modLogger, resources, entry,
                descriptor.Candidate.SourcePath);
            _containers.Add(container);
            source = null;
            return container;
        }
        catch (Exception ex)
        {
            source?.Dispose();
            throw new ModLoadException($"Failed to load mod '{id}' from '{descriptor.Candidate.SourcePath}'.", ex);
        }
    }

    private static ModSource OpenSource(ModCandidate candidate)
    {
        return candidate.Kind switch
        {
            ModSourceKind.Zip => new ZipModSource(candidate.SourcePath),
            ModSourceKind.Directory => new DirectoryModSource(candidate.SourcePath),
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };
    }

    private static IResourceSource CreateResourceSource(ModSource source) => source switch
    {
        DirectoryModSource directorySource => new DirectoryResourceSource(directorySource.SourcePath),
        ZipModSource zipSource => new ZipResourceSource(zipSource.Archive),
        _ => throw new NotSupportedException($"Unsupported mod source type '{source.GetType().Name}'.")
    };

    private static ILogger CreateModLogger(string modId) => Log.CreateLogger(modId);

    private static ModCandidate CreateExplicitCandidate(string sourcePath)
    {
        var kind = Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? ModSourceKind.Zip
            : ModSourceKind.Directory;
        return new ModCandidate(sourcePath, kind);
    }

    private sealed record ModCandidate(string SourcePath, ModSourceKind Kind)
    {
        public string DisplayName => Path.GetFileName(SourcePath);
    }

    private sealed record ModDescriptor(ModCandidate Candidate, ModInfo Info);

    private enum ModSourceKind
    {
        Zip,
        Directory
    }
}