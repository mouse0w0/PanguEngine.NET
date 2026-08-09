using PanguEngine.Client.Tests;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;
using PanguEngine.Windowing;

namespace PanguEngine.Client.Tests.UiBatch;

internal static class UiBatch
{
    private static void Main() =>
        ClientTestApp.Run(new UiBatchScene());
}

internal sealed class UiBatchScene : IClientTestScene
{
    private const double UiScale = 1.25;
    private Presenter _presenter = null!;
    private UiRenderer _renderer = null!;
    private UiBatchNode _root = null!;
    private UiScreen _screen = null!;

    public string Name => "UI Batch";

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        _renderer = new UiRenderer(
            ClientTestApp.Current.Device,
            _presenter.ColorFormat,
            TextureFormat.Undefined,
            _presenter.MaxFramesInFlight);
        _root = new UiBatchNode();
        _screen = new UiScreen(_root);
        window.Render += (_, _) => DrawFrame();
    }

    public void Destroy() =>
        _renderer.Destroy();

    private void DrawFrame()
    {
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commandList = frame.CommandList;
            commandList.BeginRecording();
            if (frame.Width == 0 || frame.Height == 0)
            {
                commandList.PrepareForPresent(frame.ColorOutput);
                commandList.EndRecording();
                return;
            }

            _root.Dense = frame.FrameNumber >= _presenter.MaxFramesInFlight;
            var logicalSize = new Size(frame.Width / UiScale, frame.Height / UiScale);
            _root.Measure(logicalSize);
            _root.Arrange(new Rect(0, 0, logicalSize));
            var drawCommands = _screen.CreateDrawCommandList();

            commandList.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(
                        frame.ColorOutput,
                        new ClearColor(0.015f, 0.018f, 0.024f, 1))
                ]
            });
            _renderer.Draw(frame, drawCommands, UiScale);
            commandList.EndRendering();
            commandList.PrepareForPresent(frame.ColorOutput);
            commandList.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private sealed class UiBatchNode : UiNode
    {
        private const double DesignWidth = 640;
        private const double DesignHeight = 480;

        internal bool Dense { get; set; }

        protected override void DrawCore(UiDrawingContext context)
        {
            var layoutScale = Math.Min(LayoutBounds.Width / DesignWidth, LayoutBounds.Height / DesignHeight);
            var offsetX = (LayoutBounds.Width - DesignWidth * layoutScale) / 2;
            var offsetY = (LayoutBounds.Height - DesignHeight * layoutScale) / 2;

            using (context.PushClip(Scale(new Rect(40, 40, 300, 180), layoutScale, offsetX, offsetY)))
            {
                context.FillRectangle(Scale(new Rect(20, 20, 260, 150), layoutScale, offsetX, offsetY), new Color(230, 64, 80, 180));
                context.FillRectangle(Scale(new Rect(120, 70, 260, 150), layoutScale, offsetX, offsetY), new Color(40, 190, 150, 160));
                var count = Dense ? 128 : 4;
                for (var index = 0; index < count; index++)
                {
                    var x = 48 + index % 16 * 17;
                    var y = 48 + index / 16 * 17;
                    context.FillRectangle(Scale(new Rect(x, y, 12, 12), layoutScale, offsetX, offsetY), new Color(245, 210, 70, 150));
                }
            }

            using (context.PushClip(Scale(new Rect(380, 80, 220, 220), layoutScale, offsetX, offsetY)))
            {
                context.FillRectangle(Scale(new Rect(340, 40, 300, 300), layoutScale, offsetX, offsetY), new Color(70, 110, 240, 150));
                context.FillRectangle(Scale(new Rect(430, 130, 140, 140), layoutScale, offsetX, offsetY), new Color(245, 245, 245, 120));
            }
        }

        private static Rect Scale(Rect rect, double layoutScale, double offsetX, double offsetY) =>
            new(
                offsetX + rect.X * layoutScale,
                offsetY + rect.Y * layoutScale,
                rect.Width * layoutScale,
                rect.Height * layoutScale);
    }
}
