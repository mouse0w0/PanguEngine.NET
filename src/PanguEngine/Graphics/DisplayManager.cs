using PanguEngine.Windowing;

namespace PanguEngine.Graphics;

/// <summary>
/// Provides access to display enumeration for a graphics backend.
/// </summary>
public abstract class DisplayManager
{
    /// <summary>Gets the displays reported by the backend.</summary>
    public abstract IReadOnlyList<DisplayMonitor> Monitors { get; }

    /// <summary>Gets the primary display reported by the backend, if available.</summary>
    public abstract DisplayMonitor? MainMonitor { get; }
}