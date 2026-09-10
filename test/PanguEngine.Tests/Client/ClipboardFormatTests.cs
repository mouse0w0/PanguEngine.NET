using PanguEngine.Client;

namespace PanguEngine.Tests.Client;

public sealed class ClipboardFormatTests
{
    [Fact]
    public void StandardFormatsUseExpectedMimeTypes()
    {
        Assert.Equal("text/plain;charset=utf-8", ClipboardFormat.Text.MimeType);
        Assert.Equal("text/html", ClipboardFormat.Html.MimeType);
        Assert.Equal("text/rtf", ClipboardFormat.Rtf.MimeType);
        Assert.Equal("text/uri-list", ClipboardFormat.UriList.MimeType);
        Assert.Equal("image/bmp", ClipboardFormat.Bmp.MimeType);
        Assert.Equal("image/png", ClipboardFormat.Png.MimeType);
        Assert.Equal("image/jpeg", ClipboardFormat.Jpeg.MimeType);
    }

    [Fact]
    public void ConstructorRequiresNonEmptyMimeType()
    {
        Assert.Throws<ArgumentNullException>(() => new ClipboardFormat(null!));
        Assert.Throws<ArgumentException>(() => new ClipboardFormat(string.Empty));
    }

    [Fact]
    public void FormatsUseMimeTypeValueEquality()
    {
        Assert.Equal(ClipboardFormat.Png, new ClipboardFormat("image/png"));
        Assert.NotEqual(ClipboardFormat.Png, new ClipboardFormat("IMAGE/PNG"));
    }
}
