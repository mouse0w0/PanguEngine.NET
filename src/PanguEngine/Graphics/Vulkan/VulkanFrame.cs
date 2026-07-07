namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Frame"/>.
/// </summary>
internal sealed class VulkanFrame : Frame
{
    private bool _valid;

    /// <summary>
    /// Creates a Vulkan graphics frame.
    /// </summary>
    /// <param name="imageIndex">The swapchain image index.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="colorOutput">The frame color output.</param>
    /// <param name="commandList">The command list for this frame.</param>
    internal VulkanFrame(
        uint imageIndex,
        uint width,
        uint height,
        VulkanSwapchainTexture colorOutput,
        VulkanCommandList commandList)
    {
        ImageIndex = imageIndex;
        Width = width;
        Height = height;
        VulkanColorOutput = colorOutput;
        VulkanCommandList = commandList;
        _valid = true;
    }

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
    public override Texture ColorOutput
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
    /// Gets the Vulkan color output for this frame.
    /// </summary>
    internal VulkanSwapchainTexture VulkanColorOutput { get; }

    /// <summary>
    /// Gets the Vulkan command list for this frame.
    /// </summary>
    internal VulkanCommandList VulkanCommandList { get; }

    /// <summary>
    /// Gets whether this frame is still active.
    /// </summary>
    internal bool IsValid => _valid;

    /// <summary>
    /// Invalidates the frame and its command list.
    /// </summary>
    internal void Invalidate()
    {
        _valid = false;
        VulkanCommandList.Invalidate();
    }

    /// <summary>
    /// Throws if the frame is no longer active.
    /// </summary>
    internal void EnsureValid()
    {
        if (!_valid)
            throw new InvalidOperationException("Graphics frame is no longer valid.");
    }
}