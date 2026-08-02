using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Graphics;

public sealed class IndexedIndirectDrawArgumentsTests
{
    [Fact]
    public void LayoutMatchesVkDrawIndexedIndirectCommand()
    {
        Assert.Equal(20u, IndexedIndirectDrawArguments.SizeInBytes);
        Assert.Equal(20, Marshal.SizeOf<IndexedIndirectDrawArguments>());
        Assert.Equal(
            0,
            Marshal.OffsetOf<IndexedIndirectDrawArguments>(
                nameof(IndexedIndirectDrawArguments.IndexCount)).ToInt32());
        Assert.Equal(
            4,
            Marshal.OffsetOf<IndexedIndirectDrawArguments>(
                nameof(IndexedIndirectDrawArguments.InstanceCount)).ToInt32());
        Assert.Equal(
            8,
            Marshal.OffsetOf<IndexedIndirectDrawArguments>(
                nameof(IndexedIndirectDrawArguments.FirstIndex)).ToInt32());
        Assert.Equal(
            12,
            Marshal.OffsetOf<IndexedIndirectDrawArguments>(
                nameof(IndexedIndirectDrawArguments.VertexOffset)).ToInt32());
        Assert.Equal(
            16,
            Marshal.OffsetOf<IndexedIndirectDrawArguments>(
                nameof(IndexedIndirectDrawArguments.FirstInstance)).ToInt32());
    }
}
