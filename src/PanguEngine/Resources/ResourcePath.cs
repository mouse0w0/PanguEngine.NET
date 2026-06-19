using PanguEngine.Registry;

namespace PanguEngine.Resources;

/// <summary>
/// Provides resource path helpers.
/// </summary>
internal static class ResourcePath
{
    /// <summary>
    /// Converts a resource key to a resource path.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource path.</returns>
    public static string FromKey(ResourceKey key)
    {
        return $"{key.Namespace}/{key.Path}";
    }

    /// <summary>
    /// Converts a resource path to a package entry path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <returns>The package entry path.</returns>
    public static string ToPackagePath(string resourcePath)
    {
        return $"assets/{resourcePath}";
    }
}