using PanguEngine.Graphics;
using PanguEngine.Windowing;

namespace PanguEngine.Client.Tests.Clear;

/// <summary>
/// Entry point for the clear-only graphics test.
/// </summary>
internal static class ClearOnly
{
    private static void Main()
    {
        ClientTestApp.Run(new ClearOnlyScene());
    }
}

/// <summary>
/// Renders a frame that only clears the presentation target.
/// </summary>
internal sealed class ClearOnlyScene : IClientTestScene
{
    private Presenter _presenter = null!;

    /// <inheritdoc/>
    public string Name => "ClearOnly";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        window.Render += (_, _) => DrawFrame();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
    }

    private void DrawFrame()
    {
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription(
                frame.Width,
                frame.Height,
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.02f, 0.04f, 0.08f, 1)),
                ]));
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }
}