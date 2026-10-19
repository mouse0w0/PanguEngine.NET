using PanguEngine.Client.UI;
using PanguEngine.UI;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class UIToolkitScaleTests
{
    [Fact]
    public void DefaultScaleDefaultsToOneAndRejectsInvalidValuesWithoutChangingState()
    {
        var original = UIToolkit.DefaultScale;
        try
        {
            Assert.Equal(1, original);
            UIToolkit.DefaultScale = 1.5;

            Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.DefaultScale = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.DefaultScale = double.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.DefaultScale = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => UIToolkit.DefaultScale = -1);

            Assert.Equal(1.5, UIToolkit.DefaultScale);
        }
        finally
        {
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void SceneUsesCurrentDefaultScaleAtConstructionAndOpening()
    {
        var original = UIToolkit.DefaultScale;
        UIScene? scene = null;
        try
        {
            UIToolkit.DefaultScale = 1.25;
            scene = new UIScene();
            Assert.Equal(1.25, scene.Scale);

            UIToolkit.DefaultScale = 1.5;
            scene.Open();

            Assert.Equal(1.5, scene.Scale);
        }
        finally
        {
            scene?.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenSceneReflowsWhenDefaultScaleChanges()
    {
        var original = UIToolkit.DefaultScale;
        var manager = new UIManager();
        try
        {
            UIToolkit.DefaultScale = 1;
            var root = new LayoutNode();
            var scene = new GameUIScene(root);
            manager.Open(scene);
            manager.UpdateFrame(new Size(100, 80), 0);
            Assert.Equal(new Size(100, 80), root.LastMeasureConstraint);

            UIToolkit.DefaultScale = 2;
            manager.UpdateFrame(new Size(100, 80), 0);

            Assert.Equal(2, scene.Scale);
            Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
            Assert.Equal(2, root.MeasureCalls);
        }
        finally
        {
            if (manager.CurrentScene is not null)
                manager.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScaleEqualToCurrentValueStopsFollowingDefaultScale()
    {
        var original = UIToolkit.DefaultScale;
        var manager = new UIManager();
        try
        {
            UIToolkit.DefaultScale = 1;
            var scene = new GameUIScene { Scale = 1 };
            manager.Open(scene);

            UIToolkit.DefaultScale = 2;
            manager.UpdateFrame(new Size(100, 80), 0);

            Assert.Equal(1, scene.Scale);
        }
        finally
        {
            if (manager.CurrentScene is not null)
                manager.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void FailedExplicitScaleAssignmentDoesNotStopFollowingDefaultScale()
    {
        var original = UIToolkit.DefaultScale;
        UIScene? scene = null;
        try
        {
            UIToolkit.DefaultScale = 1;
            scene = new UIScene();
            Assert.Throws<ArgumentOutOfRangeException>(() => scene.Scale = 0);

            UIToolkit.DefaultScale = 2;
            scene.Open();

            Assert.Equal(2, scene.Scale);
        }
        finally
        {
            scene?.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void UnoverriddenSceneUsesLatestDefaultScaleWhenReopened()
    {
        var original = UIToolkit.DefaultScale;
        UIScene? scene = null;
        try
        {
            UIToolkit.DefaultScale = 1;
            scene = new UIScene();
            scene.Open();
            scene.Close();

            UIToolkit.DefaultScale = 2;
            scene.Open();

            Assert.Equal(2, scene.Scale);
        }
        finally
        {
            scene?.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    [Fact]
    public void ExplicitScalePersistsWhenSceneReopens()
    {
        var original = UIToolkit.DefaultScale;
        UIScene? scene = null;
        try
        {
            UIToolkit.DefaultScale = 1;
            scene = new UIScene { Scale = 1.25 };
            scene.Open();
            scene.Close();

            UIToolkit.DefaultScale = 2;
            scene.Open();

            Assert.Equal(1.25, scene.Scale);
        }
        finally
        {
            scene?.Close();
            UIToolkit.DefaultScale = original;
        }
    }

    private sealed class LayoutNode : UIElement
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