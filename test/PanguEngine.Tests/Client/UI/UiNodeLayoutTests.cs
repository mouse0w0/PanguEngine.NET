using System.ComponentModel;
using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiNodeLayoutTests
{
    [Fact]
    public void DefaultLayoutPropertiesUseAutoZeroInfinityAndStretch()
    {
        var node = new TestNode();

        Assert.True(double.IsNaN(node.Width));
        Assert.True(double.IsNaN(node.Height));
        Assert.Equal(0, node.MinWidth);
        Assert.Equal(0, node.MinHeight);
        Assert.Equal(double.PositiveInfinity, node.MaxWidth);
        Assert.Equal(double.PositiveInfinity, node.MaxHeight);
        Assert.Equal(Thickness.Zero, node.Margin);
        Assert.Equal(HorizontalAlignment.Stretch, node.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, node.VerticalAlignment);
        Assert.Equal(Visibility.Visible, node.Visibility);
        Assert.Equal(Size.Zero, node.DesiredSize);
        Assert.Equal(Rect.Zero, node.LayoutBounds);
        Assert.False(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void MeasurePassSubtractsMarginAndAppliesExplicitMaxAndMin()
    {
        var node = new TestNode
        {
            Width = 50,
            MinWidth = 60,
            MaxWidth = 40,
            MinHeight = 10,
            MaxHeight = 25,
            Margin = new Thickness(2, 3, 4, 5),
            CoreDesiredSize = new Size(5, 50)
        };

        node.Measure(new Size(100, 80));

        Assert.Equal(new Size(50, 25), node.LastMeasureConstraint);
        Assert.Equal(new Size(66, 33), node.DesiredSize);
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void DesiredSizeIncludesMarginAndMayExceedAvailableSize()
    {
        var node = new TestNode
        {
            Margin = new Thickness(2),
            CoreDesiredSize = new Size(100, 50)
        };

        node.Measure(new Size(10, 10));

        Assert.Equal(new Size(6, 6), node.LastMeasureConstraint);
        Assert.Equal(new Size(104, 54), node.DesiredSize);
    }

    [Fact]
    public void MeasureCachesEqualConstraintsAndInvalidatesArrange()
    {
        var node = new TestNode { CoreDesiredSize = new Size(10, 10) };
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 20, 20));

        node.Measure(new Size(100, 100));
        Assert.Equal(1, node.MeasureCoreCalls);
        Assert.True(node.IsArrangeValid);

        node.Measure(new Size(80, 100));
        Assert.Equal(2, node.MeasureCoreCalls);
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void ArrangeRequiresValidMeasureAndCachesEqualRect()
    {
        var node = new TestNode { CoreDesiredSize = new Size(10, 10) };
        var finalRect = new Rect(2, 3, 20, 30);

        Assert.Throws<InvalidOperationException>(() => node.Arrange(finalRect));
        Assert.Equal(0, node.ArrangeCoreCalls);

        node.Measure(new Size(100, 100));
        node.Arrange(finalRect);
        node.Arrange(finalRect);

        Assert.Equal(1, node.ArrangeCoreCalls);
        Assert.True(node.IsArrangeValid);
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left, 12)]
    [InlineData(HorizontalAlignment.Center, 47)]
    [InlineData(HorizontalAlignment.Right, 82)]
    public void ArrangeComputesHorizontalNonStretchBounds(
        HorizontalAlignment alignment,
        double expectedX)
    {
        var node = new TestNode
        {
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 3, 8, 7),
            CoreDesiredSize = new Size(20, 10)
        };
        node.Measure(new Size(100, 100));

        node.Arrange(new Rect(10, 20, 100, 50));

        Assert.Equal(new Rect(expectedX, 23, 20, 10), node.LayoutBounds);
        Assert.Equal(new Size(20, 10), node.LastArrangeSize);
    }

    [Theory]
    [InlineData(VerticalAlignment.Top, 23)]
    [InlineData(VerticalAlignment.Center, 38)]
    [InlineData(VerticalAlignment.Bottom, 53)]
    public void ArrangeComputesVerticalNonStretchBounds(
        VerticalAlignment alignment,
        double expectedY)
    {
        var node = new TestNode
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = alignment,
            Margin = new Thickness(2, 3, 8, 7),
            CoreDesiredSize = new Size(20, 10)
        };
        node.Measure(new Size(100, 100));

        node.Arrange(new Rect(10, 20, 100, 50));

        Assert.Equal(new Rect(12, expectedY, 20, 10), node.LayoutBounds);
    }

    [Fact]
    public void ArrangeStretchesAutoSizeInsideMargin()
    {
        var node = new TestNode
        {
            Margin = new Thickness(2, 3, 8, 7),
            CoreDesiredSize = new Size(20, 10)
        };
        node.Measure(new Size(100, 100));

        node.Arrange(new Rect(10, 20, 100, 50));

        Assert.Equal(new Rect(12, 23, 90, 40), node.LayoutBounds);
        Assert.Equal(new Size(90, 40), node.LastArrangeSize);
    }

    [Fact]
    public void ExplicitSizeOverridesStretchAndMaxUsesCenteredFallback()
    {
        var explicitNode = new TestNode
        {
            Width = 20,
            Height = 10,
            CoreDesiredSize = new Size(5, 5)
        };
        explicitNode.Measure(new Size(100, 50));
        explicitNode.Arrange(new Rect(0, 0, 100, 50));

        Assert.Equal(new Rect(40, 20, 20, 10), explicitNode.LayoutBounds);

        var maxNode = new TestNode
        {
            MaxWidth = 30,
            MaxHeight = 20,
            CoreDesiredSize = new Size(5, 5)
        };
        maxNode.Measure(new Size(100, 50));
        maxNode.Arrange(new Rect(0, 0, 100, 50));

        Assert.Equal(new Rect(35, 15, 30, 20), maxNode.LayoutBounds);
    }

    [Fact]
    public void ArrangeUsesTheSameOffsetsWhenContentOverflowsTheSlot()
    {
        var node = new TestNode
        {
            MinWidth = 30,
            MinHeight = 20,
            CoreDesiredSize = Size.Zero
        };
        node.Measure(new Size(10, 10));

        node.Arrange(new Rect(5, 7, 10, 10));

        Assert.Equal(new Rect(-5, 2, 30, 20), node.LayoutBounds);
    }

    [Fact]
    public void ZeroSlotsAndPositiveInfinityMeasureConstraintsAreDeterministic()
    {
        var infiniteNode = new TestNode
        {
            Margin = new Thickness(2),
            CoreDesiredSize = Size.Zero
        };

        infiniteNode.Measure(Size.Infinite);
        Assert.Equal(Size.Infinite, infiniteNode.LastMeasureConstraint);
        Assert.Equal(new Size(4, 4), infiniteNode.DesiredSize);

        var overflowNode = new TestNode
        {
            Margin = new Thickness(double.MaxValue),
            CoreDesiredSize = Size.Zero
        };
        Assert.Throws<InvalidOperationException>(() =>
            overflowNode.Measure(new Size(double.MaxValue, double.MaxValue)));
        Assert.False(overflowNode.IsMeasureValid);

        var zeroNode = new TestNode { CoreDesiredSize = Size.Zero };
        zeroNode.Measure(Size.Zero);
        zeroNode.Arrange(Rect.Zero);

        Assert.Equal(Size.Zero, zeroNode.LastMeasureConstraint);
        Assert.Equal(Rect.Zero, zeroNode.LayoutBounds);
    }

    [Fact]
    public void InvalidPropertyStateAndCoreResultDoNotValidateThePass()
    {
        var invalidWidth = new TestNode { Width = -1 };

        Assert.Throws<InvalidOperationException>(() =>
            invalidWidth.Measure(new Size(100, 100)));
        Assert.Equal(0, invalidWidth.MeasureCoreCalls);
        Assert.False(invalidWidth.IsMeasureValid);

        var invalidAlignment = new TestNode
        {
            HorizontalAlignment = (HorizontalAlignment)99
        };
        Assert.Throws<InvalidOperationException>(() =>
            invalidAlignment.Measure(new Size(100, 100)));

        var infiniteResult = new TestNode { CoreDesiredSize = Size.Infinite };
        Assert.Throws<InvalidOperationException>(() =>
            infiniteResult.Measure(Size.Infinite));
        Assert.False(infiniteResult.IsMeasureValid);
    }

    [Fact]
    public void CoreExceptionsPropagateWithoutCommittingThePass()
    {
        var measureException = new InvalidOperationException("measure failed");
        var measureNode = new TestNode { MeasureException = measureException };

        var actualMeasureException = Assert.Throws<InvalidOperationException>(() =>
            measureNode.Measure(new Size(100, 100)));
        Assert.Same(measureException, actualMeasureException);
        Assert.Equal(Size.Zero, measureNode.DesiredSize);
        Assert.False(measureNode.IsMeasureValid);

        var arrangeException = new InvalidOperationException("arrange failed");
        var arrangeNode = new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            ArrangeException = arrangeException
        };
        arrangeNode.Measure(new Size(100, 100));

        var actualArrangeException = Assert.Throws<InvalidOperationException>(() =>
            arrangeNode.Arrange(new Rect(1, 2, 20, 30)));
        Assert.Same(arrangeException, actualArrangeException);
        Assert.Equal(Rect.Zero, arrangeNode.LayoutBounds);
        Assert.False(arrangeNode.IsArrangeValid);
    }

    [Fact]
    public void CoreInvalidationIsNotOverwrittenByPassCompletion()
    {
        var measureNode = new TestNode { CoreDesiredSize = new Size(10, 10) };
        measureNode.MeasureAction = measureNode.InvalidateMeasure;

        measureNode.Measure(new Size(100, 100));

        Assert.Equal(Size.Zero, measureNode.DesiredSize);
        Assert.False(measureNode.IsMeasureValid);
        Assert.False(measureNode.IsArrangeValid);

        var arrangeNode = new TestNode { CoreDesiredSize = new Size(10, 10) };
        arrangeNode.Measure(new Size(100, 100));
        arrangeNode.ArrangeAction = arrangeNode.InvalidateArrange;

        arrangeNode.Arrange(new Rect(1, 2, 20, 30));

        Assert.Equal(Rect.Zero, arrangeNode.LayoutBounds);
        Assert.True(arrangeNode.IsMeasureValid);
        Assert.False(arrangeNode.IsArrangeValid);

        var measureInvalidatedArrangeNode = new TestNode
        {
            CoreDesiredSize = new Size(10, 10)
        };
        measureInvalidatedArrangeNode.Measure(new Size(100, 100));
        measureInvalidatedArrangeNode.ArrangeAction = measureInvalidatedArrangeNode.InvalidateMeasure;

        measureInvalidatedArrangeNode.Arrange(new Rect(1, 2, 20, 30));

        Assert.Equal(Rect.Zero, measureInvalidatedArrangeNode.LayoutBounds);
        Assert.False(measureInvalidatedArrangeNode.IsMeasureValid);
        Assert.False(measureInvalidatedArrangeNode.IsArrangeValid);
    }

    [Fact]
    public void ReentrantMeasureDoesNotAllowAnOlderArrangePassToCommit()
    {
        var node = new TestNode { CoreDesiredSize = new Size(10, 10) };
        node.Measure(new Size(100, 100));
        node.ArrangeAction = () =>
        {
            node.ArrangeAction = null;
            node.CoreDesiredSize = new Size(5, 5);
            node.Measure(new Size(50, 50));
        };

        node.Arrange(new Rect(1, 2, 20, 30));

        Assert.Equal(new Size(5, 5), node.DesiredSize);
        Assert.Equal(Rect.Zero, node.LayoutBounds);
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void InvalidateMeasurePropagatesMeasureAndArrangeToAllAncestors()
    {
        var root = new TestParent();
        var middle = new TestParent();
        var child = new TestNode();
        root.Add(middle);
        middle.Add(child);
        ValidateLayout(root);
        ValidateLayout(middle);
        ValidateLayout(child);

        child.InvalidateMeasure();

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(middle.IsMeasureValid);
        Assert.False(middle.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void InvalidateArrangePropagatesArrangeOnlyToAllAncestors()
    {
        var root = new TestParent();
        var middle = new TestParent();
        var child = new TestNode();
        root.Add(middle);
        middle.Add(child);
        ValidateLayout(root);
        ValidateLayout(middle);
        ValidateLayout(child);

        child.InvalidateArrange();

        Assert.True(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.True(middle.IsMeasureValid);
        Assert.False(middle.IsArrangeValid);
        Assert.True(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void RepeatedInvalidationIsIdempotent()
    {
        var root = new TestParent();
        var child = new TestNode();
        root.Add(child);
        ValidateLayout(root);
        ValidateLayout(child);

        child.InvalidateMeasure();
        child.InvalidateMeasure();
        child.InvalidateArrange();

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void TreeStructureHookInvalidatesTheVisitedAncestors()
    {
        var root = new TestParent();
        var parent = new TestParent();
        root.Add(parent);
        ValidateLayout(root);
        ValidateLayout(parent);

        parent.Add(new TestNode());

        Assert.False(parent.IsMeasureValid);
        Assert.False(parent.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void PropertyMetadataConnectsMeasureArrangeAndIgnoresRender()
    {
        var node = new TestNode { CoreDesiredSize = new Size(10, 10) };
        ValidateLayout(node);

        node.HorizontalAlignment = HorizontalAlignment.Left;
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);

        node.Arrange(new Rect(0, 0, 20, 20));
        node.RenderOnlyValue = 1;
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);

        var observedMeasureValid = true;
        node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UiNode.WidthProperty))
                observedMeasureValid = node.IsMeasureValid;
        };
        node.Width = 12;

        Assert.False(observedMeasureValid);
        Assert.False(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void VisibilityPropertyExposesThreeStateMeasureAndRenderMetadata()
    {
        Assert.Equal(nameof(UiNode.Visibility), UiNode.VisibilityProperty.Name);
        Assert.Equal(typeof(UiNode), UiNode.VisibilityProperty.OwnerType);
        Assert.Equal(typeof(UiNode), UiNode.VisibilityProperty.TargetType);
        Assert.Equal(Visibility.Visible, UiNode.VisibilityProperty.DefaultValue);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            UiNode.VisibilityProperty.Invalidation);
        Assert.Equal(
            [Visibility.Visible, Visibility.Hidden, Visibility.Collapsed],
            Enum.GetValues<Visibility>());
    }

    [Fact]
    public void HiddenParticipatesInTheSameLayoutAsVisible()
    {
        var visible = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Margin = new Thickness(2)
        };
        var hidden = new TestNode
        {
            Visibility = Visibility.Hidden,
            CoreDesiredSize = visible.CoreDesiredSize,
            Margin = visible.Margin
        };

        visible.Measure(new Size(100, 80));
        hidden.Measure(new Size(100, 80));
        visible.Arrange(new Rect(5, 7, 60, 40));
        hidden.Arrange(new Rect(5, 7, 60, 40));

        Assert.Equal(visible.DesiredSize, hidden.DesiredSize);
        Assert.Equal(visible.LayoutBounds, hidden.LayoutBounds);
        Assert.Equal(1, hidden.MeasureCoreCalls);
        Assert.Equal(1, hidden.ArrangeCoreCalls);
    }

    [Fact]
    public void CollapsedCommitsZeroLayoutWithoutCallingCoreMethods()
    {
        var node = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Margin = new Thickness(4)
        };
        node.Measure(new Size(100, 80));
        node.Arrange(new Rect(5, 7, 60, 40));
        Assert.NotEqual(Rect.Zero, node.LayoutBounds);

        node.Visibility = Visibility.Collapsed;
        Assert.False(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);

        node.Measure(new Size(100, 80));
        Assert.Equal(Size.Zero, node.DesiredSize);
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
        Assert.Equal(1, node.MeasureCoreCalls);

        node.Arrange(new Rect(5, 7, 60, 40));
        Assert.Equal(Rect.Zero, node.LayoutBounds);
        Assert.True(node.IsArrangeValid);
        Assert.Equal(1, node.ArrangeCoreCalls);
    }

    [Fact]
    public void CollapsedStillRejectsInvalidLayoutPropertiesBeforeCoreMethods()
    {
        var invalidWidth = new TestNode
        {
            Visibility = Visibility.Collapsed,
            Width = -1
        };
        Assert.Throws<InvalidOperationException>(() =>
            invalidWidth.Measure(new Size(100, 100)));
        Assert.Equal(0, invalidWidth.MeasureCoreCalls);

        var invalidVisibility = new TestNode
        {
            Visibility = (Visibility)99
        };
        Assert.Throws<InvalidOperationException>(() =>
            invalidVisibility.Measure(new Size(100, 100)));
        Assert.Equal(0, invalidVisibility.MeasureCoreCalls);
    }

    [Fact]
    public void VisibilityChangesInvalidateNodeAndAncestorsThroughPropertyPaths()
    {
        var root = new TestParent();
        var child = new TestNode { CoreDesiredSize = new Size(10, 10) };
        root.Add(child);
        ValidateLayout(root);
        ValidateLayout(child);

        child.Visibility = Visibility.Hidden;

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);

        ValidateLayout(root);
        ValidateLayout(child);
        var source = new LayoutSource { Visibility = Visibility.Collapsed };
        child.Bind(UiNode.VisibilityProperty, source, item => item.Visibility);

        Assert.Equal(Visibility.Collapsed, child.Visibility);
        Assert.False(child.IsMeasureValid);
        Assert.False(root.IsMeasureValid);
    }

    [Fact]
    public void VisibilityClearBindingUpdateAndUnbindUseThePropertyPipeline()
    {
        var root = new TestParent();
        var child = new TestNode
        {
            Visibility = Visibility.Hidden,
            CoreDesiredSize = new Size(10, 10)
        };
        root.Add(child);
        ValidateLayout(root);
        ValidateLayout(child);

        child.ClearValue(UiNode.VisibilityProperty);

        Assert.Equal(Visibility.Visible, child.Visibility);
        Assert.False(child.IsMeasureValid);
        Assert.False(root.IsMeasureValid);

        ValidateLayout(root);
        ValidateLayout(child);
        var source = new LayoutSource { Visibility = Visibility.Hidden };
        child.Bind(UiNode.VisibilityProperty, source, item => item.Visibility);
        Assert.Equal(Visibility.Hidden, child.Visibility);
        Assert.False(child.IsMeasureValid);

        ValidateLayout(root);
        ValidateLayout(child);
        source.Visibility = Visibility.Collapsed;
        Assert.Equal(Visibility.Collapsed, child.Visibility);
        Assert.False(child.IsMeasureValid);
        Assert.False(root.IsMeasureValid);

        ValidateLayout(root);
        ValidateLayout(child);
        child.Unbind(UiNode.VisibilityProperty);
        source.Visibility = Visibility.Visible;

        Assert.False(child.IsBound(UiNode.VisibilityProperty));
        Assert.Equal(Visibility.Collapsed, child.Visibility);
        Assert.True(child.IsMeasureValid);
        Assert.True(child.IsArrangeValid);
    }

    [Fact]
    public void ActiveVisibilityMutationRejectsWrongThreadWithoutPartialState()
    {
        var dispatcher = new UiDispatcher();
        var node = new TestNode { CoreDesiredSize = new Size(10, 10) };
        ValidateLayout(node);
        node.AttachToTree(dispatcher);

        var setterError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Visibility = Visibility.Hidden));
        var source = new LayoutSource { Visibility = Visibility.Collapsed };
        var bindingError = RunOnBackgroundThread(() =>
            Record.Exception(() =>
                node.Bind(UiNode.VisibilityProperty, source, item => item.Visibility)));

        Assert.IsType<InvalidOperationException>(setterError);
        Assert.IsType<InvalidOperationException>(bindingError);
        Assert.Equal(Visibility.Visible, node.Visibility);
        Assert.False(node.IsBound(UiNode.VisibilityProperty));
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);
    }

    [Fact]
    public void LayoutPropertySetClearAndBindingUseTheSameInvalidationPath()
    {
        var node = new TestNode { Width = 10, CoreDesiredSize = new Size(5, 5) };
        ValidateLayout(node);

        node.ClearValue(UiNode.WidthProperty);
        Assert.False(node.IsMeasureValid);
        Assert.True(double.IsNaN(node.Width));

        ValidateLayout(node);
        var source = new LayoutSource { Width = 20 };
        node.Bind(UiNode.WidthProperty, source, item => item.Width);
        Assert.False(node.IsMeasureValid);
        Assert.Equal(20, node.Width);

        ValidateLayout(node);
        source.Width = 30;
        Assert.False(node.IsMeasureValid);
        Assert.Equal(30, node.Width);

        ValidateLayout(node);
        node.Unbind(UiNode.WidthProperty);
        Assert.True(node.IsMeasureValid);
        Assert.Equal(30, node.Width);
    }

    [Fact]
    public void InactiveNodesAllowBackgroundLayoutOperations()
    {
        var node = RunOnBackgroundThread(() =>
        {
            var created = new TestNode
            {
                Width = 20,
                CoreDesiredSize = new Size(10, 10)
            };
            created.Measure(new Size(100, 100));
            created.Arrange(new Rect(1, 2, 20, 30));
            created.InvalidateArrange();
            return created;
        });

        Assert.Equal(20, node.Width);
        Assert.True(node.IsMeasureValid);
        Assert.False(node.IsArrangeValid);
    }

    [Fact]
    public void ActiveNodesRejectWrongThreadLayoutMutationWithoutPartialState()
    {
        var dispatcher = new UiDispatcher();
        var node = new TestNode { Width = 10, CoreDesiredSize = new Size(5, 5) };
        ValidateLayout(node);
        node.AttachToTree(dispatcher);

        var setError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Width = 20));
        Assert.IsType<InvalidOperationException>(setError);
        Assert.Equal(10, node.Width);
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);

        var invalidateError = RunOnBackgroundThread(() =>
            Record.Exception(node.InvalidateMeasure));
        Assert.IsType<InvalidOperationException>(invalidateError);
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);

        var measureError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Measure(new Size(100, 100))));
        var arrangeError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Arrange(new Rect(0, 0, 100, 100))));
        Assert.IsType<InvalidOperationException>(measureError);
        Assert.IsType<InvalidOperationException>(arrangeError);
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);

        var source = new LayoutSource { Width = 30 };
        var bindError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Bind(UiNode.WidthProperty, source, item => item.Width)));
        Assert.IsType<InvalidOperationException>(bindError);
        Assert.False(node.IsBound(UiNode.WidthProperty));
        Assert.Equal(10, node.Width);

        var twoWaySource = new LayoutSource { Width = node.Width };
        node.BindTwoWay(UiNode.WidthProperty, twoWaySource, item => item.Width);
        var equalSetError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.SetValue(UiNode.WidthProperty, node.Width)));
        Assert.IsType<InvalidOperationException>(equalSetError);

        var clearError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.ClearValue(UiNode.WidthProperty)));
        var unbindError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Unbind(UiNode.WidthProperty)));
        var bindingWriteError = RunOnBackgroundThread(() =>
            Record.Exception(() => twoWaySource.Width = 20));
        Assert.IsType<InvalidOperationException>(clearError);
        Assert.IsType<InvalidOperationException>(unbindError);
        Assert.IsType<InvalidOperationException>(bindingWriteError);
        Assert.Equal(10, node.Width);
        Assert.True(node.IsBound(UiNode.WidthProperty));
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);
    }

    [Fact]
    public void BindingRechecksLayoutAccessAfterInitialSourceEvaluation()
    {
        var dispatcher = new UiDispatcher();
        var node = new TestNode { Width = 10, CoreDesiredSize = new Size(5, 5) };
        ValidateLayout(node);
        node.AttachToTree(dispatcher);
        var source = new LayoutSource
        {
            Width = 20,
            WidthReadAction = dispatcher.Shutdown
        };

        Assert.Throws<ObjectDisposedException>(() =>
            node.Bind(UiNode.WidthProperty, source, item => item.Width));

        Assert.Equal(10, node.Width);
        Assert.False(node.IsBound(UiNode.WidthProperty));
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);
    }

    private static void ValidateLayout(UiNode node)
    {
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 100, 100));
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }

    private sealed class TestNode : UiNode
    {
        internal static readonly UiProperty<int> RenderOnlyProperty =
            UiProperty.Register<TestNode, int>(
                nameof(RenderOnlyValue),
                invalidation: UiPropertyInvalidation.Render);

        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal int MeasureCoreCalls { get; private set; }
        internal int ArrangeCoreCalls { get; private set; }
        internal Exception? MeasureException { get; set; }
        internal Exception? ArrangeException { get; set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }

        internal int RenderOnlyValue
        {
            get => GetValue(RenderOnlyProperty);
            set => SetValue(RenderOnlyProperty, value);
        }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCoreCalls++;
            LastMeasureConstraint = availableSize;
            if (MeasureException is not null)
                throw MeasureException;

            MeasureAction?.Invoke();
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCoreCalls++;
            LastArrangeSize = finalSize;
            if (ArrangeException is not null)
                throw ArrangeException;

            ArrangeAction?.Invoke();
        }
    }

    private sealed class TestParent : Parent
    {
        internal void Add(UiNode child) =>
            AddChild(child);
    }

    private sealed class LayoutSource : INotifyPropertyChanged
    {
        private double _width;
        private Visibility _visibility = Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        internal Action? WidthReadAction { get; set; }

        public double Width
        {
            get
            {
                WidthReadAction?.Invoke();
                return _width;
            }
            set
            {
                if (_width.Equals(value))
                    return;

                _width = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
            }
        }

        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility == value)
                    return;

                _visibility = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility)));
            }
        }
    }
}
