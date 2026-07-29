using PanguEngine.Client.Game;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.Graphics;
using PanguEngine.Maths;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkRenderer
{
    private readonly GraphicsDevice _device;
    private readonly ClientWorld _world;
    private readonly BlockModelManager _models;
    private readonly ChunkMeshBuildQueue _meshBuildQueue;
    private readonly ChunkMeshUpdateState _meshUpdateState = new();
    private readonly ChunkMeshBuildTicket[] _dispatchTickets;
    private readonly Dictionary<ChunkPos, ChunkMeshResource> _meshes = [];
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;
    private readonly DescriptorSetLayout _atlasDescriptorLayout;
    private readonly Sampler _atlasSampler;
    private readonly Texture _atlasTexture;
    private readonly TextureView _atlasView;
    private readonly DescriptorSet _atlasDescriptorSet;
    private readonly List<UploadHandle> _pendingUploads = [];

    internal ChunkRenderer(
        GraphicsDevice device,
        TextureFormat colorFormat,
        DescriptorSetLayout cameraLayout,
        TextureFormat depthStencilFormat,
        ClientWorld world,
        BlockModelManager models)
    {
        _device = device;
        _world = world;
        _models = models;

        _atlasDescriptorLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)]));
        _atlasSampler = _device.CreateSampler(new SamplerDescription(
            FilterMode.Nearest,
            FilterMode.Nearest,
            MipmapMode.Linear,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            1,
            0,
            models.Atlas.MipLevels - 1,
            0));
        var atlas = CreateAtlasResources();
        _atlasTexture = atlas.Texture;
        _atlasView = atlas.View;
        _atlasDescriptorSet = atlas.DescriptorSet;
        _pendingUploads.AddRange(atlas.Uploads);

        var vertSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_textured.vert");
        var fragSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_textured.frag");
        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "world_textured.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "world_textured.frag");
        _vertexShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode, "world_textured.vert"));
        _fragmentShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode, "world_textured.frag"));
        _pipeline = _device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertexShader, _fragmentShader],
            VertexInput = ChunkVertex.VertexInput,
            ColorAttachmentFormats = [colorFormat],
            DescriptorSetLayouts = [cameraLayout, _atlasDescriptorLayout],
            PushConstantRanges =
            [
                new PushConstantRangeDescription(ShaderStageFlags.Vertex, 0, 16)
            ],
            Rasterizer = new RasterizerDescription
            {
                CullMode = CullMode.Back,
                FrontFace = FrontFace.CounterClockwise
            },
            DepthStencil = new DepthStencilDescription(
                true,
                true,
                CompareOperation.LessOrEqual,
                false,
                default,
                default),
            DepthStencilAttachmentFormat = depthStencilFormat
        });

        _meshBuildQueue = new ChunkMeshBuildQueue(models);
        _dispatchTickets = new ChunkMeshBuildTicket[_meshBuildQueue.DispatchBudget];
        foreach (var chunk in _world.Chunks.EnumerateChunks())
        {
            _meshUpdateState.Register(chunk.Position);
            Invalidate(chunk.Position);
        }

        _world.BlockChanged += OnBlockChanged;
    }

    internal List<UploadHandle> UpdateMeshes(Vector3D<double> cameraPosition)
    {
        RemoveCompletedUploads();
        _meshBuildQueue.ThrowIfFaulted();
        var uploadHandles = new List<UploadHandle>(_pendingUploads);
        while (_meshBuildQueue.TryReadResult(out var result))
            ApplyBuildResult(result, uploadHandles);
        DispatchBuildTasks(cameraPosition);
        return uploadHandles;
    }

    private void ApplyBuildResult(
        ChunkMeshBuildResult result,
        List<UploadHandle> uploadHandles)
    {
        if (!_meshUpdateState.Complete(result.Position, result.Version))
            return;

        if (result.Exception is { } exception)
            exception.Throw();

        var mesh = result.Mesh!;
        if (mesh.IsEmpty)
        {
            if (_meshes.Remove(result.Position, out var oldMesh))
                oldMesh.Destroy();
            return;
        }

        var resource = CreateMeshResource(mesh);
        if (_meshes.Remove(result.Position, out var replacedMesh))
            replacedMesh.Destroy();
        _meshes.Add(result.Position, resource);
        uploadHandles.Add(resource.VertexUpload);
        uploadHandles.Add(resource.IndexUpload);
        _pendingUploads.Add(resource.VertexUpload);
        _pendingUploads.Add(resource.IndexUpload);
    }

    private void DispatchBuildTasks(Vector3D<double> cameraPosition)
    {
        var ticketCount = _meshUpdateState.CollectNearest(cameraPosition, _dispatchTickets);
        for (var index = 0; index < ticketCount; index++)
        {
            if (!_meshBuildQueue.TryReserveTaskSlot())
                return;

            var ticket = _dispatchTickets[index];
            var enqueued = false;
            try
            {
                var snapshot = ChunkMeshSnapshot.Capture(_world, ticket.Position);
                enqueued = _meshBuildQueue.TryEnqueueReserved(
                    new ChunkMeshBuildTask(snapshot, ticket.Version));
                if (!enqueued)
                    return;
                _meshUpdateState.MarkDispatched(ticket);
            }
            finally
            {
                if (!enqueued)
                    _meshBuildQueue.ReleaseReservedTaskSlot();
            }
        }
    }

    internal void Draw(
        CommandList commandList,
        DescriptorSet cameraDescriptorSet,
        WorldRenderState worldRenderState)
    {
        commandList.SetGraphicsPipeline(_pipeline);
        commandList.SetDescriptorSet(0, cameraDescriptorSet);
        commandList.SetDescriptorSet(1, _atlasDescriptorSet);
        var frustum = Frustum<float>.CreateFromZeroToOne(worldRenderState.ViewProjection);
        foreach (var (chunkPosition, mesh) in _meshes)
        {
            var worldOrigin = new Vector3D<double>(
                (double)chunkPosition.X * Chunk.SizeX,
                (double)chunkPosition.Y * Chunk.SizeY,
                (double)chunkPosition.Z * Chunk.SizeZ);
            var translatedWorldPosition = worldRenderState.ToTranslatedWorldPosition(worldOrigin);
            var bounds = new Box3D<float>(
                translatedWorldPosition.X,
                translatedWorldPosition.Y,
                translatedWorldPosition.Z,
                translatedWorldPosition.X + Chunk.SizeX,
                translatedWorldPosition.Y + Chunk.SizeY,
                translatedWorldPosition.Z + Chunk.SizeZ);
            if (!frustum.Intersects(bounds))
                continue;

            commandList.SetPushConstants(ShaderStageFlags.Vertex, 0, translatedWorldPosition);
            commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.DrawIndexed(mesh.IndexCount);
        }
    }

    internal void Destroy()
    {
        _world.BlockChanged -= OnBlockChanged;
        _meshBuildQueue.Dispose();
        foreach (var mesh in _meshes.Values)
            mesh.Destroy();
        _meshes.Clear();

        _atlasDescriptorSet.Destroy();
        _atlasView.Destroy();
        _atlasTexture.Destroy();
        _atlasSampler.Destroy();
        _atlasDescriptorLayout.Destroy();
        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }

    private AtlasResources CreateAtlasResources()
    {
        var atlas = _models.Atlas;
        var texture = _device.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = TextureFormat.R8G8B8A8Srgb,
            Width = checked((uint)atlas.Width),
            Height = checked((uint)atlas.Height),
            Depth = 1,
            MipLevels = checked((uint)atlas.MipLevels),
            ArrayLayers = 1,
            Usage = TextureUsage.TransferDestination | TextureUsage.Sampled
        });
        TextureView? view = null;
        DescriptorSet? descriptorSet = null;
        try
        {
            view = _device.CreateTextureView(texture, new TextureViewDescription(
                TextureViewDimension.Type2D,
                0,
                checked((uint)atlas.MipLevels),
                0,
                1));
            descriptorSet = _device.CreateDescriptorSet(new DescriptorSetDescription(
                _atlasDescriptorLayout,
                [DescriptorSetBinding.CombinedImageSampler(0, view, _atlasSampler)]));
            var uploads = new UploadHandle[atlas.MipLevels];
            for (var level = 0; level < atlas.MipLevels; level++)
            {
                var width = checked((uint)(atlas.Width >> level));
                var height = checked((uint)(atlas.Height >> level));
                uploads[level] = _device.UploadTexture(
                    texture,
                    atlas.GetMipPixels(level).Span,
                    TextureUploadRegion.Mip2D(width, height, checked((uint)level)));
            }

            return new AtlasResources(texture, view, descriptorSet, uploads);
        }
        catch
        {
            descriptorSet?.Destroy();
            view?.Destroy();
            texture.Destroy();
            throw;
        }
    }

    private ChunkMeshResource CreateMeshResource(ChunkMesh mesh)
    {
        GraphicsBuffer? vertexBuffer = null;
        GraphicsBuffer? indexBuffer = null;
        try
        {
            vertexBuffer = _device.CreateBuffer(new BufferDescription(
                checked((ulong)mesh.VertexCount * ChunkVertex.SizeInBytes),
                BufferUsage.TransferDestination | BufferUsage.Vertex,
                MemoryUsage.GpuOnly));
            indexBuffer = _device.CreateBuffer(new BufferDescription(
                checked((ulong)mesh.IndexCount * sizeof(uint)),
                BufferUsage.TransferDestination | BufferUsage.Index,
                MemoryUsage.GpuOnly));
            var vertexUpload = _device.UploadBuffer(vertexBuffer, mesh.Vertices);
            var indexUpload = _device.UploadBuffer(indexBuffer, mesh.Indices);
            return new ChunkMeshResource(
                vertexBuffer,
                indexBuffer,
                vertexUpload,
                indexUpload,
                checked((uint)mesh.IndexCount));
        }
        catch
        {
            indexBuffer?.Destroy();
            vertexBuffer?.Destroy();
            throw;
        }
    }

    private void OnBlockChanged(BlockPos position)
    {
        _meshUpdateState.Register(position.ToChunkPos());
        Invalidate(position);
    }

    private void Invalidate(ChunkPos position)
    {
        _meshUpdateState.Invalidate(position);
    }

    private void Invalidate(BlockPos position)
    {
        var chunkPosition = position.ToChunkPos();
        var localPosition = position.ToChunkLocalPos();
        Invalidate(chunkPosition);

        if (localPosition.X == 0)
            Invalidate(chunkPosition.Offset(-1, 0, 0));
        if (localPosition.X == Chunk.MaskX)
            Invalidate(chunkPosition.Offset(1, 0, 0));
        if (localPosition.Y == 0)
            Invalidate(chunkPosition.Offset(0, -1, 0));
        if (localPosition.Y == Chunk.MaskY)
            Invalidate(chunkPosition.Offset(0, 1, 0));
        if (localPosition.Z == 0)
            Invalidate(chunkPosition.Offset(0, 0, -1));
        if (localPosition.Z == Chunk.MaskZ)
            Invalidate(chunkPosition.Offset(0, 0, 1));
    }

    private void RemoveCompletedUploads()
    {
        _pendingUploads.RemoveAll(handle => handle.IsCompletedSuccessfully);
    }

    private sealed class ChunkMeshResource
    {
        internal ChunkMeshResource(
            GraphicsBuffer vertexBuffer,
            GraphicsBuffer indexBuffer,
            UploadHandle vertexUpload,
            UploadHandle indexUpload,
            uint indexCount)
        {
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            VertexUpload = vertexUpload;
            IndexUpload = indexUpload;
            IndexCount = indexCount;
        }

        internal GraphicsBuffer VertexBuffer { get; }
        internal GraphicsBuffer IndexBuffer { get; }
        internal UploadHandle VertexUpload { get; }
        internal UploadHandle IndexUpload { get; }
        internal uint IndexCount { get; }

        internal void Destroy()
        {
            IndexBuffer.Destroy();
            VertexBuffer.Destroy();
        }
    }

    private sealed record AtlasResources(
        Texture Texture,
        TextureView View,
        DescriptorSet DescriptorSet,
        IReadOnlyList<UploadHandle> Uploads);
}

internal readonly record struct ChunkMeshBuildTicket(ChunkPos Position, long Version);

internal sealed class ChunkMeshUpdateState
{
    private readonly Dictionary<ChunkPos, long> _versions = [];
    private readonly HashSet<ChunkPos> _dirty = [];
    private readonly HashSet<ChunkPos> _inFlight = [];

    internal int DirtyCount => _dirty.Count;

    internal void Register(ChunkPos position)
    {
        _versions.TryAdd(position, 0);
    }

    internal void Invalidate(ChunkPos position)
    {
        if (!_versions.TryGetValue(position, out var version))
            return;

        _versions[position] = version + 1;
        _dirty.Add(position);
    }

    internal int CollectNearest(
        Vector3D<double> cameraPosition,
        Span<ChunkMeshBuildTicket> tickets)
    {
        if (tickets.IsEmpty)
            return 0;

        Span<double> distances = stackalloc double[tickets.Length];
        var count = 0;
        foreach (var position in _dirty)
        {
            if (_inFlight.Contains(position))
                continue;

            var centerX = (position.X + 0.5) * Chunk.SizeX;
            var centerY = (position.Y + 0.5) * Chunk.SizeY;
            var centerZ = (position.Z + 0.5) * Chunk.SizeZ;
            var deltaX = centerX - cameraPosition.X;
            var deltaY = centerY - cameraPosition.Y;
            var deltaZ = centerZ - cameraPosition.Z;
            var distance = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
            int insertionIndex;
            if (count < tickets.Length)
            {
                insertionIndex = count;
                count++;
            }
            else
            {
                if (distance >= distances[count - 1])
                    continue;
                insertionIndex = count - 1;
            }

            while (insertionIndex > 0 && distance < distances[insertionIndex - 1])
            {
                distances[insertionIndex] = distances[insertionIndex - 1];
                tickets[insertionIndex] = tickets[insertionIndex - 1];
                insertionIndex--;
            }

            distances[insertionIndex] = distance;
            tickets[insertionIndex] = new ChunkMeshBuildTicket(position, _versions[position]);
        }

        return count;
    }

    internal void MarkDispatched(ChunkMeshBuildTicket ticket)
    {
        _dirty.Remove(ticket.Position);
        _inFlight.Add(ticket.Position);
    }

    internal bool Complete(ChunkPos position, long version)
    {
        _inFlight.Remove(position);
        return _versions[position] == version;
    }

    internal bool IsDirty(ChunkPos position) => _dirty.Contains(position);

    internal bool IsInFlight(ChunkPos position) => _inFlight.Contains(position);
}