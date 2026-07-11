using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>Manages a Vulkan command pool.</summary>
internal sealed unsafe class VulkanCommandPool
{
    /// <summary>Gets the underlying Vulkan command pool handle.</summary>
    internal CommandPool CommandPool { get; }

    /// <summary>Creates a Vulkan command pool.</summary>
    internal VulkanCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = VulkanContext.GraphicsQueueFamily
        };

        if (VulkanContext.Vk.CreateCommandPool(VulkanContext.Device, in poolInfo, null, out var commandPool) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create command pool.");

        CommandPool = commandPool;
    }

    /// <summary>Allocates one primary command buffer from this command pool.</summary>
    /// <returns>The allocated command buffer.</returns>
    internal CommandBuffer AllocateCommandBuffer()
    {
        CommandBuffer commandBuffer = default;
        AllocateCommandBuffers(&commandBuffer, 1);
        return commandBuffer;
    }

    /// <summary>Allocates primary command buffers from this command pool.</summary>
    /// <param name="count">The number of command buffers to allocate.</param>
    /// <returns>The allocated command buffers.</returns>
    internal CommandBuffer[] AllocateCommandBuffers(uint count)
    {
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count), "At least one command buffer must be allocated.");

        var commandBuffers = new CommandBuffer[count];
        fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
            AllocateCommandBuffers(commandBuffersPtr, count);

        return commandBuffers;
    }

    /// <summary>Resets the command pool and all command buffers allocated from it.</summary>
    internal void Reset()
    {
        if (VulkanContext.Vk.ResetCommandPool(VulkanContext.Device, CommandPool, 0) != Result.Success)
            throw new InvalidOperationException("Failed to reset command pool.");
    }

    private void AllocateCommandBuffers(CommandBuffer* commandBuffers, uint count)
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = count
        };

        if (VulkanContext.Vk.AllocateCommandBuffers(VulkanContext.Device, in allocInfo, commandBuffers) !=
            Result.Success)
            throw new InvalidOperationException("Failed to allocate command buffers.");
    }

    /// <summary>Destroys the command pool and releases associated Vulkan resources.</summary>
    internal void Destroy()
    {
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, CommandPool, null);
    }
}