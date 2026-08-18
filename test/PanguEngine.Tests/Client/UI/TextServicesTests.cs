using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class TextServicesTests
{
    [Fact]
    public void ServicesRequireInitialization()
    {
        Assert.Throws<InvalidOperationException>(() => TextServices.FontManager);
        Assert.Throws<InvalidOperationException>(() => TextServices.TextLayoutEngine);
    }

    [Fact]
    public void InitializeCreatesOneStableServicePair()
    {
        TextServices.Initialize();
        try
        {
            var fontManager = TextServices.FontManager;
            var textLayoutEngine = TextServices.TextLayoutEngine;

            Assert.Throws<InvalidOperationException>(TextServices.Initialize);
            Assert.Same(fontManager, TextServices.FontManager);
            Assert.Same(textLayoutEngine, TextServices.TextLayoutEngine);
        }
        finally
        {
            TextServices.Dispose();
        }
    }

    [Fact]
    public void DisposeClearsAndReleasesServices()
    {
        TextServices.Initialize();
        var fontManager = TextServices.FontManager;

        TextServices.Dispose();
        TextServices.Dispose();

        Assert.Throws<InvalidOperationException>(() => TextServices.FontManager);
        Assert.Throws<InvalidOperationException>(() => TextServices.TextLayoutEngine);
        Assert.Throws<ObjectDisposedException>(() => fontManager.Fonts);
    }
}
