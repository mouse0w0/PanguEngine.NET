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
    private static readonly Vector3D<float> LightDirection =
        Vector3D.Normalize(new Vector3D<float>(1, 2, 1));

    private static readonly Vector3D<float> LightColor = new(0.8f, 0.8f, 0.8f);
    private static readonly Vector3D<float> AmbientColor = new(0.2f, 0.2f, 0.2f);

    private readonly GraphicsDevice _device;
    private readonly DescriptorSetLayout _worldDescriptorLayout;
    private readonly GraphicsBuffer _worldBuffer;
    private readonly ulong _worldUniformStride;
    private readonly DescriptorSet[] _worldDescriptorSets;
    private readonly ChunkRenderer _chunkRenderer;
    private readonly SelectionRenderer _selectionRenderer;
    private readonly CrosshairRenderer _crosshairRenderer;
    private List<UploadHandle> _preparedUploadHandles = [];

    /// <summary>
    /// Creates a world renderer.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="colorFormat">The color attachment format.</param>
    /// <param name="depthStencilFormat">The depth/stencil attachment format.</param>
    /// <param name="frameSlotCount">The number of frame resource slots.</param>
    /// <param name="world">The client world to render.</param>
    /// <param name="models">The loaded block models.</param>
    public WorldRenderer(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        uint frameSlotCount,
        ClientWorld world,
        BlockModelManager models)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
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
            checked(_worldUniformStride * frameSlotCount),
            BufferUsage.Uniform,
            MemoryUsage.CpuToGpu));

        var frameResourceCount = checked((int)frameSlotCount);
        _worldDescriptorSets = new DescriptorSet[frameResourceCount];
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

        _chunkRenderer = new ChunkRenderer(
            _device,
            colorFormat,
            _worldDescriptorLayout,
            depthStencilFormat,
            world,
            models,
            frameSlotCount);
        _selectionRenderer = new SelectionRenderer(
            _device,
            colorFormat,
            depthStencilFormat,
            _worldDescriptorLayout,
            world,
            frameSlotCount);
        _crosshairRenderer = new CrosshairRenderer(
            _device,
            colorFormat,
            depthStencilFormat,
            frameSlotCount);
    }

    internal void PrepareFrame(Camera camera, double alpha)
    {
        var cameraPosition = camera.GetInterpolatedPosition(alpha);
        var uploadHandles = _chunkRenderer.UpdateMeshes(cameraPosition);
        _preparedUploadHandles = uploadHandles;
    }

    internal InvalidOperationException? PrepareDraw(
        Frame frame,
        Camera camera,
        BlockHit? selection,
        double alpha,
        out WorldRenderState worldRenderState)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var uploadFailure = GetUploadReadinessFailure(_preparedUploadHandles);
        camera.AspectRatio = (double)frame.Width / frame.Height;
        worldRenderState = camera.CreateWorldRenderState(alpha);
        var worldUniform = new WorldUniform(
            worldRenderState.ViewProjection,
            LightDirection,
            LightColor,
            AmbientColor);
        _worldBuffer.Write(
            worldUniform,
            checked(frame.FrameSlot * _worldUniformStride));
        if (uploadFailure is null)
        {
            _chunkRenderer.PrepareDraw(frame.FrameSlot, worldRenderState);
            _selectionRenderer.Prepare(frame.FrameSlot, selection);
        }
        _crosshairRenderer.Prepare(frame.FrameSlot, frame.Width, frame.Height);

        return uploadFailure;
    }

    internal void Draw(
        CommandList commandList,
        uint frameSlot,
        WorldRenderState worldRenderState)
    {
        var descriptorSet = _worldDescriptorSets[checked((int)frameSlot)];
        _chunkRenderer.Draw(commandList, descriptorSet, frameSlot);
        _selectionRenderer.Draw(commandList, descriptorSet, frameSlot, worldRenderState);
        _crosshairRenderer.Draw(commandList, frameSlot);
    }

    /// <summary>
    /// Releases resources owned by this renderer.
    /// </summary>
    public void Destroy()
    {
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
