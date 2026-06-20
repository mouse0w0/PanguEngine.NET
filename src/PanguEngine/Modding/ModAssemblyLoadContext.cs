using System.Reflection;
using System.Runtime.Loader;

namespace PanguEngine.Modding;

/// <summary>
/// Resolves and loads assemblies for a mod.
/// </summary>
/// <param name="modId">The mod identifier.</param>
/// <param name="source">The mod source.</param>
/// <param name="dependencies">The dependency load contexts available to the mod.</param>
internal sealed class ModAssemblyLoadContext(
    string modId,
    ModSource source,
    IReadOnlyList<AssemblyLoadContext> dependencies)
    : AssemblyLoadContext($"Mod:{modId}", isCollectible: false)
{
    private readonly Dictionary<string, string> _assemblies = BuildAssemblyIndex(source);

    /// <summary>
    /// Loads an assembly that belongs to this mod.
    /// </summary>
    /// <param name="assemblyFileName">The assembly file name.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly LoadOwnAssembly(string assemblyFileName)
    {
        if (!_assemblies.TryGetValue(Path.GetFileNameWithoutExtension(assemblyFileName), out var fileName))
            throw new FileNotFoundException($"Mod assembly '{assemblyFileName}' was not found.", assemblyFileName);

        return LoadAssembly(fileName);
    }

    /// <summary>
    /// Loads an assembly by name for this mod context.
    /// </summary>
    /// <param name="assemblyName">The assembly name to load.</param>
    /// <returns>The loaded assembly, or <see langword="null" /> to use default resolution.</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (ShouldShareDefaultAssembly(assemblyName.Name))
            return null;

        return _assemblies.TryGetValue(assemblyName.Name ?? string.Empty, out var fileName)
            ? LoadAssembly(fileName)
            : LoadFromDependencies(assemblyName);
    }

    private Assembly? LoadFromDependencies(AssemblyName assemblyName)
    {
        foreach (var dependency in dependencies)
        {
            try
            {
                return dependency.LoadFromAssemblyName(assemblyName);
            }
            catch (FileNotFoundException)
            {
            }
            catch (FileLoadException)
            {
            }
        }

        return null;
    }

    private Assembly LoadAssembly(string fileName)
    {
        if (source.TryGetFilePath(fileName, out var filePath))
            return LoadFromAssemblyPath(filePath);

        using var assemblyStream = source.Open(fileName);
        using var assemblyCopy = new MemoryStream();
        assemblyStream.CopyTo(assemblyCopy);
        assemblyCopy.Position = 0;

        var symbolsFileName = Path.ChangeExtension(fileName, ".pdb");
        if (!source.Exists(symbolsFileName))
            return LoadFromStream(assemblyCopy);

        using var symbolsStream = source.Open(symbolsFileName);
        using var symbolsCopy = new MemoryStream();
        symbolsStream.CopyTo(symbolsCopy);
        symbolsCopy.Position = 0;
        return LoadFromStream(assemblyCopy, symbolsCopy);
    }

    private static Dictionary<string, string> BuildAssemblyIndex(ModSource source)
    {
        var assemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileName in source.GetAssemblyFileNames())
            assemblies[Path.GetFileNameWithoutExtension(fileName)] = fileName;

        return assemblies;
    }

    private static bool ShouldShareDefaultAssembly(string? name)
    {
        return name is null ||
               name == "PanguEngine" ||
               name.StartsWith("System", StringComparison.Ordinal) ||
               name.StartsWith("Microsoft", StringComparison.Ordinal);
    }
}