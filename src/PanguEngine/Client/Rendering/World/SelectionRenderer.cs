using PanguEngine.Graphics;
using PanguEngine.World;
using PanguEngine.World.Interaction;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

internal sealed class SelectionRenderer
{
    private readonly GraphicsDevice _device;
    private readonly IReadOnlyBlockAccessor _blockAccessor;
    private readonly GraphicsBuffer?[] _buffers;
    private readonly uint[] _vertexCounts;
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    internal SelectionRenderer(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        DescriptorSetLayout cameraLayout,
        IReadOnlyBlockAccessor blockAccessor,
        uint frameSlotCount)
    {
        _device = device;
        _blockAccessor = blockAccessor;
        _buffers = new GraphicsBuffer?[checked((int)frameSlotCount)];
        _vertexCounts = new uint[_buffers.Length];

        var vertSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.vert");
        var fragSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.frag");
        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "world_color.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "world_color.frag");

        _vertexShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode, Name: "world_color.vert"));
        _fragmentShader =
            _device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode, Name: "world_color.frag"));
        _pipeline = _device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            [_vertexShader, _fragmentShader],
            ChunkVertex.VertexInput,
            ColorAttachmentFormats: [colorFormat],
            DescriptorSetLayouts: [cameraLayout],
            Rasterizer: new RasterizerDescription(CullMode: CullMode.None),
            DepthStencil: new DepthStencilDescription(
                DepthTestEnabled: true,
                DepthWriteEnabled: false,
                DepthCompareOperation: CompareOperation.LessOrEqual,
                StencilTestEnabled: false,
                FrontFace: default,
                BackFace: default),
            DepthStencilAttachmentFormat: depthStencilFormat));
    }

    internal void Prepare(uint frameSlot, BlockHit? selection)
    {
        var frameIndex = checked((int)frameSlot);
        if (selection is null)
        {
            _vertexCounts[frameIndex] = 0;
            return;
        }

        var hit = selection.Value;
        var shape = hit.BlockState.GetSelectionShape(_blockAccessor, hit.BlockPosition);
        var vertices = SelectionMeshBuilder.Build(hit.BlockPosition, shape);
        _vertexCounts[frameIndex] = checked((uint)vertices.Length);
        if (vertices.Length == 0)
            return;

        var requiredSize = checked((ulong)vertices.Length * ChunkVertex.SizeInBytes);
        var buffer = _buffers[frameIndex];
        if (buffer is null || buffer.Size < requiredSize)
        {
            buffer?.Destroy();
            buffer = _device.CreateBuffer(new BufferDescription(
                requiredSize,
                BufferUsage.Vertex,
                MemoryUsage.CpuToGpu));
            _buffers[frameIndex] = buffer;
        }

        buffer.Write(vertices);
    }

    internal void Draw(CommandList commandList, DescriptorSet cameraDescriptorSet, uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        var vertexCount = _vertexCounts[frameIndex];
        if (vertexCount == 0)
            return;

        commandList.SetGraphicsPipeline(_pipeline);
        commandList.SetDescriptorSet(0, cameraDescriptorSet);
        commandList.SetVertexBuffer(0, _buffers[frameIndex]!);
        commandList.Draw(vertexCount);
    }

    internal void Destroy()
    {
        foreach (var buffer in _buffers)
            buffer?.Destroy();
        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }
}