using VKSampler = Silk.NET.Vulkan.Sampler;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Sampler"/>.
/// </summary>
internal sealed unsafe class VulkanSampler : Sampler
{
    /// <summary>
    /// The Vulkan sampler handle.
    /// </summary>
    internal VKSampler Handle { get; }

    internal VulkanSampler(VKSampler sampler)
    {
        Handle = sampler;
    }

    /// <summary>
    /// Destroys the sampler resource.
    /// </summary>
    public override void Destroy()
    {
        if (IsDestroyed) return;
        MarkDestroyed();

        var sampler = Handle;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue,
            () => { VulkanContext.Vk.DestroySampler(VulkanContext.Device, sampler, null); });
    }
}