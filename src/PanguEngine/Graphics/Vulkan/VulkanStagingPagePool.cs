using Silk.NET.Vulkan;
using Vma;

namespace PanguEngine.Graphics.Vulkan;

internal readonly unsafe struct VulkanStagingSegment
{
    internal VulkanStagingSegment(
        VulkanBuffer buffer,
        byte* destination,
        ulong offset,
        int pageIdentity)
    {
        Buffer = buffer;
        Destination = destination;
        Offset = offset;
        PageIdentity = pageIdentity;
    }

    internal VulkanBuffer Buffer { get; }

    internal byte* Destination { get; }

    internal ulong Offset { get; }

    internal int PageIdentity { get; }
}

internal sealed class VulkanStagingPageAllocationException : Exception
{
    internal VulkanStagingPageAllocationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class VulkanStagingPage
{
    private readonly Action<ulong, ulong> _flush;
    private readonly Action _destroy;

    internal VulkanStagingPage(
        VulkanBuffer buffer,
        nint mappedAddress,
        ulong capacity,
        bool dedicated,
        int regularCreationIndex,
        Action<ulong, ulong> flush,
        Action destroy)
    {
        Buffer = buffer;
        MappedAddress = mappedAddress;
        Capacity = capacity;
        Dedicated = dedicated;
        RegularCreationIndex = regularCreationIndex;
        _flush = flush;
        _destroy = destroy;
    }

    internal VulkanBuffer Buffer { get; }

    internal nint MappedAddress { get; }

    internal ulong Capacity { get; }

    internal bool Dedicated { get; }

    internal int RegularCreationIndex { get; }

    internal ulong Offset { get; set; }

    internal ulong WrittenEnd { get; set; }

    internal void FlushWrittenRange(ulong offset, ulong size)
    {
        _flush(offset, size);
    }

    internal void DestroyPage()
    {
        _destroy();
    }
}

internal sealed unsafe class VulkanStagingPagePool
{
    private const int MaxCachedRegularPages = 4;

    private readonly List<VulkanStagingPage> _pages = [];
    private readonly List<VulkanStagingPage> _idleRegularPages = new(MaxCachedRegularPages);
    private readonly Func<ulong, bool, int, VulkanStagingPage> _createPage;
    private int _nextRegularCreationIndex;

    internal VulkanStagingPagePool(ulong pageSize)
        : this(pageSize, CreateNativePage)
    {
    }

    internal VulkanStagingPagePool(
        ulong pageSize,
        Func<ulong, bool, int, VulkanStagingPage> createPage)
    {
        PageSize = pageSize;
        _createPage = createPage;
        var page = CreatePage(pageSize, false, _nextRegularCreationIndex);
        _nextRegularCreationIndex++;
        _pages.Add(page);
        _idleRegularPages.Add(page);
    }

    internal ulong PageSize { get; }

    internal VulkanStagingLease BeginBatch()
    {
        return new VulkanStagingLease(this);
    }

    internal VulkanStagingSegment Allocate(
        VulkanStagingLease lease,
        ulong size,
        ulong alignment)
    {
        ValidateActiveLease(lease);
        if (RequiresDedicatedPage(size, PageSize))
            return AllocateDedicated(lease, size);

        var page = lease.CurrentRegularPage;
        if (page == null ||
            !TryAllocateInPage(
                page.Capacity,
                page.Offset,
                size,
                alignment,
                out var segmentOffset,
                out var nextOffset))
        {
            page = AcquireRegularPage();
            lease.CurrentRegularPage = page;
            lease.AddPage(page);
            if (!TryAllocateInPage(
                    page.Capacity,
                    page.Offset,
                    size,
                    alignment,
                    out segmentOffset,
                    out nextOffset))
                throw new InvalidOperationException("A regular staging request does not fit in an empty page.");
        }

        page.Offset = nextOffset;
        page.WrittenEnd = Math.Max(page.WrittenEnd, nextOffset);
        return CreateSegment(page, segmentOffset);
    }

    internal void FlushWrittenRanges(VulkanStagingLease lease)
    {
        ValidateActiveLease(lease);
        foreach (var page in lease.Pages)
        {
            if (page.WrittenEnd > 0)
                page.FlushWrittenRange(0, page.WrittenEnd);
        }
    }

    internal void Recycle(VulkanStagingLease lease)
    {
        ValidateActiveLease(lease);
        lease.MarkRecycled();
        foreach (var page in lease.Pages)
        {
            page.Offset = 0;
            page.WrittenEnd = 0;
            if (page.Dedicated)
            {
                page.DestroyPage();
                _pages.Remove(page);
                continue;
            }

            if (_idleRegularPages.Count == MaxCachedRegularPages)
            {
                var oldest = _idleRegularPages[0];
                _idleRegularPages.RemoveAt(0);
                oldest.DestroyPage();
                _pages.Remove(oldest);
            }

            _idleRegularPages.Add(page);
        }
    }

    internal void Destroy()
    {
        foreach (var page in _pages)
            page.DestroyPage();

        _pages.Clear();
        _idleRegularPages.Clear();
    }

    internal static ulong AlignOffset(ulong offset, ulong alignment)
    {
        return checked((offset + alignment - 1) / alignment * alignment);
    }

    internal static bool RequiresDedicatedPage(ulong size, ulong pageSize)
    {
        return size > pageSize;
    }

    internal static bool TryAllocateInPage(
        ulong capacity,
        ulong currentOffset,
        ulong size,
        ulong alignment,
        out ulong segmentOffset,
        out ulong nextOffset)
    {
        var alignedOffset = AlignOffset(currentOffset, alignment);
        if (alignedOffset > capacity || size > capacity - alignedOffset)
        {
            segmentOffset = 0;
            nextOffset = currentOffset;
            return false;
        }

        segmentOffset = alignedOffset;
        nextOffset = alignedOffset + size;
        return true;
    }

    private VulkanStagingSegment AllocateDedicated(VulkanStagingLease lease, ulong size)
    {
        var page = CreatePage(size, true, -1);
        _pages.Add(page);
        lease.AddPage(page);
        page.Offset = size;
        page.WrittenEnd = size;
        return CreateSegment(page, 0);
    }

    private VulkanStagingPage AcquireRegularPage()
    {
        if (_idleRegularPages.Count > 0)
        {
            var index = _idleRegularPages.Count - 1;
            var page = _idleRegularPages[index];
            _idleRegularPages.RemoveAt(index);
            return page;
        }

        var createdPage = CreatePage(PageSize, false, _nextRegularCreationIndex);
        _nextRegularCreationIndex++;
        _pages.Add(createdPage);
        return createdPage;
    }

    private static VulkanStagingSegment CreateSegment(VulkanStagingPage page, ulong offset)
    {
        return new VulkanStagingSegment(
            page.Buffer,
            (byte*)page.MappedAddress + offset,
            offset,
            page.RegularCreationIndex);
    }

    private void ValidateActiveLease(VulkanStagingLease lease)
    {
        if (!ReferenceEquals(lease.Owner, this))
            throw new InvalidOperationException("The staging lease belongs to another page pool.");
        if (lease.IsRecycled)
            throw new InvalidOperationException("The staging lease has already been recycled.");
    }

    private VulkanStagingPage CreatePage(
        ulong capacity,
        bool dedicated,
        int regularCreationIndex)
    {
        return _createPage(capacity, dedicated, regularCreationIndex);
    }

    private static VulkanStagingPage CreateNativePage(
        ulong capacity,
        bool dedicated,
        int regularCreationIndex)
    {
        VulkanBuffer? buffer = null;
        try
        {
            BufferCreateInfo bufferInfo = new()
            {
                SType = StructureType.BufferCreateInfo,
                Size = capacity,
                Usage = BufferUsageFlags.TransferSrcBit,
                SharingMode = SharingMode.Exclusive
            };
            AllocationCreateInfo allocationInfo = new()
            {
                Usage = Vma.MemoryUsage.Auto,
                Flags = AllocationCreateFlags.HostAccessSequentialWriteBit |
                        AllocationCreateFlags.MappedBit
            };
            buffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocationInfo);
            var mapped = buffer.PersistentlyMapForWrite();
            return new VulkanStagingPage(
                buffer,
                (nint)mapped,
                capacity,
                dedicated,
                regularCreationIndex,
                buffer.Flush,
                buffer.Destroy);
        }
        catch (Exception exception)
        {
            buffer?.Destroy();
            throw new VulkanStagingPageAllocationException(
                $"Failed to create a {capacity}-byte staging page.",
                exception);
        }
    }
}
