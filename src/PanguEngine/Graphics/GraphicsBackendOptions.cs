using PanguEngine.Windowing;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes options used to initialize a graphics backend.
/// </summary>
public record struct GraphicsBackendOptions
{
    /// <summary>
    /// Gets or sets whether graphics API validation is enabled.
    /// </summary>
    public bool EnableValidation { get; set; }

    /// <summary>
    /// Gets the options used to create the primary window.
    /// </summary>
    public WindowOptions PrimaryWindow { get; set; } = new();

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public GraphicsBackendOptions()
    {
    }
}