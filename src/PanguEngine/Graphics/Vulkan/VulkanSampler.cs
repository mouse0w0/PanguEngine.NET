using VKSampler = Silk.NET.Vulkan.Sampler;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Sampler"/>.
/// </summary>
internal sealed unsafe class VulkanSampler : Sampler
{
    private bool _destroyed;

    /// <summary>
    /// The Vulkan sampler handle.
    /// </summary>
    internal VKSampler Handle { get; }

    /// <summary>
    /// Gets whether the sampler has been destroyed.
    /// </summary>
    public override bool IsDestroyed => _destroyed;

    internal VulkanSampler(VKSampler sampler)
    {
        Handle = sampler;
    }

    /// <summary>
    /// Destroys the sampler resource.
    /// </summary>
    public override void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        var sampler = Handle;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue,
            () => { VulkanContext.Vk.DestroySampler(VulkanContext.Device, sampler, null); });
    }
}