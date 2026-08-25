using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

[Collection(UiSettingsCollection.Name)]
public sealed class UiSettingsTests
{
    [Fact]
    public void DefaultScaleDefaultsToOneAndRejectsInvalidValuesWithoutChangingState()
    {
        var original = UiSettings.DefaultScale;
        try
        {
            Assert.Equal(1, original);
            UiSettings.DefaultScale = 1.5;

            Assert.Throws<ArgumentOutOfRangeException>(() => UiSettings.DefaultScale = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiSettings.DefaultScale = double.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiSettings.DefaultScale = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => UiSettings.DefaultScale = -1);

            Assert.Equal(1.5, UiSettings.DefaultScale);
        }
        finally
        {
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void ScreenUsesCurrentDefaultScaleAtConstructionAndOpening()
    {
        var original = UiSettings.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiSettings.DefaultScale = 1.25;
            screen = new UiScreen();
            Assert.Equal(1.25, screen.Scale);

            UiSettings.DefaultScale = 1.5;
            screen.Open();

            Assert.Equal(1.5, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenScreenReflowsWhenDefaultScaleChanges()
    {
        var original = UiSettings.DefaultScale;
        var manager = new UiManager();
        try
        {
            UiSettings.DefaultScale = 1;
            var root = new LayoutNode();
            var screen = new UiScreen(root);
            manager.Open(screen);
            manager.Update(new Size(100, 80));
            Assert.Equal(new Size(100, 80), root.LastMeasureConstraint);

            UiSettings.DefaultScale = 2;
            manager.Update(new Size(100, 80));

            Assert.Equal(2, screen.Scale);
            Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
            Assert.Equal(2, root.MeasureCalls);
        }
        finally
        {
            if (manager.CurrentScreen is not null)
                manager.Close();
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScaleEqualToCurrentValueStopsFollowingDefaultScale()
    {
        var original = UiSettings.DefaultScale;
        var manager = new UiManager();
        try
        {
            UiSettings.DefaultScale = 1;
            var screen = new UiScreen { Scale = 1 };
            manager.Open(screen);

            UiSettings.DefaultScale = 2;
            manager.Update(new Size(100, 80));

            Assert.Equal(1, screen.Scale);
        }
        finally
        {
            if (manager.CurrentScreen is not null)
                manager.Close();
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void FailedExplicitScaleAssignmentDoesNotStopFollowingDefaultScale()
    {
        var original = UiSettings.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiSettings.DefaultScale = 1;
            screen = new UiScreen();
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = 0);

            UiSettings.DefaultScale = 2;
            screen.Open();

            Assert.Equal(2, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenScreenUsesLatestDefaultScaleWhenReopened()
    {
        var original = UiSettings.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiSettings.DefaultScale = 1;
            screen = new UiScreen();
            screen.Open();
            screen.Close();

            UiSettings.DefaultScale = 2;
            screen.Open();

            Assert.Equal(2, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiSettings.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScalePersistsWhenScreenReopens()
    {
        var original = UiSettings.DefaultScale;
        UiScreen? screen = null;
        try
        {
            UiSettings.DefaultScale = 1;
            screen = new UiScreen { Scale = 1.25 };
            screen.Open();
            screen.Close();

            UiSettings.DefaultScale = 2;
            screen.Open();

            Assert.Equal(1.25, screen.Scale);
        }
        finally
        {
            screen?.Close();
            UiSettings.DefaultScale = original;
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
