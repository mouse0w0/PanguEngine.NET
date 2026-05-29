using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Graphics;

/// <summary>
/// Creates graphics backend instances.
/// </summary>
internal static class GraphicsBackendFactory
{
    /// <summary>
    /// Creates a graphics backend for the requested type.
    /// </summary>
    /// <param name="type">The graphics backend type.</param>
    /// <param name="options">The backend initialization options.</param>
    /// <returns>The created graphics backend.</returns>
    public static GraphicsBackend Create(GraphicsBackendType type, GraphicsBackendOptions options)
    {
        return type switch
        {
            GraphicsBackendType.Vulkan => new VulkanBackend(options),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}