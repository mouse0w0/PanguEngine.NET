using System.Text;
using PanguEngine.Client;

namespace PanguEngine.Tests.Client;

public sealed class ClipboardContentTests
{
    [Fact]
    public void ContentStartsEmpty()
    {
        var content = new ClipboardContent();

        Assert.Empty(content.Formats);
        Assert.Empty(content.CopyEntries());
    }

    [Fact]
    public void SetTextAndDataPreserveFormatOrder()
    {
        var content = new ClipboardContent();

        content.SetText("plain");
        content.SetData(ClipboardFormat.Png, [1, 2, 3]);
        content.SetData(ClipboardFormat.Png, [4]);

        Assert.Equal([ClipboardFormat.Text, ClipboardFormat.Png], content.Formats);
        var entries = content.CopyEntries();
        Assert.Equal(Encoding.UTF8.GetBytes("plain"), entries[0].Data);
        Assert.Equal(new byte[] { 4 }, entries[1].Data);
    }

    [Fact]
    public void SetDataCopiesInputAndEntrySnapshots()
    {
        var source = new byte[] { 1, 2, 3 };
        var content = new ClipboardContent();
        content.SetData(ClipboardFormat.Png, source);

        source[0] = 9;
        var first = content.CopyEntries();
        first[0].Data[1] = 8;
        var second = content.CopyEntries();

        Assert.Equal(new byte[] { 1, 2, 3 }, second[0].Data);
    }

    [Fact]
    public void EmptyTextRemovesOnlyTextFormat()
    {
        var content = new ClipboardContent();
        content.SetText("plain");
        content.SetData(ClipboardFormat.Png, [1]);

        content.SetText(string.Empty);

        Assert.Equal([ClipboardFormat.Png], content.Formats);
    }

    [Fact]
    public void ClearRemovesAllFormats()
    {
        var content = new ClipboardContent();
        content.SetText("plain");
        content.SetData(ClipboardFormat.Png, [1]);

        content.Clear();

        Assert.Empty(content.Formats);
        Assert.Empty(content.CopyEntries());
    }

    [Fact]
    public void SettersRejectNullArguments()
    {
        var content = new ClipboardContent();

        Assert.Throws<ArgumentNullException>(() => content.SetText(null!));
        Assert.Throws<ArgumentNullException>(() => content.SetData(null!, []));
    }
}
