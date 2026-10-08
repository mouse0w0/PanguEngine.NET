using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

[Collection(UiToolkitCollection.Name)]
public sealed class UiToolkitScaleTests
{
    [Fact]
    public void DefaultScaleDefaultsToOneAndRejectsInvalidValuesWithoutChangingState()
    {
        var original = UiToolkit.DefaultScale;
        try
        {
            Assert.Equal(1, original);
            UiToolkit.DefaultScale = 1.5;

            Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.DefaultScale = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.DefaultScale = double.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.DefaultScale = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiToolkit.DefaultScale = -1);

            Assert.Equal(1.5, UiToolkit.DefaultScale);
        }
        finally
        {
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void ScreenUsesCurrentDefaultScaleAtConstructionAndOpening()
    {
        var original = UiToolkit.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiToolkit.DefaultScale = 1.25;
            screen = new UiScreen();
            Assert.Equal(1.25, screen.Scale);

            UiToolkit.DefaultScale = 1.5;
            screen.Open();

            Assert.Equal(1.5, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenScreenReflowsWhenDefaultScaleChanges()
    {
        var original = UiToolkit.DefaultScale;
        var manager = new UiManager();
        try
        {
            UiToolkit.DefaultScale = 1;
            var root = new LayoutNode();
            var screen = new GameScreen(root);
            manager.Open(screen);
            manager.UpdateFrame(new Size(100, 80), 0);
            Assert.Equal(new Size(100, 80), root.LastMeasureConstraint);

            UiToolkit.DefaultScale = 2;
            manager.UpdateFrame(new Size(100, 80), 0);

            Assert.Equal(2, screen.Scale);
            Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
            Assert.Equal(2, root.MeasureCalls);
        }
        finally
        {
            if (manager.CurrentScreen is not null)
                manager.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScaleEqualToCurrentValueStopsFollowingDefaultScale()
    {
        var original = UiToolkit.DefaultScale;
        var manager = new UiManager();
        try
        {
            UiToolkit.DefaultScale = 1;
            var screen = new GameScreen { Scale = 1 };
            manager.Open(screen);

            UiToolkit.DefaultScale = 2;
            manager.UpdateFrame(new Size(100, 80), 0);

            Assert.Equal(1, screen.Scale);
        }
        finally
        {
            if (manager.CurrentScreen is not null)
                manager.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void FailedExplicitScaleAssignmentDoesNotStopFollowingDefaultScale()
    {
        var original = UiToolkit.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiToolkit.DefaultScale = 1;
            screen = new UiScreen();
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = 0);

            UiToolkit.DefaultScale = 2;
            screen.Open();

            Assert.Equal(2, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenScreenUsesLatestDefaultScaleWhenReopened()
    {
        var original = UiToolkit.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiToolkit.DefaultScale = 1;
            screen = new UiScreen();
            screen.Open();
            screen.Close();

            UiToolkit.DefaultScale = 2;
            screen.Open();

            Assert.Equal(2, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScalePersistsWhenScreenReopens()
    {
        var original = UiToolkit.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiToolkit.DefaultScale = 1;
            screen = new UiScreen { Scale = 1.25 };
            screen.Open();
            screen.Close();

            UiToolkit.DefaultScale = 2;
            screen.Open();

            Assert.Equal(1.25, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiToolkit.DefaultScale = original;
        }
    }

    private sealed class LayoutNode : UiNode
    {
        internal Size LastMeasureConstraint { get; private set; }
        internal int MeasureCalls { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            MeasureCalls++;
            return availableSize;
        }
    }
}
