using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using Silk.NET.Maths;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

internal sealed class CrosshairRenderer
{
    private const uint VertexCount = 12;

    private readonly DescriptorSetLayout _descriptorLayout;
    private readonly GraphicsBuffer _uniformBuffer;
    private readonly ulong _uniformStride;
    private readonly DescriptorSet[] _descriptorSets;
    private readonly GraphicsBuffer _vertexBuffer;
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    internal CrosshairRenderer(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        uint frameSlotCount)
    {
        _descriptorLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex)]));

        var uniformSize = (ulong)Marshal.SizeOf<CrosshairUniform>();
        _uniformStride = device.GetAlignedUniformSize(uniformSize);
        _uniformBuffer = device.CreateBuffer(new BufferDescription(
            checked(_uniformStride * frameSlotCount),
            BufferUsage.Uniform,
            MemoryUsage.CpuToGpu));

        _descriptorSets = new DescriptorSet[checked((int)frameSlotCount)];
        for (var i = 0; i < _descriptorSets.Length; i++)
        {
            _descriptorSets[i] = device.CreateDescriptorSet(new DescriptorSetDescription(
                _descriptorLayout,
                [
                    DescriptorSetBinding.UniformBuffer(
                        0,
                        _uniformBuffer,
                        checked((ulong)i * _uniformStride),
                        uniformSize)
                ]));
        }

        var vertices = CreateVertices();
        _vertexBuffer = device.CreateBuffer(new BufferDescription(
            checked((ulong)vertices.Length * CrosshairVertex.SizeInBytes),
            BufferUsage.Vertex,
            MemoryUsage.CpuToGpu));
        _vertexBuffer.Write<CrosshairVertex>(vertices);

        var vertSource = Engine.ResourceManager.ReadAllText("pangu/shaders/crosshair.vert");
        var fragSource = Engine.ResourceManager.ReadAllText("pangu/shaders/crosshair.frag");
        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "crosshair.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "crosshair.frag");

        _vertexShader = device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode,
            Name: "crosshair.vert"));
        _fragmentShader = device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode,
            Name: "crosshair.frag"));
        _pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            [_vertexShader, _fragmentShader],
            CrosshairVertex.VertexInput,
            ColorAttachmentFormats: [colorFormat],
            DescriptorSetLayouts: [_descriptorLayout],
            Rasterizer: new RasterizerDescription(CullMode: CullMode.None),
            ColorBlend: new ColorBlendDescription(AlphaBlend: false),
            DepthStencil: new DepthStencilDescription(
                DepthTestEnabled: false,
                DepthWriteEnabled: false,
                DepthCompareOperation: CompareOperation.Always,
                StencilTestEnabled: false,
                FrontFace: default,
                BackFace: default),
            DepthStencilAttachmentFormat: depthStencilFormat));
    }

    internal void Prepare(uint frameSlot, uint width, uint height)
    {
        var uniform = new CrosshairUniform(CreateProjection(width, height));
        _uniformBuffer.Write(uniform, checked((ulong)frameSlot * _uniformStride));
    }

    internal void Draw(CommandList commandList, uint frameSlot)
    {
        commandList.SetGraphicsPipeline(_pipeline);
        commandList.SetDescriptorSet(0, _descriptorSets[checked((int)frameSlot)]);
        commandList.SetVertexBuffer(0, _vertexBuffer);
        commandList.Draw(VertexCount);
    }

    internal void Destroy()
    {
        _pipeline.Destroy();
        foreach (var descriptorSet in _descriptorSets)
            descriptorSet.Destroy();
        _descriptorLayout.Destroy();
        _uniformBuffer.Destroy();
        _vertexBuffer.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }

    private static CrosshairVertex[] CreateVertices()
    {
        return
        [
            new(-8, -1), new(8, -1), new(8, 1),
            new(-8, -1), new(8, 1), new(-8, 1),
            new(-1, -8), new(1, -8), new(1, 8),
            new(-1, -8), new(1, 8), new(-1, 8)
        ];
    }

    private static Matrix4X4<float> CreateProjection(uint width, uint height)
    {
        var projection = Matrix4X4.CreateOrthographic((float)width, height, 0, 1);
        projection.M22 = -projection.M22;
        return projection;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CrosshairUniform(Matrix4X4<float> Projection);
}