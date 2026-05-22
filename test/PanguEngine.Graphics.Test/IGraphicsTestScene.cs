namespace PanguEngine.Graphics.Test;

/// <summary>
/// Represents a backend-independent graphics test scene.
/// </summary>
public interface IGraphicsTestScene
{
    /// <summary>
    /// Gets the display name of the test scene.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes resources owned by the scene.
    /// </summary>
    /// <param name="presenter">The presenter used by the test application.</param>
    void Initialize(Presenter presenter);

    /// <summary>
    /// Prepares CPU-side state before the frame begins.
    /// </summary>
    void PrepareFrame()
    {
    }

    /// <summary>
    /// Records the scene rendering commands for a frame.
    /// </summary>
    /// <param name="frame">The active graphics frame.</param>
    /// <param name="commands">The command list for the active frame.</param>
    void Record(Frame frame, CommandList commands);

    /// <summary>
    /// Destroys resources owned by the scene.
    /// </summary>
    void Destroy();
}