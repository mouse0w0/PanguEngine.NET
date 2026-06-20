using System.Text;
using PanguEngine.Registries;

namespace PanguEngine.Resources;

/// <summary>
/// Resolves file resources from an ordered set of resource sources.
/// </summary>
public sealed class ResourceManager : IDisposable
{
    private readonly List<IResourceSource> _sources;

    /// <summary>
    /// Creates a resource manager with the specified source priority order.
    /// </summary>
    /// <param name="sources">The resource sources in priority order.</param>
    internal ResourceManager(IEnumerable<IResourceSource> sources)
    {
        _sources = sources.ToList();
    }

    /// <summary>
    /// Gets the current resource sources in priority order.
    /// </summary>
    public IReadOnlyList<IResourceSource> Sources => _sources.ToArray();

    /// <summary>
    /// Gets whether a resource exists for the specified key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>Whether the resource exists.</returns>
    public bool Exists(ResourceKey key)
    {
        return Exists(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Gets whether a resource exists at the specified path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>Whether the resource exists.</returns>
    public bool Exists(string resourcePath)
    {
        return _sources.Any(source => source.Exists(resourcePath));
    }

    /// <summary>
    /// Opens a readable stream for the specified resource key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>A new stream for reading the resource content.</returns>
    public Stream Open(ResourceKey key)
    {
        return Open(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Opens a readable stream for the specified resource path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>A new stream for reading the resource content.</returns>
    public Stream Open(string resourcePath)
    {
        return GetResource(resourcePath).Open();
    }

    /// <summary>
    /// Reads the specified resource key as bytes.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The full resource content.</returns>
    public byte[] ReadAllBytes(ResourceKey key)
    {
        using var stream = Open(key);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// Reads the specified resource path as bytes.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The full resource content.</returns>
    public byte[] ReadAllBytes(string resourcePath)
    {
        using var stream = Open(resourcePath);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// Reads the specified resource key as UTF-8 text.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The full resource content.</returns>
    public string ReadAllText(ResourceKey key)
    {
        using var stream = Open(key);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the specified resource path as UTF-8 text.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The full resource content.</returns>
    public string ReadAllText(string resourcePath)
    {
        using var stream = Open(resourcePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the highest-priority resource for the specified key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resolved resource.</returns>
    public Resource GetResource(ResourceKey key)
    {
        return GetResource(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Gets the highest-priority resource for the specified path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The resolved resource.</returns>
    public Resource GetResource(string resourcePath)
    {
        foreach (var source in _sources)
        {
            if (source.Exists(resourcePath))
                return source.GetResource(resourcePath);
        }

        throw new FileNotFoundException($"Resource '{resourcePath}' was not found.", resourcePath);
    }

    /// <summary>
    /// Gets all resources matching the specified key in source priority order.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The matching resource stack.</returns>
    public IReadOnlyList<Resource> GetResourceStack(ResourceKey key)
    {
        return GetResourceStack(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Gets all resources matching the specified path in source priority order.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The matching resource stack.</returns>
    public IReadOnlyList<Resource> GetResourceStack(string resourcePath)
    {
        return _sources
            .Where(source => source.Exists(resourcePath))
            .Select(source => source.GetResource(resourcePath))
            .ToArray();
    }

    /// <summary>
    /// Lists highest-priority resources under the specified directory key.
    /// </summary>
    /// <param name="directoryKey">The directory key.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resolved resources.</returns>
    public IEnumerable<Resource> List(ResourceKey directoryKey, bool recursive = false)
    {
        return List(ResourcePath.FromKey(directoryKey), recursive);
    }

    /// <summary>
    /// Lists highest-priority resources under the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resolved resources.</returns>
    public IEnumerable<Resource> List(string directoryPath, bool recursive = false)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Resource>();
        foreach (var source in _sources)
        {
            foreach (var resource in source.List(directoryPath, recursive))
            {
                var listedPath = resource.Path;
                if (seen.Add(listedPath))
                    result.Add(resource);
            }
        }

        return result;
    }

    /// <summary>
    /// Lists resource stacks under the specified directory key.
    /// </summary>
    /// <param name="directoryKey">The directory key.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resource stacks grouped by resource path.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<Resource>> ListResourceStacks(ResourceKey directoryKey,
        bool recursive = false)
    {
        return ListResourceStacks(ResourcePath.FromKey(directoryKey), recursive);
    }

    /// <summary>
    /// Lists resource stacks under the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resource stacks grouped by resource path.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<Resource>> ListResourceStacks(string directoryPath,
        bool recursive = false)
    {
        var stacks = new Dictionary<string, List<Resource>>(StringComparer.Ordinal);
        foreach (var source in _sources)
        {
            foreach (var resource in source.List(directoryPath, recursive))
            {
                var listedPath = resource.Path;
                if (!stacks.TryGetValue(listedPath, out var stack))
                {
                    stack = [];
                    stacks.Add(listedPath, stack);
                }

                stack.Add(resource);
            }
        }

        var result = new Dictionary<string, IReadOnlyList<Resource>>(StringComparer.Ordinal);
        foreach (var pair in stacks)
            result.Add(pair.Key, pair.Value.ToArray());

        return result;
    }

    /// <summary>
    /// Replaces the current resource sources.
    /// </summary>
    /// <param name="sources">The new resource sources in priority order.</param>
    public void SetSources(IEnumerable<IResourceSource> sources)
    {
        var newSources = sources.ToList();
        var oldSources = _sources.ToArray();
        _sources.Clear();
        _sources.AddRange(newSources);
        foreach (var source in oldSources)
        {
            if (!newSources.Contains(source))
                source.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var source in _sources)
            source.Dispose();
    }
}