using Silk.NET.Vulkan;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>Manages a Vulkan command pool and its allocated command buffers.</summary>
public sealed unsafe class VulkanCommandPool
{
    /// <summary>Gets the underlying Vulkan command pool handle.</summary>
    public CommandPool CommandPool { get; private set; }

    /// <summary>Gets the command buffers allocated from this pool.</summary>
    public CommandBuffer[] CommandBuffers { get; private set; }

    /// <summary>Creates a command pool and allocates command buffers using <see cref="VulkanContext.MaxFramesInFlight"/> as the buffer count.</summary>
    public VulkanCommandPool() : this(VulkanContext.MaxFramesInFlight)
    {
    }

    /// <summary>Creates a command pool and allocates the specified number of primary command buffers.</summary>
    /// <param name="count">The number of command buffers to allocate.</param>
    public VulkanCommandPool(uint count)
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = VulkanContext.GraphicsQueueFamily,
        };

        if (VulkanContext.Vk.CreateCommandPool(VulkanContext.Device, in poolInfo, null, out var commandPool) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create command pool.");

        CommandPool = commandPool;
        CommandBuffers = AllocateCommandBuffers(count);
    }

    private CommandBuffer[] AllocateCommandBuffers(uint count)
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = count,
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
    public void Destroy()
    {
        VulkanContext.Vk.DestroyCommandPool(VulkanContext.Device, CommandPool, null);
    }
}