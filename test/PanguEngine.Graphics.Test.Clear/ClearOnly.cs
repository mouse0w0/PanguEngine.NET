namespace PanguEngine.Graphics.Test.Clear;

/// <summary>
/// Entry point for the clear-only graphics test.
/// </summary>
internal static class ClearOnly
{
    private static void Main()
    {
        new GraphicsTestApp(new ClearOnlyScene()).Run();
    }
}

/// <summary>
/// Renders a frame that only clears the presentation target.
/// </summary>
internal sealed class ClearOnlyScene : IGraphicsTestScene
{
    /// <inheritdoc/>
    public string Name => "ClearOnly";

    /// <inheritdoc/>
    public void Initialize(Presenter presenter)
    {
    }

    /// <inheritdoc/>
    public void Record(Frame frame, CommandList commands)
    {
        commands.BeginRendering(new RenderingDescription(new ClearColor(0.02f, 0.04f, 0.08f, 1)));
        commands.EndRendering();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
    }
}