using PanguEngine.Client;
using PanguEngine.Desktop.Sdl;

namespace PanguEngine.Tests.Client;

public sealed class ClipboardTests
{
    [Fact]
    public void PublicOperationsUsePlatformCore()
    {
        var clipboard = new TestClipboard
        {
            AvailableFormats = [ClipboardFormat.Text, ClipboardFormat.Png],
            Text = "value",
            Data = [1, 2, 3]
        };

        Assert.Equal(clipboard.AvailableFormats, clipboard.Formats);
        Assert.True(clipboard.HasText);
        Assert.True(clipboard.Contains(ClipboardFormat.Png));
        Assert.True(clipboard.TryGetText(out var text));
        Assert.Equal("value", text);
        Assert.True(clipboard.TryGetData(ClipboardFormat.Png, out var data));
        Assert.Equal(new byte[] { 1, 2, 3 }, data);
    }

    [Fact]
    public void MissingValuesUseEmptyOutputs()
    {
        var clipboard = new TestClipboard();

        Assert.False(clipboard.HasText);
        Assert.False(clipboard.TryGetText(out var text));
        Assert.Equal(string.Empty, text);
        Assert.False(clipboard.TryGetData(ClipboardFormat.Png, out var data));
        Assert.Empty(data);
    }

    [Fact]
    public void EmptyTextIsNotReportedAsPresent()
    {
        var clipboard = new TestClipboard { Text = string.Empty };

        Assert.False(clipboard.HasText);
        Assert.False(clipboard.TryGetText(out var text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void EmptyWritesClearClipboard()
    {
        var clipboard = new TestClipboard();

        clipboard.SetText(string.Empty);
        clipboard.SetContent(new ClipboardContent());

        Assert.Equal(2, clipboard.ClearCount);
        Assert.Equal(0, clipboard.SetTextCount);
        Assert.Equal(0, clipboard.SetContentCount);
    }

    [Fact]
    public void NonEmptyWritesUsePlatformCore()
    {
        var clipboard = new TestClipboard();
        var content = new ClipboardContent();
        content.SetData(ClipboardFormat.Png, [1]);

        clipboard.SetText("value");
        clipboard.SetContent(content);

        Assert.Equal("value", clipboard.WrittenText);
        Assert.Equal([ClipboardFormat.Png], clipboard.WrittenEntries.Select(static entry => entry.Format));
    }

    [Fact]
    public void NullArgumentsAreRejectedBeforePlatformUse()
    {
        var clipboard = new TestClipboard();

        Assert.Throws<ArgumentNullException>(() => clipboard.Contains(null!));
        Assert.Throws<ArgumentNullException>(() => clipboard.TryGetData(null!, out _));
        Assert.Throws<ArgumentNullException>(() => clipboard.SetText(null!));
        Assert.Throws<ArgumentNullException>(() => clipboard.SetContent(null!));
        Assert.Equal(0, clipboard.CallCount);
    }

    [Fact]
    public void PlatformFailuresAreNotTranslated()
    {
        var expected = new InvalidOperationException("clipboard");
        var clipboard = new TestClipboard { Exception = expected };

        var actual = Assert.Throws<InvalidOperationException>(() => clipboard.SetText("value"));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void DestroyIsIdempotentAndRejectsLaterOperations()
    {
        var clipboard = new TestClipboard();

        clipboard.Destroy();
        clipboard.Destroy();

        Assert.Equal(1, clipboard.DestroyCount);
        Assert.Throws<ObjectDisposedException>(() => clipboard.HasText);
        Assert.Throws<ObjectDisposedException>(() => clipboard.SetText("value"));
        Assert.Throws<ObjectDisposedException>(() => clipboard.Contains(null!));
        Assert.Throws<ObjectDisposedException>(() => clipboard.TryGetData(null!, out _));
        Assert.Throws<ObjectDisposedException>(() => clipboard.SetText(null!));
        Assert.Throws<ObjectDisposedException>(() => clipboard.SetContent(null!));
    }

    [Fact]
    public void DestroyFailureStillTransitionsToTerminalState()
    {
        var expected = new InvalidOperationException("cleanup");
        var clipboard = new TestClipboard { DestroyException = expected };

        var actual = Assert.Throws<InvalidOperationException>(clipboard.Destroy);
        clipboard.Destroy();

        Assert.Same(expected, actual);
        Assert.Equal(1, clipboard.DestroyCount);
        Assert.Throws<ObjectDisposedException>(() => clipboard.Clear());
    }

    [Fact]
    public void SdlClipboardChecksMainThreadBeforeNativeCalls()
    {
        var clipboard = new SdlClipboard();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            failure = Record.Exception(() =>
            {
                _ = clipboard.HasText;
            });
        });

        thread.Start();
        thread.Join();

        var exception = Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("SDL main thread", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestClipboard : Clipboard
    {
        private IReadOnlyList<ClipboardFormat> _availableFormats = [];

        internal IReadOnlyList<ClipboardFormat> AvailableFormats
        {
            get => _availableFormats;
            init => _availableFormats = value;
        }

        internal string? Text { get; init; }

        internal byte[]? Data { get; init; }

        internal string? WrittenText { get; private set; }

        internal IReadOnlyList<ClipboardContentEntry> WrittenEntries { get; private set; } = [];

        internal Exception? Exception { get; init; }

        internal Exception? DestroyException { get; init; }

        internal int CallCount { get; private set; }

        internal int ClearCount { get; private set; }

        internal int DestroyCount { get; private set; }

        internal int SetTextCount { get; private set; }

        internal int SetContentCount { get; private set; }

        protected override IReadOnlyList<ClipboardFormat> GetFormatsCore()
        {
            RecordCall();
            return _availableFormats;
        }

        protected override bool TryGetTextCore(out string text)
        {
            RecordCall();
            text = Text ?? string.Empty;
            return Text is not null;
        }

        protected override bool TryGetDataCore(ClipboardFormat format, out byte[] data)
        {
            RecordCall();
            data = Data?.ToArray() ?? [9];
            return Data is not null;
        }

        protected override void SetTextCore(string text)
        {
            RecordCall();
            SetTextCount++;
            WrittenText = text;
        }

        protected override void SetContentCore(ClipboardContent content)
        {
            RecordCall();
            SetContentCount++;
            WrittenEntries = content.CopyEntries();
        }

        protected override void ClearCore()
        {
            RecordCall();
            ClearCount++;
        }

        protected override void DestroyCore()
        {
            DestroyCount++;
            if (DestroyException is not null)
                throw DestroyException;
        }

        private void RecordCall()
        {
            CallCount++;
            if (Exception is not null)
                throw Exception;
        }
    }
}
