using System.Reflection;
using System.Runtime.Loader;

namespace PanguEngine.Mod;

internal sealed class ModAssemblyLoadContext(string modId, ModSource source)
    : AssemblyLoadContext($"Mod:{modId}", isCollectible: false)
{
    private readonly Dictionary<string, string> _assemblies = BuildAssemblyIndex(source);

    public Assembly LoadMainAssembly(string assemblyFileName)
    {
        if (!_assemblies.TryGetValue(Path.GetFileNameWithoutExtension(assemblyFileName), out var fileName))
            throw new FileNotFoundException($"Mod assembly '{assemblyFileName}' was not found.", assemblyFileName);

        return LoadAssembly(fileName);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (ShouldShareDefaultAssembly(assemblyName.Name))
            return null;

        return _assemblies.TryGetValue(assemblyName.Name ?? string.Empty, out var fileName)
            ? LoadAssembly(fileName)
            : null;
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