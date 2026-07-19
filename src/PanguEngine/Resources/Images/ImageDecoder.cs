using StbImageSharp;

namespace PanguEngine.Resources.Images;

internal static class ImageDecoder
{
    internal static RgbaImage Decode(Stream stream)
    {
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var expectedLength = checked((long)image.Width * image.Height * 4);
        if (image.Data.LongLength != expectedLength)
            throw new InvalidDataException("Decoded image returned an invalid RGBA pixel length.");

        return new RgbaImage(image.Width, image.Height, image.Data);
    }
}