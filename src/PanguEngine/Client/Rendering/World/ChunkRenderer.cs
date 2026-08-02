using System.Runtime.InteropServices;
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
    private const uint ChunkPositionSizeInBytes = 16;

    private readonly GraphicsDevice _device;
    private readonly ClientWorld _world;
    private readonly BlockModelManager _models;
    private readonly ChunkMeshBuildQueue _meshBuildQueue;
    private readonly ChunkMeshUpdateState _meshUpdateState = new();
    private readonly ChunkMeshBuildTicket[] _dispatchTickets;
    private readonly Dictionary<ChunkPos, ChunkMeshRecord> _meshes = [];
    private readonly ChunkMeshArena _arena;
    private readonly ChunkInstanceSlotPool _instanceSlots = new();
    private readonly ChunkFrameResource[] _frameResources;
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;
    private readonly DescriptorSetLayout _atlasDescriptorLayout;
    private readonly DescriptorSetLayout _chunkStorageLayout;
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
        BlockModelManager models,
        uint maxFramesInFlight)
    {
        _device = device;
        _world = world;
        _models = models;

        _atlasDescriptorLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)]));
        _chunkStorageLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.Vertex)]));
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
            DescriptorSetLayouts = [cameraLayout, _atlasDescriptorLayout, _chunkStorageLayout],
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

        _arena = new ChunkMeshArena(device);
        _frameResources = new ChunkFrameResource[checked((int)maxFramesInFlight)];
        for (var frameIndex = 0; frameIndex < _frameResources.Length; frameIndex++)
            _frameResources[frameIndex] = CreateFrameResource();

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
        _meshes.TryGetValue(result.Position, out var existingMesh);
        var transition = ChunkMeshRecordTransitionPlanner.Plan(existingMesh, mesh);
        ChunkMeshArenaAllocation allocation;
        switch (transition.Kind)
        {
            case ChunkMeshRecordTransitionKind.None:
                return;
            case ChunkMeshRecordTransitionKind.Remove:
                _meshes.Remove(result.Position);
                _arena.Release(existingMesh!.Allocation);
                _instanceSlots.Release(transition.InstanceSlot!.Value);
                return;
            case ChunkMeshRecordTransitionKind.UploadInPlace:
                _arena.UploadInPlace(existingMesh!.Allocation, mesh);
                allocation = existingMesh.Allocation;
                break;
            case ChunkMeshRecordTransitionKind.Replace:
                allocation = _arena.Allocate(mesh);
                _meshes[result.Position] = new ChunkMeshRecord(allocation, transition.InstanceSlot!.Value);
                _arena.Release(existingMesh!.Allocation);
                break;
            case ChunkMeshRecordTransitionKind.Allocate:
                allocation = _arena.Allocate(mesh);
                _meshes.Add(result.Position, new ChunkMeshRecord(allocation, _instanceSlots.Acquire()));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        TrackUploads(allocation, uploadHandles);
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

    internal void PrepareDraw(uint frameSlot, WorldRenderState worldRenderState)
    {
        var frame = _frameResources[checked((int)frameSlot)];
        frame.Candidates.Clear();
        frame.Commands.Clear();
        frame.Ranges.Clear();

        var requiredStorageCapacity = 1u;
        foreach (var mesh in _meshes.Values)
            requiredStorageCapacity = Math.Max(requiredStorageCapacity, checked(mesh.InstanceSlot + 1));
        EnsureStorageCapacity(frame, requiredStorageCapacity);

        var frustum = Frustum<float>.CreateFromZeroToOne(worldRenderState.ViewProjection);
        foreach (var (chunkPosition, mesh) in _meshes)
        {
            var worldOrigin = new Vector3D<double>(
                (double)chunkPosition.X * Chunk.SizeX,
                (double)chunkPosition.Y * Chunk.SizeY,
                (double)chunkPosition.Z * Chunk.SizeZ);
            var translatedWorldPosition = worldRenderState.ToTranslatedWorldPosition(worldOrigin);
            frame.StorageBuffer.Write(
                translatedWorldPosition,
                checked((ulong)mesh.InstanceSlot * ChunkPositionSizeInBytes));
            var bounds = new Box3D<float>(
                translatedWorldPosition.X,
                translatedWorldPosition.Y,
                translatedWorldPosition.Z,
                translatedWorldPosition.X + Chunk.SizeX,
                translatedWorldPosition.Y + Chunk.SizeY,
                translatedWorldPosition.Z + Chunk.SizeZ);
            if (!frustum.Intersects(bounds))
                continue;

            var allocation = mesh.Allocation;
            frame.Candidates.Add(new ChunkDrawCandidate(
                allocation.Page,
                allocation.IndexCount,
                allocation.FirstIndex,
                checked((int)allocation.VertexOffset),
                mesh.InstanceSlot));
        }

        frame.DrawBuilder.Build(frame.Candidates, frame.Commands, frame.Ranges);
        EnsureIndirectCapacity(frame, checked((uint)Math.Max(frame.Commands.Count, 1)));
        if (frame.Commands.Count > 0)
            frame.IndirectBuffer.Write(CollectionsMarshal.AsSpan(frame.Commands));
    }

    internal void Draw(
        CommandList commandList,
        DescriptorSet cameraDescriptorSet,
        uint frameSlot)
    {
        var frame = _frameResources[checked((int)frameSlot)];
        commandList.SetGraphicsPipeline(_pipeline);
        commandList.SetDescriptorSet(0, cameraDescriptorSet);
        commandList.SetDescriptorSet(1, _atlasDescriptorSet);
        commandList.SetDescriptorSet(2, frame.StorageDescriptorSet);
        foreach (var range in frame.Ranges)
        {
            commandList.SetVertexBuffer(0, range.Page.VertexBuffer);
            commandList.SetIndexBuffer(range.Page.IndexBuffer, IndexFormat.UInt32);
            commandList.DrawIndexedIndirect(frame.IndirectBuffer, range.DrawCount, range.Offset);
        }
    }

    internal void Destroy()
    {
        _world.BlockChanged -= OnBlockChanged;
        _meshBuildQueue.Dispose();
        _meshes.Clear();
        _instanceSlots.Clear();
        foreach (var frame in _frameResources)
        {
            frame.StorageDescriptorSet.Destroy();
            frame.StorageBuffer.Destroy();
            frame.IndirectBuffer.Destroy();
        }

        _arena.Destroy();
        _chunkStorageLayout.Destroy();

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

    private ChunkFrameResource CreateFrameResource()
    {
        var storageBuffer = _device.CreateBuffer(new BufferDescription(
            ChunkPositionSizeInBytes,
            BufferUsage.Storage,
            MemoryUsage.CpuToGpu));
        DescriptorSet? storageDescriptorSet = null;
        GraphicsBuffer? indirectBuffer = null;
        try
        {
            storageDescriptorSet = _device.CreateDescriptorSet(new DescriptorSetDescription(
                _chunkStorageLayout,
                [DescriptorSetBinding.StorageBuffer(0, storageBuffer, 0, storageBuffer.Size)]));
            indirectBuffer = _device.CreateBuffer(new BufferDescription(
                IndexedIndirectDrawArguments.SizeInBytes,
                BufferUsage.Indirect,
                MemoryUsage.CpuToGpu));
            return new ChunkFrameResource(storageBuffer, storageDescriptorSet!, indirectBuffer!);
        }
        catch
        {
            indirectBuffer?.Destroy();
            storageDescriptorSet?.Destroy();
            storageBuffer.Destroy();
            throw;
        }
    }

    private void EnsureStorageCapacity(ChunkFrameResource frame, uint requiredCapacity)
    {
        if (frame.StorageCapacity >= requiredCapacity)
            return;

        var newCapacity = GrowCapacity(frame.StorageCapacity, requiredCapacity);
        var newBuffer = _device.CreateBuffer(new BufferDescription(
            checked((ulong)newCapacity * ChunkPositionSizeInBytes),
            BufferUsage.Storage,
            MemoryUsage.CpuToGpu));
        DescriptorSet? newDescriptorSet = null;
        try
        {
            newDescriptorSet = _device.CreateDescriptorSet(new DescriptorSetDescription(
                _chunkStorageLayout,
                [DescriptorSetBinding.StorageBuffer(0, newBuffer, 0, newBuffer.Size)]));
        }
        catch
        {
            newBuffer.Destroy();
            throw;
        }

        var oldDescriptorSet = frame.StorageDescriptorSet;
        var oldBuffer = frame.StorageBuffer;
        frame.StorageDescriptorSet = newDescriptorSet!;
        frame.StorageBuffer = newBuffer;
        frame.StorageCapacity = newCapacity;
        oldDescriptorSet.Destroy();
        oldBuffer.Destroy();
    }

    private void EnsureIndirectCapacity(ChunkFrameResource frame, uint requiredCapacity)
    {
        if (frame.IndirectCapacity >= requiredCapacity)
            return;

        var newCapacity = GrowCapacity(frame.IndirectCapacity, requiredCapacity);
        var newBuffer = _device.CreateBuffer(new BufferDescription(
            checked((ulong)newCapacity * IndexedIndirectDrawArguments.SizeInBytes),
            BufferUsage.Indirect,
            MemoryUsage.CpuToGpu));
        var oldBuffer = frame.IndirectBuffer;
        frame.IndirectBuffer = newBuffer;
        frame.IndirectCapacity = newCapacity;
        oldBuffer.Destroy();
    }

    private static uint GrowCapacity(uint currentCapacity, uint requiredCapacity)
    {
        var capacity = currentCapacity;
        while (capacity < requiredCapacity)
            capacity = checked(capacity * 2);
        return capacity;
    }

    private void TrackUploads(
        ChunkMeshArenaAllocation allocation,
        List<UploadHandle> uploadHandles)
    {
        uploadHandles.Add(allocation.VertexUpload);
        uploadHandles.Add(allocation.IndexUpload);
        _pendingUploads.Add(allocation.VertexUpload);
        _pendingUploads.Add(allocation.IndexUpload);
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
        _pendingUploads.RemoveAll(handle => handle.IsSucceeded);
    }

    private sealed class ChunkFrameResource
    {
        internal ChunkFrameResource(
            GraphicsBuffer storageBuffer,
            DescriptorSet storageDescriptorSet,
            GraphicsBuffer indirectBuffer)
        {
            StorageBuffer = storageBuffer;
            StorageDescriptorSet = storageDescriptorSet;
            IndirectBuffer = indirectBuffer;
        }

        internal GraphicsBuffer StorageBuffer { get; set; }

        internal DescriptorSet StorageDescriptorSet { get; set; }

        internal uint StorageCapacity { get; set; } = 1;

        internal GraphicsBuffer IndirectBuffer { get; set; }

        internal uint IndirectCapacity { get; set; } = 1;

        internal List<ChunkDrawCandidate> Candidates { get; } = [];

        internal List<IndexedIndirectDrawArguments> Commands { get; } = [];

        internal List<ChunkPageDrawRange> Ranges { get; } = [];

        internal ChunkIndirectDrawBuilder DrawBuilder { get; } = new();
    }

    private sealed record AtlasResources(
        Texture Texture,
        TextureView View,
        DescriptorSet DescriptorSet,
        IReadOnlyList<UploadHandle> Uploads);
}

internal sealed class ChunkInstanceSlotPool
{
    private readonly SortedSet<uint> _free = [];
    private uint _next;

    internal uint Acquire()
    {
        if (_free.Count == 0)
        {
            var nextSlot = _next;
            _next = checked(_next + 1);
            return nextSlot;
        }

        var slot = _free.Min;
        _free.Remove(slot);
        return slot;
    }

    internal void Release(uint slot)
    {
        _free.Add(slot);
    }

    internal void Clear()
    {
        _free.Clear();
        _next = 0;
    }
}

internal readonly record struct ChunkDrawCandidate(
    ChunkMeshArenaPage Page,
    uint IndexCount,
    uint FirstIndex,
    int VertexOffset,
    uint InstanceSlot);

internal readonly record struct ChunkPageDrawRange(
    ChunkMeshArenaPage Page,
    ulong Offset,
    uint DrawCount);

internal sealed record ChunkMeshRecord(
    ChunkMeshArenaAllocation Allocation,
    uint InstanceSlot);

internal enum ChunkMeshRecordTransitionKind
{
    None,
    Remove,
    UploadInPlace,
    Replace,
    Allocate
}

internal readonly record struct ChunkMeshRecordTransition(
    ChunkMeshRecordTransitionKind Kind,
    uint? InstanceSlot);

internal static class ChunkMeshRecordTransitionPlanner
{
    internal static ChunkMeshRecordTransition Plan(ChunkMeshRecord? existing, ChunkMesh mesh)
    {
        if (mesh.IsEmpty)
            return existing is null
                ? new ChunkMeshRecordTransition(ChunkMeshRecordTransitionKind.None, null)
                : new ChunkMeshRecordTransition(ChunkMeshRecordTransitionKind.Remove, existing.InstanceSlot);
        if (existing is null)
            return new ChunkMeshRecordTransition(ChunkMeshRecordTransitionKind.Allocate, null);
        return existing.Allocation.CanFit(mesh)
            ? new ChunkMeshRecordTransition(ChunkMeshRecordTransitionKind.UploadInPlace, existing.InstanceSlot)
            : new ChunkMeshRecordTransition(ChunkMeshRecordTransitionKind.Replace, existing.InstanceSlot);
    }
}

internal sealed class ChunkIndirectDrawBuilder
{
    private readonly Dictionary<ChunkMeshArenaPage, int> _groupIndices = [];
    private readonly List<ChunkCandidateGroup> _groups = [];

    internal void Build(
        IReadOnlyList<ChunkDrawCandidate> candidates,
        List<IndexedIndirectDrawArguments> commands,
        List<ChunkPageDrawRange> ranges)
    {
        commands.Clear();
        ranges.Clear();
        _groupIndices.Clear();
        var groupCount = 0;
        foreach (var candidate in candidates)
        {
            if (!_groupIndices.TryGetValue(candidate.Page, out var groupIndex))
            {
                groupIndex = groupCount++;
                if (groupIndex == _groups.Count)
                    _groups.Add(new ChunkCandidateGroup());
                _groups[groupIndex].Reset(candidate.Page);
                _groupIndices.Add(candidate.Page, groupIndex);
            }

            _groups[groupIndex].Candidates.Add(candidate);
        }

        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            var group = _groups[groupIndex];
            var offset = checked((ulong)commands.Count * IndexedIndirectDrawArguments.SizeInBytes);
            foreach (var candidate in group.Candidates)
            {
                commands.Add(new IndexedIndirectDrawArguments(
                    candidate.IndexCount,
                    1,
                    candidate.FirstIndex,
                    candidate.VertexOffset,
                    candidate.InstanceSlot));
            }

            ranges.Add(new ChunkPageDrawRange(group.Page, offset, checked((uint)group.Candidates.Count)));
        }
    }

    private sealed class ChunkCandidateGroup
    {
        internal ChunkMeshArenaPage Page { get; private set; } = null!;

        internal List<ChunkDrawCandidate> Candidates { get; } = [];

        internal void Reset(ChunkMeshArenaPage page)
        {
            Page = page;
            Candidates.Clear();
        }
    }
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
