using System.Text.Json;
using Microsoft.Extensions.Logging;
using PanguEngine.Versioning;

namespace PanguEngine.Mod;

/// <summary>
/// Discovers, validates, loads, and unloads mods.
/// </summary>
/// <param name="modsDirectory">The default mods directory.</param>
/// <param name="logger">The logger used for mod loading.</param>
/// <param name="explicitModPaths">Additional mod paths to load.</param>
public sealed partial class ModManager(
    string modsDirectory,
    ILogger logger,
    IReadOnlyList<string>? explicitModPaths = null)
{
    private readonly List<ModContainer> _containers = [];

    /// <summary>
    /// The loaded mods.
    /// </summary>
    public IReadOnlyList<ModInfo> LoadedMods => _containers.Select(container => container.Info).ToArray();

    /// <summary>
    /// Loads all discovered mods.
    /// </summary>
    public void Load()
    {
        var candidates = DiscoverCandidates();
        var descriptors = ReadManifests(candidates);
        ValidateDependencies(descriptors);
        LoadDescriptors(SortByDependencies(descriptors));
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

    private static List<ModDescriptor> ReadManifests(IReadOnlyList<ModCandidate> candidates)
    {
        var descriptors = new List<ModDescriptor>();
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var source = OpenSource(candidate);
                var manifest = ReadManifest(source);
                ValidateManifest(candidate, source, manifest, errors);
                descriptors.Add(new ModDescriptor(candidate, manifest));
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

    private static ModManifest ReadManifest(ModSource source)
    {
        using var stream = source.Open("mod.json");
        return JsonSerializer.Deserialize<ModManifest>(stream)
               ?? throw new ModLoadException("mod.json is empty.");
    }

    private static void ValidateManifest(ModCandidate candidate, ModSource source, ModManifest manifest,
        List<string> errors)
    {
        if (!IsValidModId(manifest.Id)) errors.Add($"{candidate.DisplayName}: id is invalid.");
        if (!IsValidVersion(manifest.Version)) errors.Add($"{candidate.DisplayName}: version is invalid.");
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
        foreach (var group in descriptors.Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.Manifest.Id))
                     .GroupBy(descriptor => descriptor.Manifest.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var files = string.Join(", ", group.Select(descriptor => descriptor.Candidate.DisplayName));
            errors.Add($"Duplicate mod id '{group.Key}': {files}");
        }
    }

    private static void ValidateDependencies(IReadOnlyList<ModDescriptor> descriptors)
    {
        var errors = new List<string>();
        var descriptorsById = descriptors.ToDictionary(descriptor => descriptor.Manifest.Id!, StringComparer.Ordinal);

        foreach (var descriptor in descriptors.OrderBy(descriptor => descriptor.Manifest.Id, StringComparer.Ordinal))
        {
            var modId = descriptor.Manifest.Id!;
            var dependencyIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var dependency in descriptor.Manifest.Dependencies ?? [])
            {
                var dependencyId = dependency.Id;
                var dependencyIdIsValid = IsValidModId(dependencyId);
                if (!dependencyIdIsValid)
                    errors.Add($"{modId}: dependency id '{dependencyId}' is invalid.");

                var hasVersionRange = !string.IsNullOrWhiteSpace(dependency.Version);
                SemVersionRange? range = null;
                if (hasVersionRange && !SemVersionRange.TryParse(dependency.Version, out range))
                    errors.Add($"{modId}: dependency '{dependencyId}' version '{dependency.Version}' is invalid.");

                if (!dependencyIdIsValid)
                    continue;

                var normalizedDependencyId = dependencyId!;
                if (!dependencyIds.Add(normalizedDependencyId))
                    errors.Add($"{modId}: duplicate dependency '{normalizedDependencyId}'.");

                if (string.Equals(modId, normalizedDependencyId, StringComparison.Ordinal))
                    errors.Add($"{modId}: mod '{modId}' cannot depend on itself.");

                if (hasVersionRange && range is null)
                    continue;

                if (!descriptorsById.TryGetValue(normalizedDependencyId, out var target))
                {
                    if (!dependency.Optional)
                        errors.Add($"{modId}: dependency '{normalizedDependencyId}' is missing.");

                    continue;
                }

                if (range is null)
                    continue;

                var targetVersion = SemVersion.Parse(target.Manifest.Version!);
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
        var descriptorsById = descriptors.ToDictionary(descriptor => descriptor.Manifest.Id!, StringComparer.Ordinal);
        var incomingEdges =
            descriptors.ToDictionary(descriptor => descriptor.Manifest.Id!, _ => 0, StringComparer.Ordinal);
        var outgoingEdges = descriptors.ToDictionary(descriptor => descriptor.Manifest.Id!, _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            var modId = descriptor.Manifest.Id!;
            foreach (var dependency in descriptor.Manifest.Dependencies ?? [])
            {
                if (!IsValidModId(dependency.Id))
                    continue;

                var dependencyId = dependency.Id!;
                if (!descriptorsById.ContainsKey(dependencyId))
                    continue;

                incomingEdges[modId]++;
                outgoingEdges[dependencyId].Add(modId);
            }
        }

        var ready = new SortedSet<string>(incomingEdges.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var sorted = new List<ModDescriptor>(descriptors.Count);

        while (ready.Count > 0)
        {
            var modId = ready.Min!;
            ready.Remove(modId);
            sorted.Add(descriptorsById[modId]);

            foreach (var dependentId in outgoingEdges[modId])
            {
                incomingEdges[dependentId]--;
                if (incomingEdges[dependentId] == 0)
                    ready.Add(dependentId);
            }
        }

        if (sorted.Count != descriptors.Count)
        {
            var cycle = string.Join(", ", incomingEdges.Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .OrderBy(value => value, StringComparer.Ordinal));
            throw new ModLoadException($"Dependency cycle detected: {cycle}");
        }

        return sorted.ToArray();
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

    private static bool IsValidVersion(string? value) =>
        SemVersion.TryParse(value, out _);

    private static bool IsFileName(string value) =>
        value == Path.GetFileName(value) && !value.Contains('/') && !value.Contains('\\');

    [LoggerMessage(EventId = 0, Level = LogLevel.Information,
        Message = "Loaded mod {ModId} {Version} from {SourcePath}")]
    private static partial void LogModLoaded(ILogger logger, string modId, SemVersion version, string sourcePath);

    private void LoadDescriptors(IReadOnlyList<ModDescriptor> descriptors)
    {
        var errors = new List<string>();
        foreach (var descriptor in descriptors)
        {
            try
            {
                LoadDescriptor(descriptor);
            }
            catch (ModLoadException ex)
            {
                errors.Add(ex.Message);
            }
        }

        if (errors.Count <= 0)
            return;

        Shutdown();
        throw new ModLoadException(string.Join(Environment.NewLine, errors));
    }

    private void LoadDescriptor(ModDescriptor descriptor)
    {
        var id = descriptor.Manifest.Id!;
        var version = SemVersion.Parse(descriptor.Manifest.Version!);
        var assemblyName = descriptor.Manifest.Assembly!;
        var entryName = descriptor.Manifest.Entry!;
        ModSource? source = null;

        try
        {
            source = OpenSource(descriptor.Candidate);
            var loadContext = new ModAssemblyLoadContext(id, source);
            var assembly = loadContext.LoadMainAssembly(assemblyName);
            var entryType = assembly.GetType(entryName, throwOnError: true)!;
            var assets = new ModAssetProvider(source);
            var info = new ModInfo(id, version);
            var modLogger = CreateModLogger(id);
            var context = new ModContext(info, modLogger, assets);
            if (!typeof(IMod).IsAssignableFrom(entryType))
                throw new ModLoadException($"Mod '{id}' entry '{entryName}' must implement {nameof(IMod)}.");

            var entry = Activator.CreateInstance(entryType) as IMod
                        ?? throw new ModLoadException($"Mod '{id}' entry '{entryName}' could not be created.");
            entry.Configure(context);

            _containers.Add(new ModContainer(info, source, loadContext, context, modLogger, entry));
            source = null;
            LogModLoaded(logger, id, version, descriptor.Candidate.SourcePath);
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

    private sealed record ModDescriptor(ModCandidate Candidate, ModManifest Manifest);

    private enum ModSourceKind
    {
        Zip,
        Directory
    }
}