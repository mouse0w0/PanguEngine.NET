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
        Lifetime = new VulkanResourceLifetime(
            this,
            () => VulkanContext.Vk.DestroySampler(VulkanContext.Device, sampler, null),
            VulkanDeletionQueue.Enqueue);
    }

    internal VulkanResourceLifetime Lifetime { get; }

    /// <summary>
    /// Destroys the sampler resource.
    /// </summary>
    public override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (IsDestroyed) return;
        MarkDestroyed();
        Lifetime.RequestDestroy();
    }
}