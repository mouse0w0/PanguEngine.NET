namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanStagingLease
{
    private readonly List<VulkanStagingPage> _pages = [];

    internal VulkanStagingLease(VulkanStagingPagePool owner)
    {
        Owner = owner;
    }

    internal VulkanStagingPagePool Owner { get; }

    internal IReadOnlyList<VulkanStagingPage> Pages => _pages;

    internal VulkanStagingPage? CurrentRegularPage { get; set; }

    internal bool IsRecycled { get; private set; }

    internal void AddPage(VulkanStagingPage page)
    {
        _pages.Add(page);
    }

    internal void MarkRecycled()
    {
        IsRecycled = true;
    }
}
