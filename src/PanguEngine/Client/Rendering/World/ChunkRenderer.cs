using PanguEngine.Client.World;
using PanguEngine.Graphics;
using PanguEngine.World.Chunking;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

/// <summary>
/// Renders chunk meshes for a client world.
/// </summary>
internal sealed class ChunkRenderer
{
    private readonly GraphicsDevice _device;
    private readonly ClientWorld _world;
    private readonly ChunkMeshBuilder _meshBuilder = new();
    private readonly Dictionary<ChunkPos, ChunkMeshResource> _meshes = [];
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    /// <summary>
    /// Creates a chunk renderer.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="colorFormat">The target color format.</param>
    /// <param name="world">The client world to render.</param>
    public ChunkRenderer(GraphicsDevice device, TextureFormat colorFormat, ClientWorld world)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _world = world ?? throw new ArgumentNullException(nameof(world));

        var vertSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.vert");
        var fragSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.frag");
        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "world_color.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "world_color.frag");

        _vertexShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode, Name: "world_color.vert"));
        _fragmentShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode, Name: "world_color.frag"));
        _pipeline = _device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertexShader, _fragmentShader },
            ChunkVertex.VertexInput,
            ColorAttachmentFormats: new[] { colorFormat }));
    }

    /// <summary>
    /// Rebuilds meshes for dirty chunks.
    /// </summary>
    /// <returns>The upload handles created for rebuilt meshes.</returns>
    public List<UploadHandle> RebuildDirtyChunks()
    {
        var uploadHandles = new List<UploadHandle>();
        foreach (var chunk in _world.Chunks.EnumerateDirtyChunks().ToArray())
        {
            var chunkPos = chunk.Position;
            var mesh = _meshBuilder.Build(_world, chunk);

            if (_meshes.Remove(chunkPos, out var oldMesh))
                oldMesh.Destroy();

            if (!mesh.IsEmpty)
            {
                var bufferSize = checked((ulong)mesh.VertexCount * ChunkVertex.SizeInBytes);
                var buffer = _device.CreateBuffer(new BufferDescription(
                    bufferSize,
                    BufferUsage.TransferDestination | BufferUsage.Vertex,
                    MemoryUsage.GpuOnly));
                var uploadHandle = _device.UploadBuffer(buffer, mesh.Vertices);
                _meshes.Add(chunkPos, new ChunkMeshResource(buffer, uploadHandle, checked((uint)mesh.VertexCount)));
                uploadHandles.Add(uploadHandle);
            }

            chunk.ClearDirty();
        }

        return uploadHandles;
    }

    /// <summary>
    /// Records chunk draw commands.
    /// </summary>
    /// <param name="commandList">The active command list.</param>
    public void Draw(CommandList commandList)
    {
        ArgumentNullException.ThrowIfNull(commandList);

        commandList.SetGraphicsPipeline(_pipeline);
        foreach (var mesh in _meshes.Values)
        {
            commandList.SetVertexBuffer(0, mesh.Buffer);
            commandList.Draw(mesh.VertexCount);
        }
    }

    /// <summary>
    /// Releases graphics resources owned by this renderer.
    /// </summary>
    public void Destroy()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Destroy();
        _meshes.Clear();

        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }

    /// <summary>
    /// Stores graphics resources for a chunk mesh.
    /// </summary>
    private sealed class ChunkMeshResource
    {
        /// <summary>
        /// Creates a chunk mesh resource record.
        /// </summary>
        /// <param name="buffer">The vertex buffer.</param>
        /// <param name="uploadHandle">The upload handle for the buffer.</param>
        /// <param name="vertexCount">The number of vertices in the mesh.</param>
        public ChunkMeshResource(GraphicsBuffer buffer, UploadHandle uploadHandle, uint vertexCount)
        {
            Buffer = buffer;
            UploadHandle = uploadHandle;
            VertexCount = vertexCount;
        }

        /// <summary>The vertex buffer.</summary>
        public GraphicsBuffer Buffer { get; }

        /// <summary>The upload handle for the buffer.</summary>
        public UploadHandle UploadHandle { get; }

        /// <summary>The number of vertices in the mesh.</summary>
        public uint VertexCount { get; }

        /// <summary>
        /// Releases graphics resources owned by this mesh resource.
        /// </summary>
        public void Destroy()
        {
            Buffer.Destroy();
        }
    }
}