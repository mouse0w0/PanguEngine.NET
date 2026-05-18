namespace PanguEngine.Graphics.Vulkan;

internal readonly record struct TextureUploadRegion(
    uint X,
    uint Y,
    uint Z,
    uint Width,
    uint Height,
    uint Depth,
    uint MipLevel,
    uint ArrayLayer);