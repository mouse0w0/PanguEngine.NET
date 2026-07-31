using PanguEngine.Windowing;

namespace PanguEngine.Tests.Windowing;

public sealed class WindowManagerTests
{
    [Fact]
    public void WindowPhasesUseOneDueSnapshot()
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = CreateManager(primary, secondary);
        var calls = new List<string>();
        primary.PreRender += (_, _) => calls.Add("primary-pre");
        secondary.PreRender += (_, _) => calls.Add("secondary-pre");
        primary.Render += (_, _) => calls.Add("primary-render");
        secondary.Render += (_, _) => calls.Add("secondary-render");

        manager.PreRenderWindows(0.25);

        Assert.Equal(["primary-pre", "secondary-pre"], calls);

        manager.RenderWindows(0.25);

        Assert.Equal(
            ["primary-pre", "secondary-pre", "primary-render", "secondary-render"],
            calls);

        calls.Clear();
        primary.IsVisible = false;
        secondary.IsVisible = false;

        manager.PreRenderWindows(0.5);
        manager.RenderWindows(0.5);

        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(RenderBlockingState.Destroyed)]
    [InlineData(RenderBlockingState.Closing)]
    [InlineData(RenderBlockingState.Hidden)]
    [InlineData(RenderBlockingState.Minimized)]
    public void PreRenderStateChangeSkipsOnlyRenderForTheAffectedWindow(RenderBlockingState state)
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = CreateManager(primary, secondary);
        var calls = new List<string>();
        primary.PreRender += (_, _) =>
        {
            calls.Add("primary-pre");
            MakeWindowNonRenderable(secondary, state);
        };
        secondary.PreRender += (_, _) => calls.Add("secondary-pre");
        primary.Render += (_, _) => calls.Add("primary-render");
        secondary.Render += (_, _) => calls.Add("secondary-render");

        manager.PreRenderWindows(0);
        manager.RenderWindows(0);

        Assert.Equal(["primary-pre", "secondary-pre", "primary-render"], calls);
    }

    [Fact]
    public void WindowCreatedDuringPreRenderJoinsTheNextSnapshot()
    {
        var primary = new TestWindow(true);
        var createdWindow = new TestWindow();
        var manager = CreateManager(primary, createdWindow, addSecondary: false);
        var calls = new List<string>();
        var created = false;
        primary.PreRender += (_, _) =>
        {
            calls.Add("primary-pre");
            if (!created)
            {
                manager.CreateWindow(default);
                created = true;
            }
        };
        primary.Render += (_, _) => calls.Add("primary-render");
        createdWindow.PreRender += (_, _) => calls.Add("created-pre");
        createdWindow.Render += (_, _) => calls.Add("created-render");

        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        Assert.Equal(["primary-pre", "primary-render"], calls);

        calls.Clear();
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        Assert.Equal(
            ["primary-pre", "created-pre", "primary-render", "created-render"],
            calls);
    }

    [Fact]
    public void WindowCreatedDuringRenderJoinsTheNextSnapshot()
    {
        var primary = new TestWindow(true);
        var createdWindow = new TestWindow();
        var manager = CreateManager(primary, createdWindow, addSecondary: false);
        var calls = new List<string>();
        var created = false;
        primary.PreRender += (_, _) => calls.Add("primary-pre");
        primary.Render += (_, _) =>
        {
            calls.Add("primary-render");
            if (!created)
            {
                manager.CreateWindow(default);
                created = true;
            }
        };
        createdWindow.PreRender += (_, _) => calls.Add("created-pre");
        createdWindow.Render += (_, _) => calls.Add("created-render");

        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        Assert.Equal(["primary-pre", "primary-render"], calls);

        calls.Clear();
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        Assert.Equal(
            ["primary-pre", "created-pre", "primary-render", "created-render"],
            calls);
    }

    [Fact]
    public void PreRenderExceptionStopsLaterPreRenders()
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = CreateManager(primary, secondary);
        var expected = new InvalidOperationException("pre-render failed");
        var calls = new List<string>();
        primary.PreRender += (_, _) =>
        {
            calls.Add("primary-pre");
            throw expected;
        };
        secondary.PreRender += (_, _) => calls.Add("secondary-pre");
        primary.Render += (_, _) => calls.Add("primary-render");

        var actual = Assert.Throws<InvalidOperationException>(
            () => manager.PreRenderWindows(0));

        Assert.Same(expected, actual);
        Assert.Equal(["primary-pre"], calls);
    }

    [Fact]
    public void RenderExceptionStopsLaterWindows()
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = CreateManager(primary, secondary);
        var expected = new InvalidOperationException("render failed");
        var calls = new List<string>();
        primary.PreRender += (_, _) => calls.Add("primary-pre");
        secondary.PreRender += (_, _) => calls.Add("secondary-pre");
        primary.Render += (_, _) =>
        {
            calls.Add("primary-render");
            throw expected;
        };
        secondary.Render += (_, _) => calls.Add("secondary-render");

        manager.PreRenderWindows(0);
        var actual = Assert.Throws<InvalidOperationException>(() => manager.RenderWindows(0));

        Assert.Same(expected, actual);
        Assert.Equal(["primary-pre", "secondary-pre", "primary-render"], calls);
    }

    [Fact]
    public void FramesPerSecondUsesTheCapturedRenderTime()
    {
        var now = 1d;
        var timeReadCount = 0;
        var primary = new TestWindow(true) { FramesPerSecond = 2 };
        var manager = new WindowManager(primary, _ => new TestWindow(), () =>
        {
            timeReadCount++;
            return now;
        });
        var renderCount = 0;
        primary.Render += (_, _) => renderCount++;

        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        now = 1.25;
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        now = 1.5;
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);

        Assert.Equal(2, renderCount);
        Assert.Equal(3, timeReadCount);
    }

    [Fact]
    public void SkippingRenderAfterPreRenderDoesNotUpdateLastRenderTime()
    {
        var now = 1d;
        var primary = new TestWindow(true) { FramesPerSecond = 2 };
        var manager = new WindowManager(primary, _ => new TestWindow(), () => now);
        var renderCount = 0;
        Action<PanguEngine.Windowing.Window, double> hide = (window, _) => window.IsVisible = false;
        primary.PreRender += hide;
        primary.Render += (_, _) => renderCount++;

        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        primary.PreRender -= hide;
        primary.IsVisible = true;
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);

        Assert.Equal(1, renderCount);
    }

    private static WindowManager CreateManager(
        TestWindow primary,
        TestWindow secondary,
        bool addSecondary = true)
    {
        var manager = new WindowManager(primary, _ => secondary);
        if (addSecondary)
            manager.CreateWindow(default);
        return manager;
    }

    private static void MakeWindowNonRenderable(TestWindow window, RenderBlockingState state)
    {
        switch (state)
        {
            case RenderBlockingState.Destroyed:
                window.Destroy();
                break;
            case RenderBlockingState.Closing:
                window.IsClosing = true;
                break;
            case RenderBlockingState.Hidden:
                window.IsVisible = false;
                break;
            case RenderBlockingState.Minimized:
                window.WindowState = WindowState.Minimized;
                break;
        }
    }

    public enum RenderBlockingState
    {
        Destroyed,
        Closing,
        Hidden,
        Minimized
    }
}
