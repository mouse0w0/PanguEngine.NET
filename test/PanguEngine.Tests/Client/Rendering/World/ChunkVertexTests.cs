using System.Runtime.InteropServices;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkVertexTests
{
    [Fact]
    public void UsesPositionAndColorLayout()
    {
        Assert.Equal(28, Marshal.SizeOf<ChunkVertex>());
        Assert.Equal(28u, ChunkVertex.SizeInBytes);
        Assert.Equal(28u, ChunkVertex.VertexInput.Buffers.Single().Stride);
        Assert.Equal(
            new[]
            {
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 12)
            },
            ChunkVertex.VertexInput.Attributes);
    }
}