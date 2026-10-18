using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.UI;

public sealed class ImageAtlasTests
{
    [Fact]
    public void SmallImagesSharePageAndUploadExactRegionsWithoutPadding()
    {
        using var context = new AtlasContext(frameSlotCount: 1);

        var first = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(4, 5)));
        var second = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(3, 2)));

        Assert.Same(first.Page, second.Page);
        Assert.Equal((4u, 5u), (first.Region.Width, first.Region.Height));
        var uploads = context.Device.Uploads.Where(upload => upload.Region is not null).ToArray();
        Assert.Equal(2, uploads.Length);
        Assert.Equal((4u, 5u), (uploads[0].Region!.Value.Width, uploads[0].Region!.Value.Height));
        Assert.Equal(4 * 5 * 4, uploads[0].Data.Length);
    }

    [Fact]
    public void RegionWaitsForFramesAndUploadCompletionBeforeFreeing()
    {
        var upload = TestUploadHandle.Ready();
        using var context = new AtlasContext(frameSlotCount: 2, upload);
        var entry = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(4, 4)));
        context.Atlas.Retire(entry);

        context.Atlas.AdvanceFrame(0);
        context.Atlas.AdvanceFrame(1);
        Assert.Equal(1, entry.Page.RetiringRegionCount);

        upload.SetSucceeded();
        context.Atlas.AdvanceFrame(0);
        Assert.Equal(0, entry.Page.RetiringRegionCount);
    }

    [Fact]
    public void ExtraEmptyPageRetiresItsTextureSlotAndResources()
    {
        using var context = new AtlasContext(frameSlotCount: 1);
        var first = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(1024, 1024)));
        var second = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(1024, 1024)));

        context.Atlas.Retire(first);
        context.Atlas.Retire(second);
        context.Atlas.AdvanceFrame(0);
        context.Table.SynchronizeFrame(0);

        Assert.False(first.Page.Texture.IsDestroyed);
        Assert.True(second.Page.Texture.IsDestroyed);
        Assert.True(second.Page.TextureView.IsDestroyed);
    }

    [Fact]
    public void SynchronousUploadFailureIsStoredOnOnlyItsEntry()
    {
        using var context = new AtlasContext(frameSlotCount: 1);
        var expected = new InvalidOperationException("upload");
        context.Device.UploadException = expected;

        var failed = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(4, 4)));
        Assert.True(failed.TryObserveUploadFailure(out var failure, out var firstObservation));
        Assert.Same(expected, failure);
        Assert.True(firstObservation);

        context.Device.UploadException = null;
        var ready = Assert.IsType<ImageAtlasEntry>(context.Atlas.TryCreate(Image(4, 4)));
        Assert.True(ready.IsUploadReady);
    }

    [Fact]
    public void CapacityFailureRetriesAfterTextureSlotRetires()
    {
        using var context = new AtlasContext(frameSlotCount: 1);
        TextureSlot lastSlot = default;
        for (var index = 0; index < TextureTable.SlotCount; index++)
        {
            Assert.True(context.Table.TryRegister(context.Device.CreateSampledView(), out lastSlot));
        }

        var textureCount = context.Device.Textures.Count;

        Assert.Null(context.Atlas.TryCreate(Image(4, 4)));
        Assert.Null(context.Atlas.TryCreate(Image(4, 4)));
        Assert.Equal(textureCount, context.Device.Textures.Count);

        context.Table.Retire(lastSlot, () => { });
        context.Table.SynchronizeFrame(0);
        Assert.NotNull(context.Atlas.TryCreate(Image(4, 4)));
    }

    [Fact]
    public void DestroyFailureStopsBeforeBackingTextureAndRemainingPages()
    {
        var device = new GraphicsTestDevice();
        var table = new TextureTable(device, new TestDescriptorSetLayout(default), 1);
        var atlas = new ImageAtlas(device, table);
        var first = Assert.IsType<ImageAtlasEntry>(atlas.TryCreate(Image(1024, 1024)));
        var second = Assert.IsType<ImageAtlasEntry>(atlas.TryCreate(Image(1024, 1024)));
        var expected = new InvalidOperationException("view destroy failed");
        Assert.IsType<TestTextureView>(second.Page.TextureView).DestroyException = expected;

        var actual = Assert.Throws<InvalidOperationException>(atlas.Destroy);

        Assert.Same(expected, actual);
        Assert.False(second.Page.Texture.IsDestroyed);
        Assert.False(first.Page.TextureView.IsDestroyed);
        Assert.False(first.Page.Texture.IsDestroyed);
    }

    private static Image Image(int width, int height) =>
        PanguEngine.Client.UI.Drawing.Image.FromRgba(new byte[checked(width * height * 4)], width, height);

    private sealed class AtlasContext : IDisposable
    {
        internal AtlasContext(uint frameSlotCount, UploadHandle? upload = null)
        {
            Device = new GraphicsTestDevice();
            if (upload is not null)
                Device.UploadHandle = upload;
            Table = new TextureTable(Device, new TestDescriptorSetLayout(default), frameSlotCount);
            Atlas = new ImageAtlas(Device, Table);
        }

        internal GraphicsTestDevice Device { get; }
        internal TextureTable Table { get; }
        internal ImageAtlas Atlas { get; }

        public void Dispose()
        {
            Table.DestroyDescriptorSets();
            Atlas.Destroy();
            Table.DestroyOwnedResources();
        }
    }
}
