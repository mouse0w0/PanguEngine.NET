using PanguEngine.Client.Rendering.World;
using PanguEngine.Graphics;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkMeshArenaTests
{
    [Fact]
    public void ReleasedAdjacentRangesAreReusedByFirstFit()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var first = arena.Allocate(CreateMesh(2, 2));
        var second = arena.Allocate(CreateMesh(2, 2));
        _ = arena.Allocate(CreateMesh(2, 2));
        arena.Release(first);
        arena.Release(second);

        var merged = arena.Allocate(CreateMesh(4, 4));

        Assert.Equal(0u, merged.VertexOffset);
        Assert.Equal(0u, merged.FirstIndex);
    }

    [Fact]
    public void FailedPairAllocationRollsBackFirstInterval()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var first = arena.Allocate(CreateMesh(2, 6));

        var moved = arena.Allocate(CreateMesh(2, 4));
        var fillsRolledBackSpace = arena.Allocate(CreateMesh(6, 2));

        Assert.NotSame(first.Page, moved.Page);
        Assert.Same(first.Page, fillsRolledBackSpace.Page);
        Assert.Equal(2u, fillsRolledBackSpace.VertexOffset);
    }

    [Fact]
    public void PageAllocationCountIsLimitedByMaxDrawIndirectCount()
    {
        var device = new TestGraphicsDevice(1);
        var arena = new ChunkMeshArena(device, 8, 8);

        var first = arena.Allocate(CreateMesh(1, 1));
        var second = arena.Allocate(CreateMesh(1, 1));

        Assert.NotSame(first.Page, second.Page);
    }

    [Fact]
    public void OversizedMeshUsesDedicatedPageDestroyedOnRelease()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 4, 4);
        var allocation = arena.Allocate(CreateMesh(5, 2));

        arena.Release(allocation);

        Assert.True(allocation.Page.IsDedicated);
        Assert.True(allocation.Page.VertexBuffer.IsDestroyed);
        Assert.True(allocation.Page.IndexBuffer.IsDestroyed);
    }

    [Fact]
    public void UploadWithinCapacityPreservesPageOffsetsAndCapacities()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var allocation = arena.Allocate(CreateMesh(4, 4));

        arena.UploadInPlace(allocation, CreateMesh(2, 3));

        Assert.Equal(0u, allocation.VertexOffset);
        Assert.Equal(0u, allocation.FirstIndex);
        Assert.Equal(4u, allocation.VertexCapacity);
        Assert.Equal(4u, allocation.IndexCapacity);
        Assert.Equal(3u, allocation.IndexCount);
        Assert.Equal(4, device.Uploads.Count);
    }

    [Fact]
    public void ReplacementReleasesOnlyTheOldAllocation()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var oldAllocation = arena.Allocate(CreateMesh(2, 2));
        var otherAllocation = arena.Allocate(CreateMesh(2, 2));

        var replacement = arena.Allocate(CreateMesh(3, 3));
        arena.Release(oldAllocation);

        Assert.Same(oldAllocation.Page, replacement.Page);
        Assert.Same(oldAllocation.Page, otherAllocation.Page);
        Assert.Equal(2u, otherAllocation.VertexOffset);
        Assert.Equal(2u, otherAllocation.FirstIndex);
        Assert.Equal(2u, replacement.Page.AllocationCount);
        Assert.False(otherAllocation.Page.VertexBuffer.IsDestroyed);
    }

    [Fact]
    public void ReleasingLastRegularAllocationDestroysPage()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var allocation = arena.Allocate(CreateMesh(2, 2));

        arena.Release(allocation);

        Assert.True(allocation.Page.VertexBuffer.IsDestroyed);
        Assert.True(allocation.Page.IndexBuffer.IsDestroyed);
    }
    private static ChunkMesh CreateMesh(int vertexCount, int indexCount)
    {
        return new ChunkMesh(new ChunkVertex[vertexCount], new uint[indexCount]);
    }
}

internal sealed class TestGraphicsDevice(uint maxDrawIndirectCount) : GraphicsDevice
{
    internal List<TestBufferUpload> Uploads { get; } = [];

    public override uint MaxTextureDimension2D => 4096;

    public override uint MaxDrawIndirectCount { get; } = maxDrawIndirectCount;

    public override GraphicsBuffer CreateBuffer(in BufferDescription description)
    {
        return new TestBuffer(description.Size);
    }

    public override UploadHandle UploadBuffer<T>(
        GraphicsBuffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0)
    {
        Uploads.Add(new TestBufferUpload(destination, data.Length, destinationOffset));
        return TestUploadHandle.Instance;
    }

    public override Texture CreateTexture(in TextureDescription description) => throw new NotSupportedException();

    public override TextureView CreateTextureView(
        Texture texture,
        in TextureViewDescription description) => throw new NotSupportedException();

    public override UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data) => throw new NotSupportedException();

    public override UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data,
        in TextureUploadRegion region) => throw new NotSupportedException();

    public override UploadHandle GenerateMipmaps(Texture texture) => throw new NotSupportedException();

    public override Sampler CreateSampler(in SamplerDescription description) => throw new NotSupportedException();

    public override Shader CreateShader(in ShaderDescription description) => throw new NotSupportedException();

    public override DescriptorSetLayout CreateDescriptorSetLayout(
        in DescriptorSetLayoutDescription description) => throw new NotSupportedException();

    public override DescriptorSet CreateDescriptorSet(
        in DescriptorSetDescription description) => throw new NotSupportedException();

    public override ulong GetAlignedUniformSize(ulong rawSize) => throw new NotSupportedException();

    public override GraphicsPipeline CreateGraphicsPipeline(
        in GraphicsPipelineDescription description) => throw new NotSupportedException();

    public override void WaitIdle() => throw new NotSupportedException();
}

internal sealed class TestBuffer(ulong size) : GraphicsBuffer
{
    public override ulong Size { get; } = size;

    public override void Write<T>(in T value, ulong destinationOffset = 0)
    {
        throw new NotSupportedException();
    }

    public override void Write<T>(ReadOnlySpan<T> data, ulong destinationOffset = 0)
    {
        throw new NotSupportedException();
    }

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestUploadHandle : UploadHandle
{
    internal static readonly TestUploadHandle Instance = new();

    protected override UploadState State => UploadState.Ready;

    public override Exception? Exception => null;
}

internal readonly record struct TestBufferUpload(
    GraphicsBuffer Destination,
    int ElementCount,
    ulong DestinationOffset);
