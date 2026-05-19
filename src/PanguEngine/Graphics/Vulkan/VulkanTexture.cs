using Silk.NET.Vulkan;
using Vma;
using VkImage = Silk.NET.Vulkan.Image;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanTexture : Texture
{
    private readonly Allocation* _allocation;
    private bool _destroyed;
    private bool _uploadQueued;

    public VkImage Image { get; }

    public ImageView ImageView { get; }

    public override bool IsDestroyed => _destroyed;

    public override TextureFormat Format { get; }

    public override TextureDimension Dimension { get; }

    public override uint Width { get; }

    public override uint Height { get; }

    public override uint Depth { get; }

    public override uint MipLevels { get; }

    public override uint ArrayLayers { get; }

    public override TextureUsage Usage { get; }

    public ImageLayout CurrentLayout { get; private set; } = ImageLayout.Undefined;

    internal VulkanTexture(VkImage image, Allocation* allocation, ImageView imageView, TextureDimension dimension,
        TextureFormat format, uint width, uint height, uint depth, uint mipLevels, uint arrayLayers, TextureUsage usage)
    {
        Image = image;
        _allocation = allocation;
        ImageView = imageView;
        Dimension = dimension;
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        MipLevels = mipLevels;
        ArrayLayers = arrayLayers;
        Usage = usage;
    }

    public void MarkUploadQueued()
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        if (_uploadQueued)
            throw new InvalidOperationException("Texture already has a pending upload.");
        if (CurrentLayout != ImageLayout.Undefined)
            throw new InvalidOperationException("Texture has already been uploaded or transitioned.");
        _uploadQueued = true;
    }

    public void CompleteUpload(ImageLayout layout)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        CurrentLayout = layout;
        _uploadQueued = false;
    }

    public override void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        var image = Image;
        var imageView = ImageView;
        var allocation = _allocation;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue, () =>
        {
            VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null);
            VulkanAllocator.DestroyImage(image, allocation);
        });
    }
}