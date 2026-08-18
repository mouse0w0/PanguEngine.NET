using System.Text;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

internal sealed class UiTextTestContext : IDisposable
{
    private const string SourceFamily = "Source Han Sans CN";
    private static readonly string FontPath = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "Fonts",
        "SourceHanSansCN-Regular.otf");

    internal UiTextTestContext()
    {
        TextServices.Initialize();
        try
        {
            using var stream = File.OpenRead(FontPath);
            var font = Assert.Single(FontManager.Register(stream, 0));
            FontManager.DefaultFont = font;
            DefaultFace = FontManager.Match(font);
        }
        catch
        {
            TextServices.Dispose();
            throw;
        }
    }

    internal FontManager FontManager => TextServices.FontManager;
    internal TextLayoutEngine LayoutEngine => TextServices.TextLayoutEngine;
    internal FontFace DefaultFace { get; }

    internal Font RegisterAlias(string familyName = "Testxx Han Sans CN")
    {
        var data = File.ReadAllBytes(FontPath);
        var source = Encoding.BigEndianUnicode.GetBytes(SourceFamily);
        var target = Encoding.BigEndianUnicode.GetBytes(familyName);
        Assert.Equal(source.Length, target.Length);
        var replacements = 0;
        for (var index = 0; index <= data.Length - source.Length; index++)
        {
            if (!data.AsSpan(index, source.Length).SequenceEqual(source))
                continue;
            target.CopyTo(data, index);
            replacements++;
            index += source.Length - 1;
        }
        Assert.NotEqual(0, replacements);

        using var stream = new MemoryStream(data, writable: false);
        return Assert.Single(FontManager.Register(stream, 0));
    }

    public void Dispose() => TextServices.Dispose();
}
