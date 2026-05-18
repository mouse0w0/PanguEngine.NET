using Silk.NET.Vulkan;
using Vma;
using VkImage = Silk.NET.Vulkan.Image;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanTexture2D : Texture2D
{
    private readonly Allocation* _allocation;
    private bool _destroyed;
    private bool _uploadQueued;

    public VkImage Image { get; }

    public ImageView ImageView { get; }

    public override bool IsDestroyed => _destroyed;

    public override TextureFormat Format { get; }

    public override uint Width { get; }

    public override uint Height { get; }

    public override uint MipLevels { get; }

    public override TextureUsage Usage { get; }

    public ImageLayout CurrentLayout { get; private set; } = ImageLayout.Undefined;

    internal VulkanTexture2D(VkImage image, Allocation* allocation, ImageView imageView, TextureFormat format,
        uint width, uint height, uint mipLevels, TextureUsage usage)
    {
        Image = image;
        _allocation = allocation;
        ImageView = imageView;
        Format = format;
        Width = width;
        Height = height;
        MipLevels = mipLevels;
        Usage = usage;
    }

    public void MarkUploadQueued()
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture2D));
        if (_uploadQueued)
            throw new InvalidOperationException("Texture already has a pending upload.");
        if (CurrentLayout != ImageLayout.Undefined)
            throw new InvalidOperationException("Texture has already been uploaded or transitioned.");
        _uploadQueued = true;
    }

    public void CompleteUpload(ImageLayout layout)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture2D));
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