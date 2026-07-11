using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>Manages a Vulkan command pool and its allocated command buffers.</summary>
internal sealed unsafe class VulkanCommandPool
{
    /// <summary>Gets the underlying Vulkan command pool handle.</summary>
    internal CommandPool CommandPool { get; }

    /// <summary>Gets the command buffers allocated from this pool.</summary>
    internal CommandBuffer[] CommandBuffers { get; }

    /// <summary>Creates a command pool and allocates the specified number of primary command buffers.</summary>
    /// <param name="count">The number of command buffers to allocate.</param>
    internal VulkanCommandPool(uint count)
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = VulkanContext.GraphicsQueueFamily
        };

        if (VulkanContext.Vk.CreateCommandPool(VulkanContext.Device, in poolInfo, null, out var commandPool) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create command pool.");

        CommandPool = commandPool;
        try
        {
            CommandBuffers = AllocateCommandBuffers(count);
        }
        catch
        {
            VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, CommandPool, null);
            throw;
        }
    }

    /// <summary>Resets the command pool and all command buffers allocated from it.</summary>
    internal void Reset()
    {
        if (VulkanContext.Vk.ResetCommandPool(VulkanContext.Device, CommandPool, 0) != Result.Success)
            throw new InvalidOperationException("Failed to reset command pool.");
    }

    private CommandBuffer[] AllocateCommandBuffers(uint count)
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = count
        };

        var commandBuffers = new CommandBuffer[count];
        fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
        {
            if (VulkanContext.Vk.AllocateCommandBuffers(VulkanContext.Device, in allocInfo, commandBuffersPtr) !=
                Result.Success)
                throw new InvalidOperationException("Failed to allocate command buffers.");
        }

        return commandBuffers;
    }

    /// <summary>Destroys the command pool and releases associated Vulkan resources.</summary>
    internal void Destroy()
    {
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, CommandPool, null);
    }
}