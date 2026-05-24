using Silk.NET.Vulkan;
using Vma;
using VkImage = Silk.NET.Vulkan.Image;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanTexture : Texture
{
    private readonly Allocation* _allocation;
    private readonly ImageLayout[] _subresourceLayouts;
    private bool _destroyed;

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

        var trackedSubresourceCount = dimension == TextureDimension.Type3D
            ? mipLevels
            : checked(mipLevels * arrayLayers);
        _subresourceLayouts = new ImageLayout[trackedSubresourceCount];
        Array.Fill(_subresourceLayouts, ImageLayout.Undefined);
    }

    public ImageLayout GetLayout(uint mipLevel, uint arrayLayer)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        return _subresourceLayouts[GetLayoutIndex(mipLevel, arrayLayer)];
    }

    public void SetLayout(uint mipLevel, uint arrayLayer, ImageLayout layout)
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(VulkanTexture));
        _subresourceLayouts[GetLayoutIndex(mipLevel, arrayLayer)] = layout;
    }

    private int GetLayoutIndex(uint mipLevel, uint arrayLayer)
    {
        if (mipLevel >= MipLevels)
            throw new ArgumentOutOfRangeException(nameof(mipLevel), "Texture mip level is out of range.");
        if (Dimension == TextureDimension.Type3D)
        {
            if (arrayLayer != 0)
                throw new ArgumentOutOfRangeException(nameof(arrayLayer), "3D textures do not have array layers.");
            return checked((int)mipLevel);
        }

        if (arrayLayer >= ArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(arrayLayer), "Texture array layer is out of range.");
        return checked((int)(mipLevel * ArrayLayers + arrayLayer));
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