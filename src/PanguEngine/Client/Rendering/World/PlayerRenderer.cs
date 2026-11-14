using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.Graphics;
using PanguEngine.World.Entities;
using Silk.NET.Maths;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

internal sealed class PlayerRenderer
{
    private readonly GraphicsDevice _device;
    private readonly ClientWorld _world;
    private readonly GraphicsBuffer?[] _buffers;
    private readonly uint[] _vertexCounts;
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    internal PlayerRenderer(GraphicsDevice device, TextureFormat colorFormat, TextureFormat depthStencilFormat,
        DescriptorSetLayout cameraLayout, ClientWorld world, uint frameSlotCount)
    {
        _device = device;
        _world = world;
        _buffers = new GraphicsBuffer?[checked((int)frameSlotCount)];
        _vertexCounts = new uint[_buffers.Length];
        var vertSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.vert");
        var fragSource = Engine.ResourceManager.ReadAllText("pangu/shaders/world_color.frag");
        _vertexShader = device.CreateShader(new ShaderDescription(ShaderStage.Vertex,
            ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "world_color.vert"), "world_color.vert"));
        _fragmentShader = device.CreateShader(new ShaderDescription(ShaderStage.Fragment,
            ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "world_color.frag"), "world_color.frag"));
        _pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertexShader, _fragmentShader],
            VertexInput = SelectionVertex.VertexInput,
            ColorAttachmentFormats = [colorFormat],
            DescriptorSetLayouts = [cameraLayout],
            PushConstantRanges = [new PushConstantRangeDescription(ShaderStageFlags.Vertex, 0, 16)],
            Topology = PrimitiveTopology.LineList,
            Rasterizer = new RasterizerDescription { CullMode = CullMode.None },
            DepthStencil = new DepthStencilDescription(true, false, CompareOperation.LessOrEqual, false, default, default),
            DepthStencilAttachmentFormat = depthStencilFormat
        });
    }

    internal void Prepare(uint frameSlot, WorldRenderState renderState, double alpha)
    {
        var vertices = new List<SelectionVertex>();
        foreach (var (id, player) in _world.Players)
        {
            if (id == _world.PlayerNetworkId)
                continue;
            var position = player.Position;
            var origin = position.Previous + (position.Current - position.Previous) * alpha - renderState.WorldOrigin;
            for (var corner = 0; corner < 8; corner++)
            {
                for (var axis = 1; axis <= 4; axis <<= 1)
                {
                    if ((corner & axis) != 0)
                        continue;
                    vertices.Add(Vertex(origin + Corner(corner)));
                    vertices.Add(Vertex(origin + Corner(corner | axis)));
                }
            }
            var rotation = player.Rotation;
            vertices.Add(Vertex(origin));
            vertices.Add(Vertex(origin + EntityPoseMath.GetForward(rotation.Yaw, rotation.Pitch) * 0.8));
        }
        var index = checked((int)frameSlot);
        _vertexCounts[index] = checked((uint)vertices.Count);
        if (vertices.Count == 0)
            return;
        var data = vertices.ToArray();
        var size = checked((ulong)data.Length * SelectionVertex.SizeInBytes);
        var buffer = _buffers[index];
        if (buffer is null || buffer.Size < size)
        {
            buffer?.Destroy();
            buffer = _device.CreateBuffer(new BufferDescription(size, BufferUsage.Vertex, MemoryUsage.CpuToGpu));
            _buffers[index] = buffer;
        }
        buffer.Write(data);
    }

    internal void Draw(CommandList commands, DescriptorSet camera, uint frameSlot)
    {
        var index = checked((int)frameSlot);
        if (_vertexCounts[index] == 0)
            return;
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetDescriptorSet(0, camera);
        commands.SetPushConstants(ShaderStageFlags.Vertex, 0, default(Vector4D<float>));
        commands.SetVertexBuffer(0, _buffers[index]!);
        commands.Draw(_vertexCounts[index]);
    }

    internal void Destroy()
    {
        foreach (var buffer in _buffers)
            buffer?.Destroy();
        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }

    private static Vector3D<double> Corner(int index) =>
        new((index & 1) == 0 ? -0.3 : 0.3, (index & 2) == 0 ? -1.6 : 0.2, (index & 4) == 0 ? -0.3 : 0.3);

    private static SelectionVertex Vertex(Vector3D<double> position) =>
        new((float)position.X, (float)position.Y, (float)position.Z, 0.2f, 0.8f, 1f, 1f);
}
