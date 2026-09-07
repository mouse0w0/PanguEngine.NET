using System.Runtime.CompilerServices;
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
        var manager = new TestWindowManager(primary, _ => new TestWindow(), () =>
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
        var manager = new TestWindowManager(primary, _ => new TestWindow(), () => now);
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

    [Fact]
    public void DoEventsCallsEventPumpOnce()
    {
        var primary = new TestWindow(true);
        var pumpCount = 0;
        var manager = new TestWindowManager(primary, _ => new TestWindow(), () => 0, () =>
        {
            pumpCount++;
        });

        manager.DoEvents();

        Assert.Equal(1, pumpCount);
    }

    [Fact]
    public void HideAllHidesWindowsWithoutDestroyingThem()
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = new TestWindowManager(primary, _ => secondary);
        manager.CreateWindow(default);

        manager.HideAll();

        Assert.Empty(manager.VisibleWindows);
        Assert.Equal(2, manager.Windows.Count);
        Assert.False(primary.IsDestroyed);
        Assert.False(secondary.IsDestroyed);
    }

    [Fact]
    public void HiddenWindowCanBeShownAgain()
    {
        var primary = new TestWindow(true);
        var secondary = new TestWindow();
        var manager = CreateManager(primary, secondary);

        secondary.Hide();

        Assert.Contains(secondary, manager.Windows);
        Assert.DoesNotContain(secondary, manager.VisibleWindows);
        Assert.False(secondary.IsDestroyed);

        secondary.Show();

        Assert.Contains(secondary, manager.VisibleWindows);
        Assert.False(secondary.IsDestroyed);
    }

    [Fact]
    public void VisibilityChangedIsRaisedOncePerStateTransition()
    {
        var window = new TestWindow();
        var changes = new List<(bool IsVisible, bool ObservedIsVisible)>();
        window.VisibilityChanged += (sender, isVisible) => changes.Add((isVisible, sender.IsVisible));

        window.Hide();
        window.Hide();
        window.Show();
        window.Show();

        Assert.Equal([(false, false), (true, true)], changes);
    }

    [Fact]
    public void VisibleWindowsKeepsVisibleWindowAlive()
    {
        var primary = new TestWindow(true);
        var manager = new TestWindowManager(primary, _ => new TestWindow());
        var weakWindow = CreateVisibleWindow(manager);

        ForceCollection();

        AssertWindowIsAliveAndHide(weakWindow);
        ForceCollection();

        Assert.False(weakWindow.TryGetTarget(out _));
        GC.KeepAlive(manager);
    }

    [Fact]
    public void HiddenWindowIsRemovedAfterExternalReferencesAreReleased()
    {
        var primary = new TestWindow(true);
        var manager = new TestWindowManager(primary, _ => new TestWindow());
        var weakWindow = CreateHiddenWindow(manager);
        var renderCount = 0;
        primary.Render += (_, _) => renderCount++;

        ForceCollection();
        manager.DoEvents();
        manager.PreRenderWindows(0);
        manager.RenderWindows(0);
        ForceCollection();

        Assert.False(weakWindow.TryGetTarget(out _));
        Assert.Single(manager.Windows);
        Assert.Equal(1, renderCount);
    }

    private static WindowManager CreateManager(
        TestWindow primary,
        TestWindow secondary,
        bool addSecondary = true)
    {
        var manager = new TestWindowManager(primary, _ => secondary);
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
            case RenderBlockingState.Hidden:
                window.IsVisible = false;
                break;
            case RenderBlockingState.Minimized:
                window.WindowState = WindowState.Minimized;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Window> CreateVisibleWindow(WindowManager manager)
    {
        var window = manager.CreateWindow(default);
        return new WeakReference<Window>(window);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Window> CreateHiddenWindow(WindowManager manager)
    {
        var window = manager.CreateWindow(default);
        window.Hide();
        return new WeakReference<Window>(window);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertWindowIsAliveAndHide(WeakReference<Window> weakWindow)
    {
        Assert.True(weakWindow.TryGetTarget(out var window));
        window!.Hide();
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public enum RenderBlockingState
    {
        Destroyed,
        Hidden,
        Minimized
    }
}
