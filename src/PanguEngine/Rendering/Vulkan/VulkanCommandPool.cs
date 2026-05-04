using Silk.NET.Vulkan;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>Manages a Vulkan command pool and its allocated command buffers.</summary>
public sealed unsafe class VulkanCommandPool
{
    private readonly VulkanContext _context;

    /// <summary>Gets the underlying Vulkan command pool handle.</summary>
    public CommandPool CommandPool { get; private set; }
    /// <summary>Gets the command buffers allocated from this pool.</summary>
    public CommandBuffer[] CommandBuffers { get; private set; }

    /// <summary>Creates a command pool and allocates command buffers using <see cref="VulkanContext.MaxFramesInFlight"/> as the buffer count.</summary>
    /// <param name="context">The Vulkan context used for device and queue family information.</param>
    public VulkanCommandPool(VulkanContext context) : this(context, (uint)context.MaxFramesInFlight)
    {
    }

    /// <summary>Creates a command pool and allocates the specified number of primary command buffers.</summary>
    /// <param name="context">The Vulkan context used for device and queue family information.</param>
    /// <param name="count">The number of command buffers to allocate.</param>
    public VulkanCommandPool(VulkanContext context, uint count)
    {
        _context = context;

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = context.GraphicsQueueFamily,
        };

        if (context.Vk.CreateCommandPool(context.Device, in poolInfo, null, out var commandPool) != Result.Success)
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
            if (_context.Vk.AllocateCommandBuffers(_context.Device, in allocInfo, commandBuffersPtr) != Result.Success)
                throw new InvalidOperationException("Failed to allocate command buffers.");
        }

        return commandBuffers;
    }

    /// <summary>Destroys the command pool and releases associated Vulkan resources.</summary>
    public void Destroy()
    {
        _context.Vk.DestroyCommandPool(_context.Device, CommandPool, null);
    }
}