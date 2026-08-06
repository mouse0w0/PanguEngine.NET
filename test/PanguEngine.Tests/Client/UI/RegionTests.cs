using System.ComponentModel;
using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class RegionTests
{
    [Fact]
    public void ColorConstructorsPreserveChannels()
    {
        var opaque = new Color(1, 2, 3);
        var translucent = new Color(4, 5, 6, 7);

        Assert.Equal((byte)1, opaque.R);
        Assert.Equal((byte)2, opaque.G);
        Assert.Equal((byte)3, opaque.B);
        Assert.Equal(byte.MaxValue, opaque.A);
        Assert.Equal((byte)4, translucent.R);
        Assert.Equal((byte)5, translucent.G);
        Assert.Equal((byte)6, translucent.B);
        Assert.Equal((byte)7, translucent.A);
    }

    [Fact]
    public void DefaultColorIsTransparentBlackAndColorsUseValueEquality()
    {
        Assert.Equal(new Color(0, 0, 0, 0), default);
        Assert.Equal(new Color(10, 20, 30, 40), new Color(10, 20, 30, 40));
        Assert.NotEqual(new Color(10, 20, 30, 40), new Color(10, 20, 30, 41));
    }

    [Fact]
    public void SolidColorBrushStoresColorAndUsesValueEquality()
    {
        var first = new SolidColorBrush(new Color(10, 20, 30, 40));
        var equivalent = new SolidColorBrush(new Color(10, 20, 30, 40));
        var different = new SolidColorBrush(new Color(10, 20, 30, 41));

        Assert.Equal(new Color(10, 20, 30, 40), first.Color);
        Assert.True(first.Equals(equivalent));
        Assert.True(first.Equals((object)equivalent));
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.False(first.Equals(different));
        Assert.False(first.Equals(null));
    }

    [Fact]
    public void EquivalentBackgroundDoesNotRaisePropertyChanged()
    {
        var region = new TestRegion
        {
            Background = new SolidColorBrush(new Color(10, 20, 30, 40))
        };
        var changes = 0;
        region.PropertyChanged += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Property, Region.BackgroundProperty))
                changes++;
        };

        region.Background = new SolidColorBrush(new Color(10, 20, 30, 40));

        Assert.Equal(0, changes);
    }

    [Fact]
    public void RegionPropertyDescriptorsExposeOwnerDefaultsAndInvalidation()
    {
        Assert.Equal(nameof(Region.Padding), Region.PaddingProperty.Name);
        Assert.Equal(typeof(Region), Region.PaddingProperty.OwnerType);
        Assert.Equal(typeof(Thickness), Region.PaddingProperty.ValueType);
        Assert.Equal(Thickness.Zero, Region.PaddingProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Measure, Region.PaddingProperty.Invalidation);

        Assert.Equal(nameof(Region.Background), Region.BackgroundProperty.Name);
        Assert.Equal(typeof(Region), Region.BackgroundProperty.OwnerType);
        Assert.Equal(typeof(Brush), Region.BackgroundProperty.ValueType);
        Assert.Null(Region.BackgroundProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Render, Region.BackgroundProperty.Invalidation);
    }

    [Fact]
    public void DefaultRegionStateHasZeroValues()
    {
        var region = new TestRegion();

        Assert.Equal(Thickness.Zero, region.Padding);
        Assert.Null(region.Background);
        Assert.Equal(Rect.Zero, region.DecorationBounds);
        Assert.Equal(Rect.Zero, region.ContentBounds);
    }

    [Fact]
    public void EmptyRegionMeasureRequirementIncludesPadding()
    {
        var region = new TestRegion
        {
            Padding = new Thickness(10, 20, 30, 40)
        };

        region.Measure(new Size(100, 120));

        Assert.Equal(new Size(40, 60), region.DesiredSize);
    }

    [Fact]
    public void DefaultMeasureReducesConstraintAndTakesPerAxisMaximum()
    {
        var region = new TestRegion
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        var first = new TestNode { CoreDesiredSize = new Size(12, 30) };
        var second = new TestNode { CoreDesiredSize = new Size(25, 8) };
        region.Add(first);
        region.Add(second);

        region.Measure(new Size(100, 120));

        Assert.Equal(new Size(60, 60), first.LastMeasureConstraint);
        Assert.Equal(new Size(60, 60), second.LastMeasureConstraint);
        Assert.Equal(new Size(65, 90), region.DesiredSize);
    }

    [Fact]
    public void BackgroundDoesNotAffectValidLayout()
    {
        var region = new TestRegion();
        ValidateLayout(region);
        var desiredSize = region.DesiredSize;
        var layoutBounds = region.LayoutBounds;

        region.Background = new SolidColorBrush(new Color(1, 2, 3));

        Assert.Equal(desiredSize, region.DesiredSize);
        Assert.Equal(layoutBounds, region.LayoutBounds);
        Assert.True(region.IsMeasureValid);
        Assert.True(region.IsArrangeValid);
    }

    [Fact]
    public void PositiveInfinityMeasureConstraintStaysInfiniteAfterPadding()
    {
        var region = new HookRegion
        {
            Padding = new Thickness(10),
            ContentDesiredSize = Size.Zero
        };

        region.Measure(Size.Infinite);

        Assert.Equal(Size.Infinite, region.LastMeasureConstraint);
        Assert.Equal(new Size(20, 20), region.DesiredSize);
    }

    [Fact]
    public void PaddingSumOverflowCollapsesFiniteContentConstraintAndFailsInflation()
    {
        var region = new HookRegion
        {
            Padding = new Thickness(double.MaxValue, 0, double.MaxValue, 0),
            ContentDesiredSize = Size.Zero
        };

        Assert.Throws<InvalidOperationException>(() => region.Measure(new Size(100, 100)));

        Assert.Equal(new Size(0, 100), region.LastMeasureConstraint);
        Assert.False(region.IsMeasureValid);
        Assert.False(region.IsArrangeValid);
    }

    [Fact]
    public void ContentRequirementPlusPaddingOverflowFailsWithoutValidPass()
    {
        var region = new HookRegion
        {
            Padding = new Thickness(double.MaxValue, 0, 0, 0),
            ContentDesiredSize = new Size(double.MaxValue, 0)
        };

        Assert.Throws<InvalidOperationException>(() => region.Measure(new Size(100, 100)));

        Assert.False(region.IsMeasureValid);
        Assert.False(region.IsArrangeValid);
    }

    [Fact]
    public void ArrangeComputesLocalDecorationAndContentBounds()
    {
        var region = new TestRegion
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        region.Measure(new Size(100, 120));

        region.Arrange(new Rect(50, 60, 100, 120));

        Assert.Equal(new Rect(0, 0, 100, 120), region.DecorationBounds);
        Assert.Equal(new Rect(10, 20, 60, 60), region.ContentBounds);
    }

    [Fact]
    public void DefaultArrangePassesSameContentBoundsToEveryChild()
    {
        var region = new TestRegion
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        var first = new TestNode();
        var second = new TestNode();
        region.Add(first);
        region.Add(second);
        region.Measure(new Size(100, 120));

        region.Arrange(new Rect(0, 0, 100, 120));

        var expected = new Rect(10, 20, 60, 60);
        Assert.Equal(expected, first.LayoutBounds);
        Assert.Equal(expected, second.LayoutBounds);
        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(1, second.ArrangeCount);
    }

    [Fact]
    public void ChildMarginExplicitSizeAndAlignmentRemainChildResponsibilities()
    {
        var region = new TestRegion
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        var child = new TestNode
        {
            CoreDesiredSize = new Size(1, 1),
            Margin = new Thickness(5, 6, 7, 8),
            Width = 20,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        region.Add(child);
        region.Measure(new Size(100, 120));

        region.Arrange(new Rect(0, 0, 100, 120));

        Assert.Equal(new Rect(43, 62, 20, 10), child.LayoutBounds);
        Assert.Equal(new Size(20, 10), child.LastArrangeSize);
    }

    [Fact]
    public void OversizedPaddingCollapsesContentBoundsInsideDecoration()
    {
        var region = new HookRegion
        {
            Padding = new Thickness(100),
            ContentDesiredSize = Size.Zero
        };
        region.Measure(new Size(300, 300));

        region.Arrange(new Rect(0, 0, 40, 30));

        Assert.Equal(new Rect(0, 0, 40, 30), region.DecorationBounds);
        Assert.Equal(new Rect(40, 30, 0, 0), region.ContentBounds);
        Assert.Equal(new Rect(40, 30, 0, 0), region.LastArrangeBounds);
    }

    [Fact]
    public void PaddingChangeInvalidatesCommittedBounds()
    {
        var region = new TestRegion();
        ValidateLayout(region);
        Assert.NotEqual(Rect.Zero, region.DecorationBounds);

        region.Padding = new Thickness(1);

        Assert.False(region.IsMeasureValid);
        Assert.False(region.IsArrangeValid);
        Assert.Equal(Rect.Zero, region.DecorationBounds);
        Assert.Equal(Rect.Zero, region.ContentBounds);
    }

    [Fact]
    public void ContentHookExceptionsPropagateWithoutCommittingPasses()
    {
        var measureException = new InvalidOperationException("measure");
        var measureRegion = new HookRegion { MeasureException = measureException };

        Assert.Same(
            measureException,
            Assert.Throws<InvalidOperationException>(() => measureRegion.Measure(new Size(100, 100))));
        Assert.False(measureRegion.IsMeasureValid);

        var arrangeException = new InvalidOperationException("arrange");
        var arrangeRegion = new HookRegion
        {
            ContentDesiredSize = Size.Zero,
            ArrangeException = arrangeException
        };
        arrangeRegion.Measure(new Size(100, 100));

        Assert.Same(
            arrangeException,
            Assert.Throws<InvalidOperationException>(() => arrangeRegion.Arrange(new Rect(0, 0, 100, 100))));
        Assert.False(arrangeRegion.IsArrangeValid);
        Assert.Equal(Rect.Zero, arrangeRegion.DecorationBounds);
        Assert.Equal(Rect.Zero, arrangeRegion.ContentBounds);
    }

    [Fact]
    public void ChildLayoutExceptionsPropagateWithoutCommittingRegionPasses()
    {
        var measureException = new InvalidOperationException("child measure");
        var measureRegion = new TestRegion();
        measureRegion.Add(new TestNode { MeasureException = measureException });

        Assert.Same(
            measureException,
            Assert.Throws<InvalidOperationException>(() => measureRegion.Measure(new Size(100, 100))));
        Assert.False(measureRegion.IsMeasureValid);

        var arrangeException = new InvalidOperationException("child arrange");
        var arrangeRegion = new TestRegion();
        arrangeRegion.Add(new TestNode { ArrangeException = arrangeException });
        arrangeRegion.Measure(new Size(100, 100));

        Assert.Same(
            arrangeException,
            Assert.Throws<InvalidOperationException>(() => arrangeRegion.Arrange(new Rect(0, 0, 100, 100))));
        Assert.False(arrangeRegion.IsArrangeValid);
        Assert.Equal(Rect.Zero, arrangeRegion.DecorationBounds);
    }

    [Fact]
    public void ContentHookInvalidationPreventsPassCommit()
    {
        var region = new HookRegion { ContentDesiredSize = Size.Zero };
        region.Measure(new Size(100, 100));
        region.ArrangeAction = () => region.Padding = new Thickness(1);

        region.Arrange(new Rect(0, 0, 100, 100));

        Assert.False(region.IsMeasureValid);
        Assert.False(region.IsArrangeValid);
        Assert.Equal(Rect.Zero, region.DecorationBounds);
        Assert.Equal(Rect.Zero, region.ContentBounds);
    }

    [Fact]
    public void MeasureContentInvalidationPreventsPassCommit()
    {
        var region = new HookRegion { ContentDesiredSize = Size.Zero };
        region.MeasureAction = () => region.Padding = new Thickness(1);

        region.Measure(new Size(100, 100));

        Assert.False(region.IsMeasureValid);
        Assert.False(region.IsArrangeValid);
        Assert.Equal(Rect.Zero, region.DecorationBounds);
        Assert.Equal(Rect.Zero, region.ContentBounds);
    }

    [Fact]
    public void PaddingChangeInvalidatesRegionAndAncestors()
    {
        var root = new TestRegion();
        var child = new TestRegion();
        root.Add(child);
        ValidateLayout(root);

        child.Padding = new Thickness(1);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void ChildStructureChangeInvalidatesRegionAndAncestors()
    {
        var root = new TestRegion();
        var child = new TestRegion();
        root.Add(child);
        ValidateLayout(root);

        child.Add(new TestNode());

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void InactiveRegionAllowsBackgroundThreadConfiguration()
    {
        var region = new TestRegion();
        var expectedBackground = new SolidColorBrush(new Color(10, 20, 30));

        RunOnBackgroundThread(() =>
        {
            region.Padding = new Thickness(4);
            region.Background = expectedBackground;
            return true;
        });

        Assert.Equal(new Thickness(4), region.Padding);
        Assert.Same(expectedBackground, region.Background);
    }

    [Fact]
    public void ActiveRegionRejectsWrongThreadRenderPropertyPathsWithoutPartialState()
    {
        var region = new TestRegion();
        var original = new SolidColorBrush(new Color(10, 20, 30));
        var changed = new SolidColorBrush(new Color(40, 50, 60));
        region.Background = original;
        var screen = new UiScreen(region);
        ValidateLayout(region);
        screen.Open();

        var setterError = RunOnBackgroundThread(
            () => Record.Exception(() => region.Background = changed));
        Assert.IsType<InvalidOperationException>(setterError);
        Assert.Same(original, region.Background);
        Assert.True(region.IsMeasureValid);
        Assert.True(region.IsArrangeValid);

        var setValueError = RunOnBackgroundThread(
            () => Record.Exception(() => region.SetValue(Region.BackgroundProperty, changed)));
        Assert.IsType<InvalidOperationException>(setValueError);
        Assert.Same(original, region.Background);

        var clearError = RunOnBackgroundThread(
            () => Record.Exception(() => region.ClearValue(Region.BackgroundProperty)));
        Assert.IsType<InvalidOperationException>(clearError);
        Assert.Same(original, region.Background);

        var source = new BackgroundSource { Background = changed };
        var bindError = RunOnBackgroundThread(
            () => Record.Exception(() => region.Bind(Region.BackgroundProperty, source, item => item.Background)));
        Assert.IsType<InvalidOperationException>(bindError);
        Assert.False(region.IsBound(Region.BackgroundProperty));
        Assert.Same(original, region.Background);

        region.Bind(Region.BackgroundProperty, source, item => item.Background);
        Assert.True(region.IsBound(Region.BackgroundProperty));
        Assert.Same(changed, region.Background);

        var update = new SolidColorBrush(new Color(70, 80, 90));
        var updateError = RunOnBackgroundThread(
            () => Record.Exception(() => source.Background = update));
        Assert.IsType<InvalidOperationException>(updateError);
        Assert.Same(changed, region.Background);
        Assert.True(region.IsBound(Region.BackgroundProperty));

        var unbindError = RunOnBackgroundThread(
            () => Record.Exception(() => region.Unbind(Region.BackgroundProperty)));
        Assert.IsType<InvalidOperationException>(unbindError);
        Assert.True(region.IsBound(Region.BackgroundProperty));
        Assert.Same(changed, region.Background);
        Assert.True(region.IsMeasureValid);
        Assert.True(region.IsArrangeValid);

        screen.Close();
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

    private sealed class TestRegion : Region
    {
        internal void Add(UiNode child) => AddChild(child);
    }

    private sealed class HookRegion : Region
    {
        internal Size ContentDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Rect LastArrangeBounds { get; private set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }
        internal Exception? MeasureException { get; set; }
        internal Exception? ArrangeException { get; set; }

        protected override Size MeasureContent(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            MeasureAction?.Invoke();
            if (MeasureException is not null)
                throw MeasureException;
            return ContentDesiredSize;
        }

        protected override void ArrangeContent(Rect contentBounds)
        {
            LastArrangeBounds = contentBounds;
            ArrangeAction?.Invoke();
            if (ArrangeException is not null)
                throw ArrangeException;
        }
    }

    private sealed class TestNode : UiNode
    {
        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal int MeasureCount { get; private set; }
        internal int ArrangeCount { get; private set; }
        internal Exception? MeasureException { get; set; }
        internal Exception? ArrangeException { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCount++;
            LastMeasureConstraint = availableSize;
            if (MeasureException is not null)
                throw MeasureException;
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCount++;
            LastArrangeSize = finalSize;
            if (ArrangeException is not null)
                throw ArrangeException;
        }
    }

    private sealed class BackgroundSource : INotifyPropertyChanged
    {
        private Brush? _background;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Brush? Background
        {
            get => _background;
            set
            {
                if (EqualityComparer<Brush?>.Default.Equals(_background, value))
                    return;
                _background = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Background)));
            }
        }
    }
}
