using System.Runtime.InteropServices;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class SelectionVertexTests
{
    [Fact]
    public void UsesPositionAndColorLayout()
    {
        Assert.Equal(28, Marshal.SizeOf<SelectionVertex>());
        Assert.Equal(28u, SelectionVertex.SizeInBytes);
        Assert.Equal(28u, SelectionVertex.VertexInput.Buffers.Single().Stride);
        Assert.Equal(
            new[]
            {
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 12)
            },
            SelectionVertex.VertexInput.Attributes);
    }
}