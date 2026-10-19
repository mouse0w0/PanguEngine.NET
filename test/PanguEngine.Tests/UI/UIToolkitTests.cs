using Microsoft.Extensions.Logging;
using PanguEngine.Graphics.Text;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class UIToolkitTests
{
    [Fact]
    public void ServicesAreUnavailableBeforeInitialization()
    {
        Assert.Throws<InvalidOperationException>(() => UIToolkit.FontManager);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.TextLayoutEngine);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Logger);
    }

    [Fact]
    public void InitializationBorrowsTheHostsServicesAndRejectsASecondBinding()
    {
        using var context = new TextTestContext();

        Assert.Same(context.FontManager, UIToolkit.FontManager);
        Assert.Same(context.LayoutEngine, UIToolkit.TextLayoutEngine);
        Assert.Same(context.Clipboard, UIToolkit.Clipboard);
        Assert.Same(context.Logger, UIToolkit.Logger);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, 5));
        Assert.Same(context.Logger, UIToolkit.Logger);
        Assert.Equal(1, UIToolkit.DefaultScale);
        Assert.Equal(3, UIToolkit.WheelScrollLines);
    }

    [Fact]
    public void ShutdownKeepsSettingsAndBorrowedServicesAliveForRebinding()
    {
        using var logger = new RecordingLogger();
        using var context = new TextTestContext(logger);
        UIToolkit.DefaultScale = 1.5;
        UIToolkit.WheelScrollLines = 5;

        UIToolkit.Shutdown();
        UIToolkit.Shutdown();

        Assert.Equal(1.5, UIToolkit.DefaultScale);
        Assert.Equal(5, UIToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.FontManager);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.TextLayoutEngine);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Logger);
        Assert.False(logger.IsDisposed);
        logger.LogInformation("still available");
        Assert.Equal("still available", Assert.Single(logger.Messages));
        Assert.Same(context.DefaultFace, context.FontManager.Match(context.FontManager.DefaultFont));
        context.Clipboard.SetText("still available");
        Assert.True(context.Clipboard.TryGetText(out var text));
        Assert.Equal("still available", text);

        UIToolkit.Initialize(context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, -1);

        Assert.Same(context.FontManager, UIToolkit.FontManager);
        Assert.Same(logger, UIToolkit.Logger);
        Assert.Equal(2, UIToolkit.DefaultScale);
        Assert.Equal(-1, UIToolkit.WheelScrollLines);
    }

    [Fact]
    public void InvalidInitializationSettingsDoNotBindOrChangeExistingSettings()
    {
        using var context = new TextTestContext();
        UIToolkit.Shutdown();

        Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, 2, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.Initialize(
            context.FontManager, context.LayoutEngine, context.Clipboard, context.Logger, double.NaN, 5));
        Assert.Equal(1, UIToolkit.DefaultScale);
        Assert.Equal(3, UIToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Logger);
    }

    [Fact]
    public void ANewHostLifecycleReplacesAllServiceReferences()
    {
        using var firstLogger = new RecordingLogger();
        using var secondLogger = new RecordingLogger();
        FontManager firstFonts;
        TextLayoutEngine firstLayout;
        MemoryClipboard firstClipboard;
        using (var first = new TextTestContext(firstLogger))
        {
            firstFonts = UIToolkit.FontManager;
            firstLayout = UIToolkit.TextLayoutEngine;
            firstClipboard = first.Clipboard;
        }

        using var second = new TextTestContext(secondLogger);

        Assert.NotSame(firstFonts, UIToolkit.FontManager);
        Assert.NotSame(firstLayout, UIToolkit.TextLayoutEngine);
        Assert.NotSame(firstClipboard, UIToolkit.Clipboard);
        Assert.Same(second.FontManager, UIToolkit.FontManager);
        Assert.Same(second.LayoutEngine, UIToolkit.TextLayoutEngine);
        Assert.Same(second.Clipboard, UIToolkit.Clipboard);
        Assert.NotSame(firstLogger, UIToolkit.Logger);
        Assert.Same(secondLogger, UIToolkit.Logger);
        UIToolkit.Logger.LogInformation("new host");
        Assert.Empty(firstLogger.Messages);
        Assert.Equal("new host", Assert.Single(secondLogger.Messages));
    }

    [Fact]
    public void ShutdownFromAnotherThreadRejectsTheOperationAndPreservesTheBinding()
    {
        using var context = new TextTestContext();
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(UIToolkit.Shutdown));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(context.Clipboard, UIToolkit.Clipboard);
        Assert.Same(context.Logger, UIToolkit.Logger);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SettingChangesFromAnotherThreadAreRejectedWithoutChangingState(int setting)
    {
        using var context = new TextTestContext();
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(() =>
        {
            if (setting == 0)
                UIToolkit.DefaultScale = 2;
            else
                UIToolkit.WheelScrollLines = 5;
        }));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, UIToolkit.DefaultScale);
        Assert.Equal(3, UIToolkit.WheelScrollLines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingServicesAreRejectedBeforeBindingOrChangingSettings(int missingService)
    {
        using var context = new TextTestContext();
        UIToolkit.Shutdown();

        Assert.Throws<ArgumentNullException>(() => UIToolkit.Initialize(
            missingService == 0 ? null! : context.FontManager,
            missingService == 1 ? null! : context.LayoutEngine,
            missingService == 2 ? null! : context.Clipboard,
            missingService == 3 ? null! : context.Logger,
            2,
            5));

        Assert.Equal(1, UIToolkit.DefaultScale);
        Assert.Equal(3, UIToolkit.WheelScrollLines);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Clipboard);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Logger);
    }

    [Fact]
    public void WheelScrollLinesAcceptsPageZeroAndPositiveValuesAndRejectsOtherNegatives()
    {
        var original = UIToolkit.WheelScrollLines;
        try
        {
            foreach (var value in new[] { -1, 0, 5, int.MaxValue })
            {
                UIToolkit.WheelScrollLines = value;
                Assert.Equal(value, UIToolkit.WheelScrollLines);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.WheelScrollLines = -2);
            Assert.Equal(int.MaxValue, UIToolkit.WheelScrollLines);
        }
        finally
        {
            UIToolkit.WheelScrollLines = original;
        }
    }

    [Fact]
    public void PublicTextBoxClipboardOperationsUseTheToolkitWithoutAClientEngine()
    {
        using var context = new TextTestContext();
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