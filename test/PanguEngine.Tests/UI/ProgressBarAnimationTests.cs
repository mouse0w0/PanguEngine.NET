using System.Reflection;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class ProgressBarAnimationTests
{
    [Fact]
    public void IndeterminateControlsCanBeConfiguredBeforeToolkitInitialization()
    {
        var bar = new ProgressBar { IsIndeterminate = true };
        var scene = CreateScene(bar);

        Assert.True(bar.IsIndeterminate);
        Assert.False(scene.IsOpen);
        Assert.Same(scene, bar.Scene);
    }

    [Fact]
    [UIThreadTest]
    public void ModeChangesRestoreTheCurrentBoundValueWithoutAnotherTick()
    {
        var source = new ProgressBar { Value = 40 };
        var mode = new ToggleSwitch();
        var bar = CreateBar();
        bar.BindTwoWay(ProgressBar.ValueProperty, source, ProgressBar.ValueProperty);
        bar.Bind(ProgressBar.IsIndeterminateProperty, mode, ToggleSwitch.IsOnProperty);
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(48, Fill(bar).LayoutBounds.Width);
            mode.IsOn = true;
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 10);
            Frame(scene, 10.6);
            source.Value = 70;
            Prepare(scene);
            Assert.Equal(36, Fill(bar).LayoutBounds.Width, 8);
            Assert.Equal(70, source.Value);

            mode.IsOn = false;
            Assert.False(AnimationTicker(bar).IsActive);
            Prepare(scene);
            Assert.Equal(84, Fill(bar).LayoutBounds.Width);
            Assert.Equal(70, bar.Value);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(Orientation.Horizontal, false, 30.88, 8, 31.2, 64)]
    [InlineData(Orientation.Horizontal, true, 57.92, 8, 31.2, 64)]
    [InlineData(Orientation.Vertical, false, 8, 38.72, 104, 19.2)]
    [InlineData(Orientation.Vertical, true, 8, 22.08, 104, 19.2)]
    [UIThreadTest]
    public void MotionUsesTheContentBoxAndDirection(Orientation orientation, bool reversed,
        double x, double y, double width, double height)
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        bar.Orientation = orientation;
        bar.IsReversed = reversed;
        bar.BorderThickness = new Thickness(2);
        bar.Padding = new Thickness(6);
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Frame(scene, 10);
            Frame(scene, 10.6);
            var bounds = Fill(bar).LayoutBounds;
            Assert.Equal(x, bounds.X, 8);
            Assert.Equal(y, bounds.Y, 8);
            Assert.Equal(width, bounds.Width, 8);
            Assert.Equal(height, bounds.Height, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void SegmentEntersLeavesAndWrapsUsingElapsedTime()
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Frame(scene, 10);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            var desiredSize = bar.DesiredSize;
            Frame(scene, 10.15);
            Assert.Equal(0, Fill(bar).LayoutBounds.X);
            Assert.Equal(15.6, Fill(bar).LayoutBounds.Width, 8);
            Frame(scene, 10.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
            Assert.Equal(36, Fill(bar).LayoutBounds.Width, 8);
            Frame(scene, 11.35);
            Assert.Equal(104.4, Fill(bar).LayoutBounds.X, 8);
            Assert.Equal(15.6, Fill(bar).LayoutBounds.Width, 8);
            Frame(scene, 16.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
            Assert.Equal(36, Fill(bar).LayoutBounds.Width, 8);
            Assert.Equal(desiredSize, bar.DesiredSize);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void DirectionAndSizeChangesKeepPhaseAndDisabledDegenerateRangesStillAnimate()
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        bar.IsEnabled = false;
        bar.Minimum = 100;
        bar.Maximum = 0;
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Frame(scene, 10);
            Frame(scene, 10.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
            Assert.Equal(36, Fill(bar).LayoutBounds.Width, 8);
            bar.Orientation = Orientation.Vertical;
            Prepare(scene);
            Assert.Equal(38.4, Fill(bar).LayoutBounds.Y, 8);
            Assert.Equal(24, Fill(bar).LayoutBounds.Height, 8);
            bar.IsReversed = true;
            bar.Height = 40;
            Prepare(scene);
            Assert.Equal(8.8, Fill(bar).LayoutBounds.Y, 8);
            Assert.Equal(12, Fill(bar).LayoutBounds.Height, 8);
            bar.Width = 0;
            bar.Height = 0;
            Prepare(scene);
            Assert.Equal(Size.Zero, Fill(bar).LayoutBounds.Size);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void ClosingAndDetachingStopRegistrationsAndNewRunsResetTime()
    {
        var bar = CreateBar();
        AssertLifecycleSubscriptions(bar, false, false);
        bar.IsIndeterminate = true;
        AssertLifecycleSubscriptions(bar, true, false);
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            AssertLifecycleSubscriptions(bar, true, true);
            Frame(scene, 10);
            Frame(scene, 10.6);
            scene.Close();
            Assert.False(AnimationTicker(bar).IsActive);
            AssertLifecycleSubscriptions(bar, true, true);
            scene.Open();
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 100);
            Frame(scene, 100.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);

            scene.Root = null;
            Assert.False(AnimationTicker(bar).IsActive);
            AssertLifecycleSubscriptions(bar, true, false);
            scene.Root = bar;
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 200);
            Frame(scene, 200.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);

            bar.IsIndeterminate = false;
            AssertLifecycleSubscriptions(bar, false, false);
            Assert.False(AnimationTicker(bar).IsActive);
            bar.IsIndeterminate = true;
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 300);
            Frame(scene, 300.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);

            bar.Visibility = Visibility.Hidden;
            scene.Close();
            bar.Visibility = Visibility.Visible;
            Assert.False(AnimationTicker(bar).IsActive);
            scene.Open();
            Frame(scene, 400);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 400.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void DirectRootMigrationResetsPlaybackAndUnsubscribesTheOldScene()
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        var first = CreateScene(bar);
        var second = CreateScene();
        first.Open();
        second.Open();
        try
        {
            Frame(first, 10);
            Frame(first, 10.6);
            second.Root = bar;
            Assert.True(AnimationTicker(bar).IsActive);
            Frame(second, 100);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(second, 100.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);

            first.Close();
            Frame(second, 100.75);
            Assert.Equal(42, Fill(bar).LayoutBounds.X, 8);
            Assert.True(AnimationTicker(bar).IsActive);
        }
        finally
        {
            if (first.IsOpen)
                first.Close();
            second.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void LeavingIndeterminateModeDuringSceneNotificationDoesNotRestoreSubscriptions()
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Property, UIElement.SceneProperty))
                bar.IsIndeterminate = false;
        };
        var scene = CreateScene();
        scene.Open();
        try
        {
            scene.Root = bar;

            Assert.False(bar.IsIndeterminate);
            AssertLifecycleSubscriptions(bar, false, false);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false, Visibility.Hidden)]
    [InlineData(false, Visibility.Collapsed)]
    [InlineData(true, Visibility.Hidden)]
    [InlineData(true, Visibility.Collapsed)]
    [UIThreadTest]
    public void HiddenRunsStopTickerAndResumeWithoutCountingHiddenTime(bool ancestor,
        Visibility visibility)
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        var root = new Panel();
        root.Children.Add(bar);
        var hidden = ancestor ? (UIElement)root : bar;
        var scene = CreateScene(root);
        scene.Open();
        try
        {
            Frame(scene, 10);
            Frame(scene, 10.6);
            hidden.Visibility = visibility;
            Assert.False(AnimationTicker(bar).IsActive);
            Prepare(scene);
            Assert.True(root.IsArrangeValid);
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(100));
            Assert.True(root.IsArrangeValid);
            Assert.False(AnimationTicker(bar).IsActive);

            hidden.Visibility = Visibility.Visible;
            Assert.True(AnimationTicker(bar).IsActive);
            Prepare(scene);
            Frame(scene, 200);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
            Frame(scene, 200.15);
            Assert.Equal(42, Fill(bar).LayoutBounds.X, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest]
    public void InitiallyCollapsedRunsCanMeasureAndAnimateAfterBecomingVisible(bool ancestor)
    {
        var bar = CreateBar();
        bar.IsIndeterminate = true;
        var root = new Panel();
        root.Children.Add(bar);
        var hidden = ancestor ? (UIElement)root : bar;
        hidden.Visibility = Visibility.Collapsed;
        var scene = CreateScene(root);
        scene.Open();
        try
        {
            Frame(scene, 10);
            Assert.Null(typeof(ProgressBar)
                .GetField("_animationTicker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar));
            hidden.Visibility = Visibility.Visible;
            Assert.True(AnimationTicker(bar).IsActive);
            Frame(scene, 100);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Frame(scene, 100.6);
            Assert.Equal(26.4, Fill(bar).LayoutBounds.X, 8);
            Assert.Equal(36, Fill(bar).LayoutBounds.Width, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void ProgrammaticModeChangesUpdatePseudoClassStylesAndClearRestoresDeterminateProgress()
    {
        var bar = CreateBar();
        bar.Value = 60;
        var scene = CreateScene(bar);
        scene.SetStyleSheets([StyleSheet.Parse("""
            ProgressBar .progress-bar-fill { background-color: #445566; }
            ProgressBar:indeterminate .progress-bar-fill { background-color: #112233; }
            """)]);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(72, Fill(bar).LayoutBounds.Width);
            Assert.Equal(new SolidColorBrush(68, 85, 102), Assert.IsType<Panel>(Fill(bar)).Background);
            Assert.Null(CssRegistry.FindProperty(typeof(ProgressBar), "is-indeterminate"));
            bar.SetValue(ProgressBar.IsIndeterminateProperty, true);
            Prepare(scene);
            Assert.True(bar.IsIndeterminate);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            Assert.Equal(new SolidColorBrush(17, 34, 51), Assert.IsType<Panel>(Fill(bar)).Background);
            Frame(scene, 10);
            Frame(scene, 10.6);
            bar.ClearValue(ProgressBar.IsIndeterminateProperty);
            Prepare(scene);
            Assert.False(bar.IsIndeterminate);
            Assert.False(AnimationTicker(bar).IsActive);
            AssertLifecycleSubscriptions(bar, false, false);
            Assert.Equal(72, Fill(bar).LayoutBounds.Width);
            Assert.Equal(new SolidColorBrush(68, 85, 102), Assert.IsType<Panel>(Fill(bar)).Background);
        }
        finally
        {
            scene.Close();
        }
    }

    private static void AssertLifecycleSubscriptions(ProgressBar bar, bool scene, bool sceneOpen)
    {
        var sceneToken = typeof(ProgressBar)
            .GetField("_sceneSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar);
        var openToken = typeof(ProgressBar)
            .GetField("_sceneOpenSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar);
        var visibilityToken = typeof(ProgressBar)
            .GetField("_visibilitySubscription", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar);
        Assert.Equal(scene, sceneToken is IDisposable);
        Assert.Equal(sceneOpen, openToken is IDisposable);
        Assert.Equal(scene, visibilityToken is IDisposable);
    }

    private static ProgressBar CreateBar() => new() { Width = 120, Height = 80 };

    private static UIScene CreateScene(UIElement? root = null)
    {
        var scene = new UIScene(root) { UseLayoutRounding = false };
        scene.SetBaseStyleSheets([]);
        return scene;
    }

    private static void Prepare(UIScene scene) => scene.PrepareFrame(new Size(120, 80));

    private static void Frame(UIScene scene, double seconds)
    {
        UIToolkit.UpdateFrame(TimeSpan.FromSeconds(seconds));
        Prepare(scene);
    }

    private static UIElement Fill(ProgressBar bar) =>
        bar.ReadOnlyChildren.Single(child => child.Classes.Contains("progress-bar-fill"));

    internal static Ticker AnimationTicker(ProgressBar bar) =>
        Assert.IsType<Ticker>(typeof(ProgressBar)
            .GetField("_animationTicker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar));
}
