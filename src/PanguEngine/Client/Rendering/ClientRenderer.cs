using PanguEngine.Client.Game;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Client.World;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
using PanguEngine.World.Interaction;

namespace PanguEngine.Client.Rendering;

internal sealed class ClientRenderer
{
    private const TextureFormat DepthStencilFormat = TextureFormat.Depth24UnormStencil8;

    private readonly GraphicsDevice _device;
    private readonly Presenter _presenter;
    private readonly UiManager _uiManager;
    private readonly WorldRenderer _worldRenderer;
    private readonly UiRenderer _uiRenderer;
    private readonly Texture?[] _depthStencilTextures;
    private readonly TextureView?[] _depthStencilAttachments;
    private uint _depthStencilWidth;
    private uint _depthStencilHeight;
    private bool _destroyed;

    internal ClientRenderer(
        GraphicsDevice device,
        Presenter presenter,
        FontManager fontManager,
        UiManager uiManager,
        ClientWorld world,
        BlockModelManager models)
    {
        _device = device;
        _presenter = presenter;
        _uiManager = uiManager;
        _worldRenderer = new WorldRenderer(
            device,
            presenter.ColorFormat,
            DepthStencilFormat,
            presenter.MaxFramesInFlight,
            world,
            models);
        _uiRenderer = new UiRenderer(
            device,
            fontManager,
            presenter.ColorFormat,
            DepthStencilFormat,
            presenter.MaxFramesInFlight);
        var frameSlotCount = checked((int)presenter.MaxFramesInFlight);
        _depthStencilTextures = new Texture?[frameSlotCount];
        _depthStencilAttachments = new TextureView?[frameSlotCount];
    }

    internal void PrepareFrame(Camera camera, double alpha)
    {
        _uiManager.Update(new Size(_presenter.Width, _presenter.Height));
        _worldRenderer.PrepareFrame(camera, alpha);
        EnsureDepthStencilAttachmentSize();
    }

    internal void DrawFrame(Camera camera, BlockHit? selection, double alpha)
    {
        var screen = _uiManager.CurrentScreen;
        var uiCommands = screen?.CreateDrawCommandList();
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        InvalidOperationException? uploadFailure = null;
        try
        {
            _uiRenderer.PrepareFrame(frame);
            var commandList = frame.CommandList;
            if (frame.Width == 0 || frame.Height == 0
                                 || frame.Width != _depthStencilWidth
                                 || frame.Height != _depthStencilHeight)
            {
                commandList.BeginRecording();
                commandList.PrepareForPresent(frame.ColorOutput);
                commandList.EndRecording();
                return;
            }

            var depthStencilAttachment = EnsureDepthStencilAttachment(frame.FrameSlot);
            uploadFailure = _worldRenderer.PrepareDraw(
                frame,
                camera,
                selection,
                alpha,
                out var worldRenderState);

            commandList.BeginRecording();
            commandList.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(
                        frame.ColorOutput,
                        new ClearColor(0.008f, 0.01f, 0.016f, 1))
                ],
                DepthStencilAttachment = new DepthStencilAttachmentDescription(depthStencilAttachment)
            });
            commandList.SetViewport(0, 0, frame.Width, frame.Height);
            commandList.SetScissor(0, 0, frame.Width, frame.Height);
            if (uploadFailure is null)
                _worldRenderer.Draw(commandList, frame.FrameSlot, worldRenderState);
            if (uiCommands is not null)
                _uiRenderer.Draw(frame, uiCommands);
            commandList.EndRendering();
            commandList.PrepareForPresent(frame.ColorOutput);
            commandList.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }

        if (uploadFailure is not null)
            throw uploadFailure;
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;

        _device.WaitIdle();
        _uiRenderer.Destroy();
        foreach (var depthStencilAttachment in _depthStencilAttachments)
            depthStencilAttachment?.Destroy();
        foreach (var depthStencilTexture in _depthStencilTextures)
            depthStencilTexture?.Destroy();
        _worldRenderer.Destroy();
    }

    private void EnsureDepthStencilAttachmentSize()
    {
        if (_depthStencilWidth == _presenter.Width && _depthStencilHeight == _presenter.Height)
            return;

        _device.WaitIdle();
        for (var i = 0; i < _depthStencilAttachments.Length; i++)
        {
            _depthStencilAttachments[i]?.Destroy();
            _depthStencilAttachments[i] = null;
            _depthStencilTextures[i]?.Destroy();
            _depthStencilTextures[i] = null;
        }

        _depthStencilWidth = _presenter.Width;
        _depthStencilHeight = _presenter.Height;
    }

    private TextureView EnsureDepthStencilAttachment(uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        if (_depthStencilAttachments[frameIndex] is { } existingAttachment)
            return existingAttachment;

        var texture = _device.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = DepthStencilFormat,
            Width = _depthStencilWidth,
            Height = _depthStencilHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.DepthStencilAttachment
        });
        try
        {
            var attachment = _device.CreateTextureView(texture, new TextureViewDescription(
                TextureViewDimension.Type2D,
                0,
                1,
                0,
                1));
            _depthStencilTextures[frameIndex] = texture;
            _depthStencilAttachments[frameIndex] = attachment;
            return attachment;
        }
        catch
        {
            texture.Destroy();
            throw;
        }
    }
}
