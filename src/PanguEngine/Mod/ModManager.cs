using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PanguEngine.Mod;

public sealed partial class ModManager(string modsDirectory, ILogger logger)
{
    private readonly List<ModContainer> _containers = [];

    public IReadOnlyList<ModInfo> LoadedMods => _containers.Select(container => container.Info).ToArray();

    public void Load()
    {
        if (!Directory.Exists(modsDirectory))
            return;

        var candidates = DiscoverCandidates();
        var descriptors = ReadManifests(candidates);
        LoadDescriptors(descriptors.OrderBy(descriptor => descriptor.Manifest.Id, StringComparer.Ordinal).ToArray());
    }

    public void Shutdown()
    {
        foreach (var container in _containers)
            container.Destroy();

        _containers.Clear();
    }

    private List<ModCandidate> DiscoverCandidates()
    {
        var candidates = new List<ModCandidate>();
        candidates.AddRange(Directory.EnumerateFiles(modsDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new ModCandidate(path, ModSourceKind.Zip)));
        candidates.AddRange(Directory.EnumerateDirectories(modsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => File.Exists(Path.Combine(path, "mod.json")))
            .Select(path => new ModCandidate(path, ModSourceKind.Directory)));
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
        !string.IsNullOrWhiteSpace(value) && SemVerRegex().IsMatch(value);

    private static bool IsFileName(string value) =>
        value == Path.GetFileName(value) && !value.Contains('/') && !value.Contains('\\');

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemVerRegex();

    [LoggerMessage(EventId = 0, Level = LogLevel.Information,
        Message = "Loaded mod {ModId} {Version} from {SourcePath}")]
    private static partial void LogModLoaded(ILogger logger, string modId, string version, string sourcePath);

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
        var version = descriptor.Manifest.Version!;
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
            var context = new ModContext(info, logger, assets);
            var entry = Activator.CreateInstance(entryType, context)
                        ?? throw new ModLoadException($"Mod '{id}' entry '{entryName}' could not be created.");
            _containers.Add(new ModContainer(info, source, loadContext, context, entry));
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