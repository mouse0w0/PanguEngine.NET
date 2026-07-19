using System.Runtime.CompilerServices;
using PanguEngine.Client.Game;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.Graphics;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

/// <summary>
/// Coordinates world rendering for a presentation target.
/// </summary>
internal sealed class WorldRenderer
{
    private const TextureFormat DepthStencilFormat = TextureFormat.Depth24UnormStencil8;

    private readonly GraphicsDevice _device;
    private readonly Presenter _presenter;
    private readonly DescriptorSetLayout _cameraDescriptorLayout;
    private readonly GraphicsBuffer _cameraBuffer;
    private readonly ulong _cameraUniformStride;
    private readonly DescriptorSet[] _cameraDescriptorSets;
    private readonly Texture?[] _depthStencilTextures;
    private readonly TextureView?[] _depthStencilAttachments;
    private readonly ChunkRenderer _chunkRenderer;
    private readonly SelectionRenderer _selectionRenderer;
    private readonly CrosshairRenderer _crosshairRenderer;
    private uint _depthStencilWidth;
    private uint _depthStencilHeight;

    /// <summary>
    /// Creates a world renderer.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="presenter">The presentation target.</param>
    /// <param name="world">The client world to render.</param>
    /// <param name="models">The loaded block models.</param>
    public WorldRenderer(
        GraphicsDevice device,
        Presenter presenter,
        ClientWorld world,
        BlockModelManager models)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        ArgumentNullException.ThrowIfNull(world);

        _cameraDescriptorLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex)]));

        var cameraUniformSize = (ulong)Unsafe.SizeOf<Matrix4X4<float>>();
        _cameraUniformStride = _device.GetAlignedUniformSize(cameraUniformSize);
        _cameraBuffer = _device.CreateBuffer(new BufferDescription(
            checked(_cameraUniformStride * _presenter.MaxFramesInFlight),
            BufferUsage.Uniform,
            MemoryUsage.CpuToGpu));

        var frameSlotCount = checked((int)_presenter.MaxFramesInFlight);
        _cameraDescriptorSets = new DescriptorSet[frameSlotCount];
        for (var i = 0; i < _cameraDescriptorSets.Length; i++)
        {
            _cameraDescriptorSets[i] = _device.CreateDescriptorSet(new DescriptorSetDescription(
                _cameraDescriptorLayout,
                [
                    DescriptorSetBinding.UniformBuffer(
                        0,
                        _cameraBuffer,
                        checked((ulong)i * _cameraUniformStride),
                        cameraUniformSize)
                ]));
        }

        _depthStencilTextures = new Texture?[frameSlotCount];
        _depthStencilAttachments = new TextureView?[frameSlotCount];
        _chunkRenderer = new ChunkRenderer(
            _device,
            _presenter.ColorFormat,
            _cameraDescriptorLayout,
            DepthStencilFormat,
            world,
            models);
        _selectionRenderer = new SelectionRenderer(
            _device,
            _presenter.ColorFormat,
            DepthStencilFormat,
            _cameraDescriptorLayout,
            world,
            _presenter.MaxFramesInFlight);
        _crosshairRenderer = new CrosshairRenderer(
            _device,
            _presenter.ColorFormat,
            DepthStencilFormat,
            _presenter.MaxFramesInFlight);
    }

    /// <summary>
    /// Draws a world frame.
    /// </summary>
    /// <param name="camera">The camera used to draw the world.</param>
    /// <param name="selection">The currently selected block.</param>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void DrawFrame(Camera camera, BlockHit? selection, double alpha)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var uploadHandles = _chunkRenderer.RebuildDirtyChunks();
        EnsureDepthStencilAttachmentSize();
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        InvalidOperationException? uploadFailure;
        try
        {
            uploadFailure = GetUploadFailure(uploadHandles);

            var commandList = frame.CommandList;
            if (frame.Width == 0 || frame.Height == 0
                                 || frame.Width != _depthStencilWidth || frame.Height != _depthStencilHeight)
            {
                commandList.BeginRecording();
                commandList.PrepareForPresent(frame.ColorOutput);
                commandList.EndRecording();
                return;
            }

            var frameIndex = checked((int)frame.FrameSlot);
            var depthStencilAttachment = EnsureDepthStencilAttachment(frame.FrameSlot);
            camera.AspectRatio = (double)frame.Width / frame.Height;
            var worldRenderState = camera.CreateWorldRenderState(alpha);
            _cameraBuffer.Write(
                worldRenderState.ViewProjection,
                checked(frame.FrameSlot * _cameraUniformStride));
            if (uploadFailure is null)
                _selectionRenderer.Prepare(frame.FrameSlot, selection);
            _crosshairRenderer.Prepare(frame.FrameSlot, frame.Width, frame.Height);

            commandList.BeginRecording();
            commandList.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.008f, 0.01f, 0.016f, 1))
                ],
                DepthStencilAttachment = new DepthStencilAttachmentDescription(depthStencilAttachment)
            });
            commandList.SetViewport(0, 0, frame.Width, frame.Height);
            commandList.SetScissor(0, 0, frame.Width, frame.Height);

            if (uploadFailure is null)
            {
                _chunkRenderer.Draw(
                    commandList,
                    _cameraDescriptorSets[frameIndex],
                    worldRenderState);
                _selectionRenderer.Draw(
                    commandList,
                    _cameraDescriptorSets[frameIndex],
                    frame.FrameSlot,
                    worldRenderState);
                _crosshairRenderer.Draw(commandList, frame.FrameSlot);
            }

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

    /// <summary>
    /// Releases resources owned by this renderer.
    /// </summary>
    public void Destroy()
    {
        _device.WaitIdle();
        foreach (var depthStencilAttachment in _depthStencilAttachments)
            depthStencilAttachment?.Destroy();
        foreach (var depthStencilTexture in _depthStencilTextures)
            depthStencilTexture?.Destroy();
        _selectionRenderer.Destroy();
        _crosshairRenderer.Destroy();
        _chunkRenderer.Destroy();
        foreach (var descriptorSet in _cameraDescriptorSets)
            descriptorSet.Destroy();
        _cameraDescriptorLayout.Destroy();
        _cameraBuffer.Destroy();
    }

    /// <summary>
    /// Gets an upload failure from a set of upload handles.
    /// </summary>
    /// <param name="uploadHandles">The upload handles to inspect.</param>
    /// <returns>The upload failure, or null when all uploads completed successfully.</returns>
    private static InvalidOperationException? GetUploadFailure(List<UploadHandle> uploadHandles)
    {
        foreach (var uploadHandle in uploadHandles)
        {
            if (!uploadHandle.IsCompleted)
            {
                return new InvalidOperationException(
                    "World chunk mesh upload did not complete after flushing pending uploads.");
            }

            if (uploadHandle.IsFaulted)
                return new InvalidOperationException("World chunk mesh upload failed.", uploadHandle.Exception);
        }

        return null;
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