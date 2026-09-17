using PanguEngine.Desktop;
using PanguEngine.Tests.Windowing;

namespace PanguEngine.Tests.Client;

public sealed class FileDialogServiceTests
{
    [Fact]
    public async Task PublicOperationsUseMatchingPlatformCore()
    {
        var service = new TestFileDialogService();

        Assert.Equal("open", await service.OpenFileAsync());
        Assert.Equal(["open-1", "open-2"], await service.OpenFilesAsync());
        Assert.Equal("save", await service.SaveFileAsync());
        Assert.Equal("folder", await service.OpenFolderAsync());
        Assert.Equal(["folder-1", "folder-2"], await service.OpenFoldersAsync());
        Assert.Equal(5, service.CallCount);
    }

    [Fact]
    public async Task CancellationValuesPassThroughPlatformCore()
    {
        var service = new TestFileDialogService
        {
            OpenFileResult = null,
            OpenFilesResult = [],
            SaveFileResult = null,
            OpenFolderResult = null,
            OpenFoldersResult = []
        };

        Assert.Null(await service.OpenFileAsync());
        Assert.Empty(await service.OpenFilesAsync());
        Assert.Null(await service.SaveFileAsync());
        Assert.Null(await service.OpenFolderAsync());
        Assert.Empty(await service.OpenFoldersAsync());
    }

    [Fact]
    public async Task PlatformFailureRemainsInReturnedTask()
    {
        var expected = new InvalidOperationException("dialog");
        var service = new TestFileDialogService { Exception = expected };

        var task = service.OpenFileAsync();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void DestroyIsIdempotentAndRejectsLaterOperationsSynchronously()
    {
        var service = new TestFileDialogService();

        service.Destroy();
        service.Destroy();

        Assert.Equal(1, service.DestroyCount);
        Assert.Throws<ObjectDisposedException>(() => { _ = service.OpenFileAsync(); });
        Assert.Throws<ObjectDisposedException>(() => { _ = service.OpenFilesAsync(); });
        Assert.Throws<ObjectDisposedException>(() => { _ = service.SaveFileAsync(); });
        Assert.Throws<ObjectDisposedException>(() => { _ = service.OpenFolderAsync(); });
        Assert.Throws<ObjectDisposedException>(() => { _ = service.OpenFoldersAsync(); });
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public void OperationsFromAnotherThreadAreRejectedSynchronously()
    {
        var service = new TestFileDialogService();
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(() => { _ = service.OpenFileAsync(); }));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public void DestroyedOwnerIsRejectedSynchronously()
    {
        var service = new TestFileDialogService();
        var owner = new TestWindow();
        owner.Destroy();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = service.OpenFileAsync(new FileDialogOptions
            {
                Owner = owner
            });
        });
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public void FileOptionsAreSnapshottedBeforePlatformCore()
    {
        var filters = new List<FileDialogFilter> { new("Images", "png") };
        var options = new FileDialogOptions
        {
            Title = "Open",
            InitialLocation = "C:\\assets",
            Filters = filters
        };
        var service = new TestFileDialogService();

        _ = service.OpenFilesAsync(options);
        filters.Add(new FileDialogFilter("Text", "txt"));

        Assert.NotSame(options, service.LastFileOptions);
        Assert.Equal("Open", service.LastFileOptions!.Title);
        Assert.Equal("C:\\assets", service.LastFileOptions.InitialLocation);
        Assert.Single(service.LastFileOptions.Filters);
    }

    [Fact]
    public void FolderOptionsAreSnapshottedBeforePlatformCore()
    {
        var owner = new TestWindow();
        var options = new FolderDialogOptions
        {
            Owner = owner,
            Title = "Folder",
            InitialLocation = "C:\\assets"
        };
        var service = new TestFileDialogService();

        _ = service.OpenFolderAsync(options);

        Assert.NotSame(options, service.LastFolderOptions);
        Assert.Same(owner, service.LastFolderOptions!.Owner);
        Assert.Equal("Folder", service.LastFolderOptions.Title);
        Assert.Equal("C:\\assets", service.LastFolderOptions.InitialLocation);
    }

    private sealed class TestFileDialogService : FileDialogService
    {
        internal string? OpenFileResult { get; init; } = "open";
        internal IReadOnlyList<string> OpenFilesResult { get; init; } = ["open-1", "open-2"];
        internal string? SaveFileResult { get; init; } = "save";
        internal string? OpenFolderResult { get; init; } = "folder";
        internal IReadOnlyList<string> OpenFoldersResult { get; init; } = ["folder-1", "folder-2"];
        internal Exception? Exception { get; init; }
        internal FileDialogOptions? LastFileOptions { get; private set; }
        internal FolderDialogOptions? LastFolderOptions { get; private set; }
        internal int CallCount { get; private set; }
        internal int DestroyCount { get; private set; }

        protected override bool HasPendingNativeRequestsCore => false;

        protected override Task<string?> OpenFileCoreAsync(FileDialogOptions options)
        {
            RecordFileCall(options);
            return CreateTask(OpenFileResult);
        }

        protected override Task<IReadOnlyList<string>> OpenFilesCoreAsync(FileDialogOptions options)
        {
            RecordFileCall(options);
            return CreateTask(OpenFilesResult);
        }

        protected override Task<string?> SaveFileCoreAsync(FileDialogOptions options)
        {
            RecordFileCall(options);
            return CreateTask(SaveFileResult);
        }

        protected override Task<string?> OpenFolderCoreAsync(FolderDialogOptions options)
        {
            RecordFolderCall(options);
            return CreateTask(OpenFolderResult);
        }

        protected override Task<IReadOnlyList<string>> OpenFoldersCoreAsync(FolderDialogOptions options)
        {
            RecordFolderCall(options);
            return CreateTask(OpenFoldersResult);
        }

        protected override void DestroyCore()
        {
            DestroyCount++;
        }

        private Task<T> CreateTask<T>(T result) => Exception is null
            ? Task.FromResult(result)
            : Task.FromException<T>(Exception);

        private void RecordFileCall(FileDialogOptions options)
        {
            CallCount++;
            LastFileOptions = options;
        }

        private void RecordFolderCall(FolderDialogOptions options)
        {
            CallCount++;
            LastFolderOptions = options;
        }
    }
}
