using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    private static readonly Vector3D<float> LightDirection =
        Vector3D.Normalize(new Vector3D<float>(1, 2, 1));

    private static readonly Vector3D<float> LightColor = new(0.8f, 0.8f, 0.8f);
    private static readonly Vector3D<float> AmbientColor = new(0.2f, 0.2f, 0.2f);

    private readonly GraphicsDevice _device;
    private readonly Presenter _presenter;
    private readonly DescriptorSetLayout _worldDescriptorLayout;
    private readonly GraphicsBuffer _worldBuffer;
    private readonly ulong _worldUniformStride;
    private readonly DescriptorSet[] _worldDescriptorSets;
    private readonly Texture?[] _depthStencilTextures;
    private readonly TextureView?[] _depthStencilAttachments;
    private readonly ChunkRenderer _chunkRenderer;
    private readonly SelectionRenderer _selectionRenderer;
    private readonly CrosshairRenderer _crosshairRenderer;
    private List<UploadHandle> _preparedUploadHandles = [];
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

        _worldDescriptorLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
        [
            new DescriptorSetLayoutBinding(
                0,
                DescriptorType.UniformBuffer,
                ShaderStageFlags.Vertex | ShaderStageFlags.Fragment)
        ]));

        var worldUniformSize = (ulong)Unsafe.SizeOf<WorldUniform>();
        _worldUniformStride = _device.GetAlignedUniformSize(worldUniformSize);
        _worldBuffer = _device.CreateBuffer(new BufferDescription(
            checked(_worldUniformStride * _presenter.MaxFramesInFlight),
            BufferUsage.Uniform,
            MemoryUsage.CpuToGpu));

        var frameSlotCount = checked((int)_presenter.MaxFramesInFlight);
        _worldDescriptorSets = new DescriptorSet[frameSlotCount];
        for (var i = 0; i < _worldDescriptorSets.Length; i++)
        {
            _worldDescriptorSets[i] = _device.CreateDescriptorSet(new DescriptorSetDescription(
                _worldDescriptorLayout,
                [
                    DescriptorSetBinding.UniformBuffer(
                        0,
                        _worldBuffer,
                        checked((ulong)i * _worldUniformStride),
                        worldUniformSize)
                ]));
        }

        _depthStencilTextures = new Texture?[frameSlotCount];
        _depthStencilAttachments = new TextureView?[frameSlotCount];
        _chunkRenderer = new ChunkRenderer(
            _device,
            _presenter.ColorFormat,
            _worldDescriptorLayout,
            DepthStencilFormat,
            world,
            models);
        _selectionRenderer = new SelectionRenderer(
            _device,
            _presenter.ColorFormat,
            DepthStencilFormat,
            _worldDescriptorLayout,
            world,
            _presenter.MaxFramesInFlight);
        _crosshairRenderer = new CrosshairRenderer(
            _device,
            _presenter.ColorFormat,
            DepthStencilFormat,
            _presenter.MaxFramesInFlight);
    }

    internal void PrepareFrame(Camera camera, double alpha)
    {
        var cameraPosition = camera.GetInterpolatedPosition(alpha);
        var uploadHandles = _chunkRenderer.UpdateMeshes(cameraPosition);
        EnsureDepthStencilAttachmentSize();
        _preparedUploadHandles = uploadHandles;
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

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        InvalidOperationException? uploadFailure;
        try
        {
            uploadFailure = GetUploadReadinessFailure(_preparedUploadHandles);

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
            var worldUniform = new WorldUniform(
                worldRenderState.ViewProjection,
                LightDirection,
                LightColor,
                AmbientColor);
            _worldBuffer.Write(
                worldUniform,
                checked(frame.FrameSlot * _worldUniformStride));
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
                    _worldDescriptorSets[frameIndex],
                    worldRenderState);
                _selectionRenderer.Draw(
                    commandList,
                    _worldDescriptorSets[frameIndex],
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
        foreach (var descriptorSet in _worldDescriptorSets)
            descriptorSet.Destroy();
        _worldDescriptorLayout.Destroy();
        _worldBuffer.Destroy();
    }

    /// <summary>
    /// Checks whether a set of upload handles is ready to be consumed by a graphics submission.
    /// </summary>
    /// <param name="uploadHandles">The upload handles to inspect.</param>
    /// <returns>
    /// The upload readiness failure, or null when every upload is ready to be consumed.
    /// </returns>
    private static InvalidOperationException? GetUploadReadinessFailure(List<UploadHandle> uploadHandles)
    {
        foreach (var uploadHandle in uploadHandles)
        {
            try
            {
                uploadHandle.ThrowIfNotReady();
            }
            catch (Exception exception)
            {
                return new InvalidOperationException("World chunk mesh upload is not ready.", exception);
            }
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

[StructLayout(LayoutKind.Sequential)]
internal readonly struct WorldUniform(
    Matrix4X4<float> viewProjection,
    Vector3D<float> lightDirection,
    Vector3D<float> lightColor,
    Vector3D<float> ambientColor)
{
    internal readonly Matrix4X4<float> ViewProjection = viewProjection;

    internal readonly Vector4D<float> LightDirection = new(
        lightDirection.X,
        lightDirection.Y,
        lightDirection.Z,
        0);

    internal readonly Vector4D<float> LightColor = new(lightColor.X, lightColor.Y, lightColor.Z, 0);
    internal readonly Vector4D<float> AmbientColor = new(ambientColor.X, ambientColor.Y, ambientColor.Z, 0);
}
