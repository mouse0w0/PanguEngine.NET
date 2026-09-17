using PanguEngine.Desktop;
using PanguEngine.Desktop.Sdl;
using PanguEngine.Tests.Windowing;

namespace PanguEngine.Tests.Client;

public sealed class SdlFileDialogServiceTests
{
    [Fact]
    public void NonSdlOwnerIsRejectedSynchronously()
    {
        var service = new SdlFileDialogService();

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = service.OpenFileAsync(new FileDialogOptions
            {
                Owner = new TestWindow()
            });
        });
    }
}
