using Microsoft.Extensions.Logging;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

[Collection(UiToolkitCollection.Name)]
public sealed class UiToolkitTests
{
    [Fact]
    public void ServicesAreUnavailableBeforeInitialization()
    {
        Assert.Throws<InvalidOperationException>(() => UiToolkit.FontManager);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.TextLayoutEngine);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Logger);
    }

    [Fact]
    public void InitializationBorrowsTheHostsServicesAndRejectsASecondBinding()
    {
        using var context = new UiTextTestContext();

        Assert.Same(context.FontManager, UiToolkit.FontManager);
        Assert.Same(context.LayoutEngine, UiToolkit.TextLayoutEngine);
        Assert.Same(context.Clipboard, UiToolkit.Clipboard);
        Assert.Same(context.Logger, UiToolkit.Logger);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, 5));
        Assert.Same(context.Logger, UiToolkit.Logger);
        Assert.Equal(1, UiToolkit.DefaultScale);
        Assert.Equal(3, UiToolkit.WheelScrollLines);
    }

    [Fact]
    public void ShutdownKeepsSettingsAndBorrowedServicesAliveForRebinding()
    {
        using var logger = new RecordingLogger();
        using var context = new UiTextTestContext(logger);
        UiToolkit.DefaultScale = 1.5;
        UiToolkit.WheelScrollLines = 5;

        UiToolkit.Shutdown();
        UiToolkit.Shutdown();

        Assert.Equal(1.5, UiToolkit.DefaultScale);
        Assert.Equal(5, UiToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.FontManager);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.TextLayoutEngine);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Logger);
        Assert.False(logger.IsDisposed);
        logger.LogInformation("still available");
        Assert.Equal("still available", Assert.Single(logger.Messages));
        Assert.Same(context.DefaultFace, context.FontManager.Match(context.FontManager.DefaultFont));
        context.Clipboard.SetText("still available");
        Assert.True(context.Clipboard.TryGetText(out var text));
        Assert.Equal("still available", text);

        UiToolkit.Initialize(context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, -1);

        Assert.Same(context.FontManager, UiToolkit.FontManager);
        Assert.Same(logger, UiToolkit.Logger);
        Assert.Equal(2, UiToolkit.DefaultScale);
        Assert.Equal(-1, UiToolkit.WheelScrollLines);
    }

    [Fact]
    public void InvalidInitializationSettingsDoNotBindOrChangeExistingSettings()
    {
        using var context = new UiTextTestContext();
        UiToolkit.Shutdown();

        Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, double.NaN, 5));
        Assert.Equal(1, UiToolkit.DefaultScale);
        Assert.Equal(3, UiToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Logger);
    }

    [Fact]
    public void ANewHostLifecycleReplacesAllServiceReferences()
    {
        using var firstLogger = new RecordingLogger();
        using var secondLogger = new RecordingLogger();
        FontManager firstFonts;
        TextLayoutEngine firstLayout;
        MemoryClipboard firstClipboard;
        using (var first = new UiTextTestContext(firstLogger))
        {
            firstFonts = UiToolkit.FontManager;
            firstLayout = UiToolkit.TextLayoutEngine;
            firstClipboard = first.Clipboard;
        }

        using var second = new UiTextTestContext(secondLogger);

        Assert.NotSame(firstFonts, UiToolkit.FontManager);
        Assert.NotSame(firstLayout, UiToolkit.TextLayoutEngine);
        Assert.NotSame(firstClipboard, UiToolkit.Clipboard);
        Assert.Same(second.FontManager, UiToolkit.FontManager);
        Assert.Same(second.LayoutEngine, UiToolkit.TextLayoutEngine);
        Assert.Same(second.Clipboard, UiToolkit.Clipboard);
        Assert.NotSame(firstLogger, UiToolkit.Logger);
        Assert.Same(secondLogger, UiToolkit.Logger);
        UiToolkit.Logger.LogInformation("new host");
        Assert.Empty(firstLogger.Messages);
        Assert.Equal("new host", Assert.Single(secondLogger.Messages));
    }

    [Fact]
    public void ShutdownFromAnotherThreadRejectsTheOperationAndPreservesTheBinding()
    {
        using var context = new UiTextTestContext();
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(UiToolkit.Shutdown));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(context.Clipboard, UiToolkit.Clipboard);
        Assert.Same(context.Logger, UiToolkit.Logger);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SettingChangesFromAnotherThreadAreRejectedWithoutChangingState(int setting)
    {
        using var context = new UiTextTestContext();
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(() =>
        {
            if (setting == 0)
                UiToolkit.DefaultScale = 2;
            else
                UiToolkit.WheelScrollLines = 5;
        }));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, UiToolkit.DefaultScale);
        Assert.Equal(3, UiToolkit.WheelScrollLines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingServicesAreRejectedBeforeBindingOrChangingSettings(int missingService)
    {
        using var context = new UiTextTestContext();
        UiToolkit.Shutdown();

        Assert.Throws<ArgumentNullException>(() => UiToolkit.Initialize(
            missingService == 0 ? null! : context.FontManager,
            missingService == 1 ? null! : context.LayoutEngine,
            missingService == 2 ? null! : context.Clipboard,
            missingService == 3 ? null! : context.Logger,
            2,
            5));

        Assert.Equal(1, UiToolkit.DefaultScale);
        Assert.Equal(3, UiToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UiToolkit.Logger);
    }

    [Fact]
    public void WheelScrollLinesAcceptsPageZeroAndPositiveValuesAndRejectsOtherNegatives()
    {
        var original = UiToolkit.WheelScrollLines;
        try
        {
            foreach (var value in new[] { -1, 0, 5, int.MaxValue })
            {
                UiToolkit.WheelScrollLines = value;
                Assert.Equal(value, UiToolkit.WheelScrollLines);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.WheelScrollLines = -2);
            Assert.Equal(int.MaxValue, UiToolkit.WheelScrollLines);
        }
        finally
        {
            UiToolkit.WheelScrollLines = original;
        }
    }

    [Fact]
    public void PublicTextBoxClipboardOperationsUseTheToolkitWithoutAClientEngine()
    {
        using var context = new UiTextTestContext();
        var box = new TextBox { Text = "abcdef" };
        box.Select(1, 3);

        box.Copy();

        Assert.True(context.Clipboard.TryGetText(out var copied));
        Assert.Equal("bcd", copied);
        box.Cut();
        Assert.Equal("aef", box.Text);
        box.Paste();
        Assert.Equal("abcdef", box.Text);
    }

    private sealed class RecordingLogger : ILogger, IDisposable
    {
        internal List<string> Messages { get; } = [];
        internal bool IsDisposed { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));

        public void Dispose() => IsDisposed = true;
    }
}
