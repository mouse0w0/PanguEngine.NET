using PanguEngine.Graphics;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMeshArena
{
    private const uint DefaultVertexCapacity = 16 * 1024 * 1024 / ChunkVertex.SizeInBytes;
    private const uint DefaultIndexCapacity = 4 * 1024 * 1024 / sizeof(uint);

    private readonly GraphicsDevice _device;
    private readonly uint _regularVertexCapacity;
    private readonly uint _regularIndexCapacity;
    private readonly List<ChunkMeshArenaPage> _pages = [];

    internal ChunkMeshArena(
        GraphicsDevice device,
        uint regularVertexCapacity = DefaultVertexCapacity,
        uint regularIndexCapacity = DefaultIndexCapacity)
    {
        _device = device;
        _regularVertexCapacity = regularVertexCapacity;
        _regularIndexCapacity = regularIndexCapacity;
    }

    internal ChunkMeshArenaAllocation Allocate(ChunkMesh mesh)
    {
        var vertexCount = checked((uint)mesh.VertexCount);
        var indexCount = checked((uint)mesh.IndexCount);

        if (vertexCount > _regularVertexCapacity || indexCount > _regularIndexCapacity)
        {
            var dedicatedPage = CreatePage(vertexCount, indexCount, true);
            return AllocateOnPage(dedicatedPage, mesh, vertexCount, indexCount);
        }

        foreach (var page in _pages)
        {
            if (page.IsDedicated || page.AllocationCount >= _device.MaxDrawIndirectCount)
                continue;
            if (TryAllocatePair(page, vertexCount, indexCount, out var vertexOffset, out var firstIndex))
                return CreateAllocation(page, mesh, vertexOffset, vertexCount, firstIndex, indexCount);
        }

        var newPage = CreatePage(_regularVertexCapacity, _regularIndexCapacity, false);
        return AllocateOnPage(newPage, mesh, vertexCount, indexCount);
    }

    internal void UploadInPlace(ChunkMeshArenaAllocation allocation, ChunkMesh mesh)
    {
        var vertexUpload = _device.UploadBuffer(
            allocation.Page.VertexBuffer,
            mesh.Vertices,
            checked((ulong)allocation.VertexOffset * ChunkVertex.SizeInBytes));
        var indexUpload = _device.UploadBuffer(
            allocation.Page.IndexBuffer,
            mesh.Indices,
            checked((ulong)allocation.FirstIndex * sizeof(uint)));
        allocation.SetUploadedMesh(mesh, vertexUpload, indexUpload);
    }

    internal void Release(ChunkMeshArenaAllocation allocation)
    {
        var page = allocation.Page;
        page.VertexFreeList.Free(allocation.VertexOffset, allocation.VertexCapacity);
        page.IndexFreeList.Free(allocation.FirstIndex, allocation.IndexCapacity);
        page.AllocationCount--;

        if (page.AllocationCount != 0)
            return;

        _pages.Remove(page);
        page.VertexBuffer.Destroy();
        page.IndexBuffer.Destroy();
    }

    internal void Destroy()
    {
        foreach (var page in _pages)
        {
            page.VertexBuffer.Destroy();
            page.IndexBuffer.Destroy();
        }

        _pages.Clear();
    }

    private ChunkMeshArenaAllocation AllocateOnPage(
        ChunkMeshArenaPage page,
        ChunkMesh mesh,
        uint vertexCount,
        uint indexCount)
    {
        if (!TryAllocatePair(page, vertexCount, indexCount, out var vertexOffset, out var firstIndex))
            throw new InvalidOperationException("A newly created chunk mesh arena page could not fit its allocation.");

        return CreateAllocation(page, mesh, vertexOffset, vertexCount, firstIndex, indexCount);
    }

    private ChunkMeshArenaAllocation CreateAllocation(
        ChunkMeshArenaPage page,
        ChunkMesh mesh,
        uint vertexOffset,
        uint vertexCapacity,
        uint firstIndex,
        uint indexCapacity)
    {
        var vertexUpload = _device.UploadBuffer(
            page.VertexBuffer,
            mesh.Vertices,
            checked((ulong)vertexOffset * ChunkVertex.SizeInBytes));
        var indexUpload = _device.UploadBuffer(
            page.IndexBuffer,
            mesh.Indices,
            checked((ulong)firstIndex * sizeof(uint)));
        page.AllocationCount++;
        return new ChunkMeshArenaAllocation(
            page,
            vertexOffset,
            vertexCapacity,
            firstIndex,
            indexCapacity,
            checked((uint)mesh.IndexCount),
            vertexUpload,
            indexUpload);
    }

    private ChunkMeshArenaPage CreatePage(uint vertexCapacity, uint indexCapacity, bool dedicated)
    {
        var physicalVertexCapacity = Math.Max(vertexCapacity, 1);
        var physicalIndexCapacity = Math.Max(indexCapacity, 1);
        var vertexBuffer = _device.CreateBuffer(new BufferDescription
        {
            Size = checked((ulong)physicalVertexCapacity * ChunkVertex.SizeInBytes),
            Usage = BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage = MemoryUsage.GpuOnly
        });

        GraphicsBuffer indexBuffer;
        try
        {
            indexBuffer = _device.CreateBuffer(new BufferDescription
            {
                Size = checked((ulong)physicalIndexCapacity * sizeof(uint)),
                Usage = BufferUsage.TransferDestination | BufferUsage.Index,
                MemoryUsage = MemoryUsage.GpuOnly
            });
        }
        catch
        {
            vertexBuffer.Destroy();
            throw;
        }

        var page = new ChunkMeshArenaPage(
            vertexBuffer,
            indexBuffer,
            vertexCapacity,
            indexCapacity,
            dedicated);
        _pages.Add(page);
        return page;
    }

    private static bool TryAllocatePair(
        ChunkMeshArenaPage page,
        uint vertexCount,
        uint indexCount,
        out uint vertexOffset,
        out uint firstIndex)
    {
        if (!page.VertexFreeList.TryAllocate(vertexCount, out vertexOffset))
        {
            firstIndex = 0;
            return false;
        }

        if (page.IndexFreeList.TryAllocate(indexCount, out firstIndex))
            return true;

        page.VertexFreeList.Free(vertexOffset, vertexCount);
        return false;
    }
}

internal sealed class ChunkMeshArenaPage
{
    internal ChunkMeshArenaPage(
        GraphicsBuffer vertexBuffer,
        GraphicsBuffer indexBuffer,
        uint vertexCapacity,
        uint indexCapacity,
        bool isDedicated)
    {
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        IsDedicated = isDedicated;
        VertexFreeList = new ChunkArenaFreeList(vertexCapacity);
        IndexFreeList = new ChunkArenaFreeList(indexCapacity);
    }

    internal GraphicsBuffer VertexBuffer { get; }

    internal GraphicsBuffer IndexBuffer { get; }

    internal bool IsDedicated { get; }

    internal uint AllocationCount { get; set; }

    internal ChunkArenaFreeList VertexFreeList { get; }

    internal ChunkArenaFreeList IndexFreeList { get; }
}

internal sealed class ChunkMeshArenaAllocation(
    ChunkMeshArenaPage page,
    uint vertexOffset,
    uint vertexCapacity,
    uint firstIndex,
    uint indexCapacity,
    uint indexCount,
    UploadHandle vertexUpload,
    UploadHandle indexUpload)
{
    internal ChunkMeshArenaPage Page { get; } = page;

    internal uint VertexOffset { get; } = vertexOffset;

    internal uint VertexCapacity { get; } = vertexCapacity;

    internal uint FirstIndex { get; } = firstIndex;

    internal uint IndexCapacity { get; } = indexCapacity;

    internal uint IndexCount { get; private set; } = indexCount;

    internal UploadHandle VertexUpload { get; private set; } = vertexUpload;

    internal UploadHandle IndexUpload { get; private set; } = indexUpload;

    internal bool CanFit(ChunkMesh mesh)
    {
        return checked((uint)mesh.VertexCount) <= VertexCapacity &&
               checked((uint)mesh.IndexCount) <= IndexCapacity;
    }

    internal void SetUploadedMesh(
        ChunkMesh mesh,
        UploadHandle newVertexUpload,
        UploadHandle newIndexUpload)
    {
        IndexCount = checked((uint)mesh.IndexCount);
        VertexUpload = newVertexUpload;
        IndexUpload = newIndexUpload;
    }
}

internal sealed class ChunkArenaFreeList
{
    private readonly List<ChunkArenaRange> _ranges = [];

    internal ChunkArenaFreeList(uint capacity)
    {
        if (capacity > 0)
            _ranges.Add(new ChunkArenaRange(0, capacity));
    }

    internal bool TryAllocate(uint length, out uint offset)
    {
        if (length == 0)
        {
            offset = 0;
            return true;
        }

        for (var i = 0; i < _ranges.Count; i++)
        {
            var range = _ranges[i];
            if (range.Length < length)
                continue;

            offset = range.Offset;
            if (range.Length == length)
                _ranges.RemoveAt(i);
            else
                _ranges[i] = new ChunkArenaRange(checked(range.Offset + length), range.Length - length);
            return true;
        }

        offset = 0;
        return false;
    }

    internal void Free(uint offset, uint length)
    {
        if (length == 0)
            return;

        var insertionIndex = 0;
        while (insertionIndex < _ranges.Count && _ranges[insertionIndex].Offset < offset)
            insertionIndex++;

        var mergedOffset = offset;
        var mergedLength = length;
        if (insertionIndex > 0)
        {
            var previous = _ranges[insertionIndex - 1];
            if (checked(previous.Offset + previous.Length) == offset)
            {
                mergedOffset = previous.Offset;
                mergedLength = checked(previous.Length + mergedLength);
                _ranges.RemoveAt(--insertionIndex);
            }
        }

        if (insertionIndex < _ranges.Count)
        {
            var next = _ranges[insertionIndex];
            if (checked(mergedOffset + mergedLength) == next.Offset)
            {
                mergedLength = checked(mergedLength + next.Length);
                _ranges.RemoveAt(insertionIndex);
            }
        }

        _ranges.Insert(insertionIndex, new ChunkArenaRange(mergedOffset, mergedLength));
    }
}

internal readonly record struct ChunkArenaRange(uint Offset, uint Length);
