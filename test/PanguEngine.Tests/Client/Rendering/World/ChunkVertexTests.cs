using System.Runtime.InteropServices;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkVertexTests
{
    [Fact]
    public void UsesPositionTextureAndNormalLayout()
    {
        Assert.Equal(32, Marshal.SizeOf<ChunkVertex>());
        Assert.Equal(32u, ChunkVertex.SizeInBytes);
        Assert.Equal(32u, ChunkVertex.VertexInput.Buffers.Single().Stride);
        Assert.Equal(
            new[]
            {
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x2, 12),
                new VertexAttributeDescription(2, 0, VertexAttributeFormat.Float32x3, 20)
            },
            ChunkVertex.VertexInput.Attributes);
    }
}