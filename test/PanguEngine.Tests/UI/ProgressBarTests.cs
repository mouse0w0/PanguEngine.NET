using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class ProgressBarTests
{
    [Fact]
    public void RangeChangesKeepTheRawValueAndTwoWayBindingSource()
    {
        var source = new ProgressBar { Value = 150 };
        var bar = CreateBar();
        bar.BindTwoWay(ProgressBar.ValueProperty, source, ProgressBar.ValueProperty);
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(120, Fill(bar).LayoutBounds.Width);
            bar.Maximum = 300;
            Prepare(scene);
            Assert.Equal(60, Fill(bar).LayoutBounds.Width);
            bar.Minimum = 400;
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            bar.Minimum = 150;
            bar.Maximum = 150;
            Prepare(scene);
            Assert.Equal(0, Fill(bar).LayoutBounds.Width);
            bar.Minimum = 0;
            bar.Maximum = 200;
            Prepare(scene);
            Assert.Equal(90, Fill(bar).LayoutBounds.Width);
            Assert.Equal(150, bar.Value);
            Assert.Equal(150, source.Value);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void ClearRestoresTheDeclaredRangeAndValueDefaults()
    {
        var bar = new ProgressBar { Minimum = -50, Maximum = 200, Value = 120 };
        bar.ClearValue(ProgressBar.MinimumProperty);
        bar.ClearValue(ProgressBar.MaximumProperty);
        bar.ClearValue(ProgressBar.ValueProperty);
        Assert.Equal(0, bar.Minimum);
        Assert.Equal(100, bar.Maximum);
        Assert.Equal(0, bar.Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteWritesAreRejectedBeforeCommit(double value)
    {
        var bar = new ProgressBar { Minimum = -10, Maximum = 80, Value = 35 };
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Minimum = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Maximum = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Value = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ProgressBar.MinimumProperty, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ProgressBar.MaximumProperty, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ProgressBar.ValueProperty, value));
        Assert.Equal(-10, bar.Minimum);
        Assert.Equal(80, bar.Maximum);
        Assert.Equal(35, bar.Value);
    }

    public static TheoryData<double, double, double, double> ExtremeRanges => new()
    {
        { -double.MaxValue, double.MaxValue, 0, 0.5 },
        { -double.MaxValue, double.MaxValue, double.MaxValue / 2, 0.75 },
        { double.MaxValue / 2, double.MaxValue, double.MaxValue * 0.75, 0.5 },
        { 0, 2 * double.Epsilon, double.Epsilon, 0.5 }
    };

    [Theory]
    [MemberData(nameof(ExtremeRanges))]
    public void ExtremeAndSubnormalRangesKeepTheirProportion(double minimum, double maximum, double value,
        double proportion)
    {
        var bar = CreateBar();
        bar.Minimum = minimum;
        bar.Maximum = maximum;
        bar.Value = value;
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(120 * proportion, Fill(bar).LayoutBounds.Width, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(Orientation.Horizontal, false, 8, 8, 26, 64)]
    [InlineData(Orientation.Horizontal, true, 86, 8, 26, 64)]
    [InlineData(Orientation.Vertical, false, 8, 56, 104, 16)]
    [InlineData(Orientation.Vertical, true, 8, 8, 104, 16)]
    public void FillUsesTheContentBoxAndRequestedDirection(Orientation orientation, bool reversed,
        double x, double y, double width, double height)
    {
        var bar = new ProgressBar
        {
            Width = 120, Height = 80, Value = 25, Orientation = orientation, IsReversed = reversed,
            BorderThickness = new Thickness(2), Padding = new Thickness(6)
        };
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(new Rect(x, y, width, height), Fill(bar).LayoutBounds);
            bar.Value = -20;
            Prepare(scene);
            Assert.Equal(0, orientation == Orientation.Horizontal
                ? Fill(bar).LayoutBounds.Width : Fill(bar).LayoutBounds.Height);
            bar.Value = 200;
            Prepare(scene);
            Assert.Equal(bar.ContentBounds, Fill(bar).LayoutBounds);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void EmptyContentDoesNotProduceVisibleFill()
    {
        var bar = new ProgressBar { Width = 0, Height = 0, Value = 100 };
        var scene = CreateScene(bar);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(Size.Zero, Fill(bar).LayoutBounds.Size);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void CssConfiguresDirectionAndRecordsTheOuterRoundedClip()
    {
        var bar = CreateBar();
        bar.Value = 50;
        bar.BorderThickness = new Thickness(2);
        bar.Padding = new Thickness(1);
        bar.CornerRadius = new CornerRadius(5);
        var scene = CreateScene(bar);
        scene.SetStyleSheets([StyleSheet.Parse("""
            ProgressBar { orientation: vertical; is-reversed: true; }
            ProgressBar:vertical:reversed .progress-bar-fill { background-color: #112233; }
            """)]);
        scene.Open();
        try
        {
            Prepare(scene);
            Assert.Equal(Orientation.Vertical, bar.Orientation);
            Assert.True(bar.IsReversed);
            var fill = Assert.IsType<Panel>(Fill(bar));
            Assert.Equal(new SolidColorBrush(17, 34, 51), fill.Background);
            var clip = Assert.Single(scene.CreateDrawCommandList().OfType<PushRoundedClipCommand>());
            Assert.Equal(bar.DecorationBounds, clip.Geometry.Outer.Bounds);
            Assert.Equal(new Rect(3, 3, 114, 3), fill.LayoutBounds);
            scene.SetStyleSheets([StyleSheet.Parse("""
                ProgressBar { orientation: horizontal; is-reversed: false; }
                ProgressBar:horizontal .progress-bar-fill { background-color: #445566; }
                """)]);
            Prepare(scene);
            Assert.Equal(Orientation.Horizontal, bar.Orientation);
            Assert.False(bar.IsReversed);
            Assert.Equal(new Rect(3, 3, 57, 6), fill.LayoutBounds);
            Assert.Equal(new SolidColorBrush(68, 85, 102), fill.Background);
            Assert.False(bar.IsHitTestVisible);
            Assert.False(fill.IsHitTestVisible);
            Assert.Null(CssRegistry.FindProperty(typeof(ProgressBar), "value"));
        }
        finally
        {
            scene.Close();
        }
    }

    private static ProgressBar CreateBar() => new() { Width = 120, Height = 12 };

    private static UIScene CreateScene(UIElement? root = null)
    {
        var scene = new UIScene(root) { UseLayoutRounding = false };
        scene.SetBaseStyleSheets([]);
        return scene;
    }

    private static void Prepare(UIScene scene) =>
        scene.PrepareFrame(new Size(120, 80), TimeSpan.Zero);

    private static UIElement Fill(ProgressBar bar) =>
        bar.ReadOnlyChildren.Single(child => child.Classes.Contains("progress-bar-fill"));
}
