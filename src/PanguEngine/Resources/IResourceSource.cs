using PanguEngine.Registries;

namespace PanguEngine.Resources;

/// <summary>
/// Provides file resources from a single resource root.
/// </summary>
public interface IResourceSource : IDisposable
{
    /// <summary>
    /// Gets whether a resource exists for the specified key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>Whether the resource exists.</returns>
    bool Exists(ResourceKey key)
    {
        return Exists(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Gets whether a resource exists at the specified path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>Whether the resource exists.</returns>
    bool Exists(string resourcePath);

    /// <summary>
    /// Gets a resource for the specified key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource from this source.</returns>
    Resource GetResource(ResourceKey key)
    {
        return GetResource(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Gets a resource for the specified path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The resource from this source.</returns>
    Resource GetResource(string resourcePath)
    {
        return Exists(resourcePath)
            ? new Resource(resourcePath, this, () => Open(resourcePath))
            : throw new FileNotFoundException($"Resource '{resourcePath}' was not found.", resourcePath);
    }

    /// <summary>
    /// Opens a readable stream for the specified resource key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>A new stream for reading the resource content.</returns>
    Stream Open(ResourceKey key)
    {
        return Open(ResourcePath.FromKey(key));
    }

    /// <summary>
    /// Opens a readable stream for the specified resource path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>A new stream for reading the resource content.</returns>
    Stream Open(string resourcePath);

    /// <summary>
    /// Lists resources under the specified directory key.
    /// </summary>
    /// <param name="directoryKey">The directory key.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resources found under the directory.</returns>
    IEnumerable<Resource> List(ResourceKey directoryKey, bool recursive = false)
    {
        return List(ResourcePath.FromKey(directoryKey), recursive);
    }

    /// <summary>
    /// Lists resources under the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="recursive">Whether to include resources in nested directories.</param>
    /// <returns>The resources found under the directory.</returns>
    IEnumerable<Resource> List(string directoryPath, bool recursive = false);
}