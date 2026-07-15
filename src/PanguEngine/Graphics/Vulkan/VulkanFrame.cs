namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Frame"/>.
/// </summary>
internal sealed class VulkanFrame : Frame
{
    /// <summary>
    /// Creates a Vulkan graphics frame.
    /// </summary>
    /// <param name="frameNumber">The frame number.</param>
    /// <param name="frameSlot">The in-flight frame slot.</param>
    /// <param name="imageIndex">The swapchain image index.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="colorOutput">The frame color output.</param>
    /// <param name="frameContext">The Vulkan resources assigned to this frame.</param>
    internal VulkanFrame(
        ulong frameNumber,
        uint frameSlot,
        uint imageIndex,
        uint width,
        uint height,
        VulkanSwapchainTextureView colorOutput,
        VulkanFrameContext frameContext)
    {
        FrameNumber = frameNumber;
        FrameSlot = frameSlot;
        ImageIndex = imageIndex;
        Width = width;
        Height = height;
        VulkanColorOutput = colorOutput;
        FrameContext = frameContext;
        IsValid = true;
    }

    /// <inheritdoc/>
    public override ulong FrameNumber { get; }

    /// <inheritdoc/>
    public override uint FrameSlot { get; }

    /// <inheritdoc/>
    public override CommandList CommandList
    {
        get
        {
            EnsureValid();
            return VulkanCommandList;
        }
    }

    /// <inheritdoc/>
    public override uint Width { get; }

    /// <inheritdoc/>
    public override uint Height { get; }

    /// <inheritdoc/>
    public override TextureView ColorOutput
    {
        get
        {
            EnsureValid();
            return VulkanColorOutput;
        }
    }

    /// <summary>
    /// Gets the swapchain image index.
    /// </summary>
    internal uint ImageIndex { get; }

    /// <summary>
    /// Gets the Vulkan resources assigned to this frame.
    /// </summary>
    internal VulkanFrameContext FrameContext { get; }

    /// <summary>
    /// Gets the Vulkan color output for this frame.
    /// </summary>
    internal VulkanSwapchainTextureView VulkanColorOutput { get; }

    /// <summary>
    /// Gets the Vulkan command list for this frame.
    /// </summary>
    internal VulkanCommandList VulkanCommandList => FrameContext.CommandList;

    /// <summary>
    /// Gets whether this frame is still active.
    /// </summary>
    internal bool IsValid { get; private set; }

    /// <summary>
    /// Invalidates the frame and its command list.
    /// </summary>
    internal void Invalidate()
    {
        IsValid = false;
        VulkanCommandList.Invalidate();
    }

    /// <summary>
    /// Throws if the frame is no longer active.
    /// </summary>
    internal void EnsureValid()
    {
        if (!IsValid)
            throw new InvalidOperationException("Graphics frame is no longer valid.");
    }
}