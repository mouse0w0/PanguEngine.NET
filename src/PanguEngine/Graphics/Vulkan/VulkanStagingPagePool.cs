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
    private readonly List<VulkanStagingPage> _pages = [];
    private readonly Func<ulong, bool, int, VulkanStagingPage> _createPage;
    private VulkanStagingPage? _currentRegularPage;
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
        var page = _createPage(pageSize, false, _nextRegularCreationIndex);
        _pages.Add(page);
        _currentRegularPage = page;
        _nextRegularCreationIndex++;
    }

    internal ulong PageSize { get; }

    internal VulkanStagingSegment Allocate(ulong size, ulong alignment)
    {
        if (RequiresDedicatedPage(size, PageSize))
        {
            var dedicatedPage = CreatePage(size, true, -1);
            _pages.Add(dedicatedPage);
            dedicatedPage.Offset = size;
            dedicatedPage.WrittenEnd = size;
            return new VulkanStagingSegment(
                dedicatedPage.Buffer,
                (byte*)dedicatedPage.MappedAddress,
                0,
                dedicatedPage.RegularCreationIndex);
        }

        var currentPage = _currentRegularPage ??
                          throw new InvalidOperationException("The staging page pool has no regular page.");
        if (!TryAllocateInPage(
                currentPage.Capacity,
                currentPage.Offset,
                size,
                alignment,
                out var segmentOffset,
                out var nextOffset))
        {
            var foundReusablePage = false;
            foreach (var reusablePage in _pages)
            {
                if (ReferenceEquals(reusablePage, currentPage) ||
                    !ShouldRetainPage(reusablePage.Dedicated, reusablePage.RegularCreationIndex))
                    continue;
                if (!TryAllocateInPage(
                        reusablePage.Capacity,
                        reusablePage.Offset,
                        size,
                        alignment,
                        out segmentOffset,
                        out nextOffset))
                    continue;

                currentPage = reusablePage;
                _currentRegularPage = reusablePage;
                foundReusablePage = true;
                break;
            }

            if (!foundReusablePage)
            {
                var newPage = CreatePage(PageSize, false, _nextRegularCreationIndex);
                _pages.Add(newPage);
                _currentRegularPage = newPage;
                _nextRegularCreationIndex++;
                currentPage = newPage;

                if (!TryAllocateInPage(
                        currentPage.Capacity,
                        currentPage.Offset,
                        size,
                        alignment,
                        out segmentOffset,
                        out nextOffset))
                    throw new InvalidOperationException("A regular staging request does not fit in an empty page.");
            }
        }

        currentPage.Offset = nextOffset;
        currentPage.WrittenEnd = Math.Max(currentPage.WrittenEnd, nextOffset);
        return new VulkanStagingSegment(
            currentPage.Buffer,
            (byte*)currentPage.MappedAddress + segmentOffset,
            segmentOffset,
            currentPage.RegularCreationIndex);
    }

    internal void FlushWrittenRanges()
    {
        foreach (var page in _pages)
        {
            if (page.WrittenEnd > 0)
                page.FlushWrittenRange(0, page.WrittenEnd);
        }
    }

    internal void CompleteSubmittedBatch()
    {
        RecycleBatchPages();
    }

    internal void ResetUnsubmittedBatch()
    {
        RecycleBatchPages();
    }

    internal void Destroy()
    {
        foreach (var page in _pages)
            page.DestroyPage();

        _pages.Clear();
        _currentRegularPage = null;
    }

    internal static ulong AlignOffset(ulong offset, ulong alignment)
    {
        return checked((offset + alignment - 1) / alignment * alignment);
    }

    internal static bool RequiresDedicatedPage(ulong size, ulong pageSize)
    {
        return size > pageSize;
    }

    internal static bool ShouldRetainPage(bool dedicated, int regularCreationIndex)
    {
        return !dedicated && regularCreationIndex < 2;
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

    private void RecycleBatchPages()
    {
        VulkanStagingPage? currentRegularPage = null;
        for (var index = _pages.Count - 1; index >= 0; index--)
        {
            var page = _pages[index];
            if (ShouldRetainPage(page.Dedicated, page.RegularCreationIndex))
            {
                page.Offset = 0;
                page.WrittenEnd = 0;
                currentRegularPage ??= page;
                continue;
            }

            page.DestroyPage();
            _pages.RemoveAt(index);
        }

        _currentRegularPage = currentRegularPage ??
                              throw new InvalidOperationException("The staging page pool lost all regular pages.");
    }
}