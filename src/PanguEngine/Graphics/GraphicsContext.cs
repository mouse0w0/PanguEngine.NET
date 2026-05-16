namespace PanguEngine.Graphics;

/// <summary>
/// Static access point for the active graphics device.
/// </summary>
public static class GraphicsContext
{
    private static GraphicsDevice? _device;

    /// <summary>
    /// Gets the current graphics device.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the context is not initialized.</exception>
    public static GraphicsDevice Device =>
        _device ?? throw new InvalidOperationException("GraphicsContext is not initialized. Call Initialize first.");

    /// <summary>
    /// Gets whether the graphics context is initialized.
    /// </summary>
    public static bool IsInitialized => _device != null;

    /// <summary>
    /// Initializes the graphics context with a graphics device.
    /// </summary>
    /// <param name="device">The graphics device to use.</param>
    /// <exception cref="InvalidOperationException">Thrown when the context is already initialized.</exception>
    public static void Initialize(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_device != null)
            throw new InvalidOperationException("GraphicsContext is already initialized.");

        _device = device;
    }

    /// <summary>
    /// Shuts down the graphics context and clears the device reference.
    /// </summary>
    public static void Shutdown()
    {
        _device = null;
    }
}