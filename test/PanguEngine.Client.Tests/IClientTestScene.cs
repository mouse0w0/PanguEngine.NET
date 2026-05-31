using PanguEngine.Windowing;

namespace PanguEngine.Client.Tests;

/// <summary>
/// Represents a backend-independent graphics test scene.
/// </summary>
public interface IClientTestScene
{
    /// <summary>
    /// Gets the display name of the test scene.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes resources owned by the scene and subscribes to the window's render event.
    /// </summary>
    /// <param name="window">The primary window used by the test application.</param>
    void Initialize(Window window);

    /// <summary>
    /// Destroys resources owned by the scene.
    /// </summary>
    void Destroy();
}