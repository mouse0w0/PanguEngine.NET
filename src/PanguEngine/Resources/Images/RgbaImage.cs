namespace PanguEngine.Resources.Images;

internal sealed record RgbaImage(
    int Width,
    int Height,
    ReadOnlyMemory<byte> Pixels);