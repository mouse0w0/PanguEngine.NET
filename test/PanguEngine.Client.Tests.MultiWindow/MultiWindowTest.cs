using PanguEngine.Graphics;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Client.Tests.MultiWindow;

internal sealed class MultiWindowScene : IClientTestScene
{
    private Presenter _primaryPresenter = null!;
    private Presenter _secondaryPresenter = null!;

    public string Name => "MultiWindow";

    public void Initialize(Window window)
    {
        _primaryPresenter = window.Presenter;
        window.Render += (_, _) => Draw(_primaryPresenter, new ClearColor(0.08f, 0.02f, 0.02f, 1));

        var secondary = ClientTestApp.Current.WindowManager.CreateWindow(new WindowOptions
        {
            Title = "MultiWindow Secondary",
            Size = new Vector2D<int>(640, 480),
            FramesPerSecond = 60
        });
        _secondaryPresenter = secondary.Presenter;
        secondary.Render += (_, _) => Draw(_secondaryPresenter, new ClearColor(0.02f, 0.08f, 0.02f, 1));
    }

    public void Destroy()
    {
    }

    private static void Draw(Presenter presenter, ClearColor clearColor)
    {
        if (!presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(frame.ColorOutput, clearColor)
                ]
            });
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            presenter.EndFrame(frame);
        }
    }
}

internal static class MultiWindowTest
{
    private static void Main()
    {
        ClientTestApp.Run(new MultiWindowScene());
    }
}