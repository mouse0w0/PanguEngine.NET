using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

internal readonly record struct VulkanSubresourceLayout(
    uint MipLevel,
    uint ArrayLayer,
    ImageLayout Layout);

internal sealed class VulkanUploadLayoutState
{
    private readonly ImageLayout[] _layouts;

    internal VulkanUploadLayoutState(
        uint mipLevels,
        uint arrayLayers,
        Func<uint, uint, ImageLayout> getInitialLayout)
    {
        MipLevels = mipLevels;
        ArrayLayers = arrayLayers;
        _layouts = new ImageLayout[checked((int)mipLevels * (int)arrayLayers)];
        for (var mip = 0u; mip < mipLevels; mip++)
        {
            for (var layer = 0u; layer < arrayLayers; layer++)
                _layouts[GetIndex(mip, layer)] = getInitialLayout(mip, layer);
        }
    }

    private VulkanUploadLayoutState(
        uint mipLevels,
        uint arrayLayers,
        ImageLayout[] layouts)
    {
        MipLevels = mipLevels;
        ArrayLayers = arrayLayers;
        _layouts = layouts;
    }

    internal uint MipLevels { get; }

    internal uint ArrayLayers { get; }

    internal ImageLayout Get(uint mipLevel, uint arrayLayer)
    {
        return _layouts[GetIndex(mipLevel, arrayLayer)];
    }

    internal void Set(uint mipLevel, uint arrayLayer, ImageLayout layout)
    {
        _layouts[GetIndex(mipLevel, arrayLayer)] = layout;
    }

    internal VulkanUploadLayoutState Clone()
    {
        return new VulkanUploadLayoutState(MipLevels, ArrayLayers, (ImageLayout[])_layouts.Clone());
    }

    internal void Merge(VulkanUploadLayoutState transaction)
    {
        if (transaction.MipLevels != MipLevels || transaction.ArrayLayers != ArrayLayers)
            throw new InvalidOperationException("Upload layout transaction shape does not match.");

        transaction._layouts.CopyTo(_layouts, 0);
    }

    internal bool AreAllBaseMipsInitialized()
    {
        for (var layer = 0u; layer < ArrayLayers; layer++)
        {
            if (Get(0, layer) == ImageLayout.Undefined)
                return false;
        }

        return true;
    }

    internal IEnumerable<VulkanSubresourceLayout> EnumerateLayouts()
    {
        for (var mip = 0u; mip < MipLevels; mip++)
        {
            for (var layer = 0u; layer < ArrayLayers; layer++)
                yield return new VulkanSubresourceLayout(mip, layer, Get(mip, layer));
        }
    }

    private int GetIndex(uint mipLevel, uint arrayLayer)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(mipLevel, MipLevels);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrayLayer, ArrayLayers);

        return checked((int)(mipLevel * ArrayLayers + arrayLayer));
    }
}