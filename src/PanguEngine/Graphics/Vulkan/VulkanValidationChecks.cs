namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Specifies the Vulkan validation check groups requested during startup.
/// </summary>
[Flags]
public enum VulkanValidationChecks
{
    /// <summary>
    /// No validation checks are requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enables ordinary validation, including parameter, lifetime, and thread checks.
    /// </summary>
    Core = 1,

    /// <summary>
    /// Enables synchronization validation and its supporting tracking mechanisms.
    /// </summary>
    Synchronization = 2,

    /// <summary>
    /// Requests GPU-assisted runtime validation.
    /// </summary>
    GpuAssisted = 4,

    /// <summary>
    /// Enables general best-practices warnings.
    /// </summary>
    BestPractices = 8,

    /// <summary>
    /// Enables warnings about superseded Vulkan API usage.
    /// </summary>
    Legacy = 16
}
