using System.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class ScrollViewTests
{
    [Fact]
    public void DefaultStateAndPropertyDescriptorsMatchSpecification()
    {
        var view = new ScrollView();

        Assert.Null(view.Content);
        Assert.Equal(ScrollBarVisibility.Disabled, view.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, view.VerticalScrollBarVisibility);
        Assert.True(view.ShowArrows);
        Assert.True(view.IsScrollChainingEnabled);
        Assert.Equal(Point.Zero, view.Offset);
        Assert.Equal(Size.Zero, view.Extent);
        Assert.Equal(Size.Zero, view.Viewport);
        Assert.Equal(16d, view.SmallChange);

        Assert.Equal(nameof(ScrollView.Content), ScrollView.ContentProperty.Name);
        Assert.Equal(typeof(ScrollView), ScrollView.ContentProperty.OwnerType);
        Assert.Equal(ScrollBarVisibility.Disabled, ScrollView.HorizontalScrollBarVisibilityProperty.DefaultValue);
        Assert.Equal(ScrollBarVisibility.Auto, ScrollView.VerticalScrollBarVisibilityProperty.DefaultValue);
        Assert.True(ScrollView.ShowArrowsProperty.DefaultValue);
        Assert.True(ScrollView.IsScrollChainingEnabledProperty.DefaultValue);
        Assert.Equal(Point.Zero, ScrollView.OffsetProperty.DefaultValue);
        Assert.Equal(16d, ScrollView.SmallChangeProperty.DefaultValue);
        Assert.Equal(Size.Zero, ScrollView.ExtentProperty.DefaultValue);
        Assert.Equal(Size.Zero, ScrollView.ViewportProperty.DefaultValue);
        Assert.True(ScrollView.ExtentProperty.IsReadOnly);
        Assert.True(ScrollView.ViewportProperty.IsReadOnly);
        Assert.True(view.Focusable);
        Assert.True(ScrollView.OffsetProperty.IsDirect);
        Assert.False(ScrollView.SmallChangeProperty.IsDirect);
    }

    [Fact]
    public void DefaultBaseStyleSheetStylesScrollViewChrome()
    {
        var view = new ScrollView();
        var screen = new UiScreen(view);

        screen.Root!.UpdateStyles();

        Assert.Equal(new Thickness(1), view.BorderThickness);
        Assert.NotNull(view.BorderBrush);
        Assert.NotNull(view.Background);
        Assert.Contains(
            view.GetStyleValueSources(Region.BorderThicknessProperty),
            source => source.Origin == UiStyleOrigin.Base);
    }

    [Fact]
    public void SmallChangeRejectsNegativeAndNonFiniteValuesWithThePropertyParameter()
    {
        var view = new ScrollView();

        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => view.SmallChange = -1);
        Assert.Equal(nameof(ScrollView.SmallChange), negative.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.SmallChange = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.SmallChange = double.NaN);
        Assert.Equal(16d, view.SmallChange);
    }

    [Fact]
    public void VerticalAutoShowsBarAndClampsProgrammaticOffset()
    {
        var view = new ScrollView
        {
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(50, 600), view.Extent);
        Assert.Equal(new Size(184, 200), view.Viewport);

        view.ScrollTo(0, 1000);

        Assert.Equal(0, view.Offset.X);
        Assert.Equal(400, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void DisabledAxesDoNotScrollAndDoNotReserveBars()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Panel { Width = 400, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(400, 600), view.Extent);
        Assert.Equal(new Size(200, 200), view.Viewport);

        view.ScrollTo(100, 100);

        Assert.Equal(Point.Zero, view.Offset);
        manager.Close();
    }

    [Fact]
    public void VisibleAxisAlwaysReservesItsBar()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Panel { Width = 50, Height = 50 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(200, 184), view.Viewport);
        Assert.Equal(new Size(50, 50), view.Extent);
        manager.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisablingAnAxisBeforeFirstLayoutImmediatelyClearsItsOffset(bool horizontal)
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Offset = new Point(80, 120)
        };
        var changes = new List<Point>();
        using var subscription = view.Subscribe(ScrollView.OffsetProperty, (_, args) => changes.Add(args.NewValue));
        var property = horizontal
            ? ScrollView.HorizontalScrollBarVisibilityProperty
            : ScrollView.VerticalScrollBarVisibilityProperty;
        var expected = horizontal ? new Point(0, 120) : new Point(80, 0);

        view.SetValue(property, ScrollBarVisibility.Disabled);

        Assert.Equal(expected, view.Offset);
        Assert.Equal([expected], changes);

        view.SetValue(property, ScrollBarVisibility.Hidden);
        Assert.Equal(expected, view.Offset);
        Assert.Equal([expected], changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisablingALaidOutAxisImmediatelySynchronizesTheBindingAndBar(bool horizontal)
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Panel { Width = 400, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        try
        {
            var source = new OffsetSource { Offset = new Point(80, 120) };
            view.BindTwoWay(ScrollView.OffsetProperty, source, item => item.Offset);
            var changes = new List<Point>();
            using var subscription = view.Subscribe(ScrollView.OffsetProperty, (_, args) => changes.Add(args.NewValue));
            var property = horizontal
                ? ScrollView.HorizontalScrollBarVisibilityProperty
                : ScrollView.VerticalScrollBarVisibilityProperty;
            var expected = horizontal ? new Point(0, 120) : new Point(80, 0);
            var bar = view.Children.OfType<ScrollBar>()
                .Single(item => item.Orientation == (horizontal ? Orientation.Horizontal : Orientation.Vertical));

            view.SetValue(property, ScrollBarVisibility.Disabled);

            Assert.Equal(expected, view.Offset);
            Assert.Equal(expected, source.Offset);
            Assert.Equal(0, bar.Value);
            Assert.Equal([expected], changes);

            view.SetValue(property, ScrollBarVisibility.Visible);
            manager.PrepareFrame(new Size(200, 200), 0);
            Assert.Equal(expected, view.Offset);
            Assert.Equal(expected, source.Offset);
            Assert.Equal(0, bar.Value);
            Assert.Equal([expected], changes);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void HiddenAxisScrollsWithoutReservingABar()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Panel { Width = 400, Height = 50 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(200, 200), view.Viewport);
        Assert.Equal(new Size(400, 50), view.Extent);

        view.ScrollTo(1000, 0);

        Assert.Equal(200, view.Offset.X);
        manager.Close();
    }

    [Fact]
    public void TwoAutoAxesMutuallyTriggerBothBars()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 400, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(400, 600), view.Extent);
        Assert.Equal(new Size(184, 184), view.Viewport);
        manager.Close();
    }

    [Fact]
    public void ContentMarginIsIncludedInExtent()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel
            {
                Width = 50,
                Height = 600,
                Margin = new Thickness(10)
            }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(new Size(70, 620), view.Extent);
        manager.Close();
    }

    [Fact]
    public void ShrinkingContentConvergesOffsetAndRange()
    {
        var content = new Panel { Width = 400, Height = 600 };
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        view.ScrollTo(0, 400);
        Assert.Equal(400, view.Offset.Y);

        content.Height = 100;
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Equal(new Size(400, 100), view.Extent);
        Assert.Equal(new Size(200, 200), view.Viewport);
        Assert.Equal(0, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void GrowingContentKeepsResolvedOffsetWithoutRevivingClampedHistory()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        view.ScrollTo(0, 9999);

        content.Height = 1200;
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Equal(new Size(50, 1200), view.Extent);
        Assert.Equal(400, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void OffsetRequestBeforeFirstLayoutIsPreservedForEnabledAxisOnly()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        view.Offset = new Point(50, 120);

        Assert.Equal(0, view.Offset.X);
        Assert.Equal(120, view.Offset.Y);

        view.Content = new Panel { Width = 50, Height = 100 };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(0, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void ScrollMethodsShareThePropertyConstraintPath()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        view.ScrollTo(0, 1000);
        Assert.Equal(400, view.Offset.Y);

        view.ScrollBy(0, -50);
        Assert.Equal(350, view.Offset.Y);

        view.ScrollBy(0, -10000);
        Assert.Equal(0, view.Offset.Y);

        Assert.Throws<ArgumentOutOfRangeException>(() => view.ScrollTo(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.ScrollBy(0, double.PositiveInfinity));
        Assert.Equal(0, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void SetValueAndClrSetterAndClampAgree()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        view.SetValue(ScrollView.OffsetProperty, new Point(0, 9999));
        Assert.Equal(400, view.Offset.Y);

        view.Offset = new Point(0, 200);
        Assert.Equal(200, view.Offset.Y);

        view.SetValue(ScrollView.OffsetProperty, new Point(0, -5));
        Assert.Equal(0, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void TwoWayOffsetBindingReceivesEffectiveValues()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var source = new OffsetSource();
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        view.BindTwoWay(ScrollView.OffsetProperty, source, item => item.Offset);

        source.Offset = new Point(0, 9999);

        Assert.Equal(400, view.Offset.Y);
        Assert.Equal(9999, source.Offset.Y);

        view.ScrollTo(0, 100);

        Assert.Equal(100, view.Offset.Y);
        Assert.Equal(100, source.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void ClearingOffsetResetsTheDirectPropertyAndDetachesItsBinding()
    {
        var view = new ScrollView { Content = new Panel { Width = 50, Height = 600 } };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        try
        {
            var source = new OffsetSource { Offset = new Point(0, 80) };
            view.Bind(ScrollView.OffsetProperty, source, item => item.Offset);
            var changes = new List<Point>();
            using var subscription = view.Subscribe(ScrollView.OffsetProperty, (_, args) => changes.Add(args.NewValue));

            view.ClearValue(ScrollView.OffsetProperty);
            view.ClearValue(ScrollView.OffsetProperty);

            Assert.Equal(Point.Zero, view.Offset);
            Assert.Equal([Point.Zero], changes);
            Assert.False(view.IsBound(ScrollView.OffsetProperty));
            Assert.Equal(80, source.Offset.Y);
            Assert.Equal(0, view.Children.OfType<ScrollBar>()
                .Single(item => item.Orientation == Orientation.Vertical).Value);

            source.Offset = new Point(0, 100);
            Assert.Equal(Point.Zero, view.Offset);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void OneWayOffsetBindingConvergesAfterLayoutWithoutChangingTheSourceOrRevivingHistory()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView { Content = content };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        try
        {
            var source = new OffsetSource { Offset = new Point(0, 350) };
            view.Bind(ScrollView.OffsetProperty, source, item => item.Offset);

            content.Height = 250;
            manager.PrepareFrame(new Size(200, 200), 0);
            Assert.Equal(50, view.Offset.Y);
            Assert.Equal(350, source.Offset.Y);
            Assert.True(view.IsBound(ScrollView.OffsetProperty));

            content.Height = 600;
            manager.PrepareFrame(new Size(200, 200), 0);
            Assert.Equal(50, view.Offset.Y);
            Assert.Equal(350, source.Offset.Y);

            source.Offset = new Point(0, 100);
            Assert.Equal(100, view.Offset.Y);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void TwoWayOffsetBindingSourceClampResynchronizesTheInternalBar()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var source = new ClampingOffsetSource(maximum: 50) { Offset = new Point(0, 48) };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        view.BindTwoWay(ScrollView.OffsetProperty, source, item => item.Offset);

        var bar = view.Children.OfType<ScrollBar>()
            .Single(item => item.Orientation == Orientation.Vertical);
        Assert.Equal(48, view.Offset.Y);
        Assert.Equal(48, bar.Value);

        bar.Value = 64;

        Assert.Equal(50, source.Offset.Y);
        Assert.Equal(50, view.Offset.Y);
        Assert.Equal(50, bar.Value);
        manager.Close();
    }

    [Fact]
    public void EmptyAndCollapsedContentProduceZeroExtentAndOffset()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Equal(Size.Zero, view.Extent);
        Assert.Equal(new Size(200, 200), view.Viewport);
        Assert.Equal(Point.Zero, view.Offset);

        view.Offset = new Point(0, 300);
        view.Content = new Panel
        {
            Width = 400,
            Height = 600,
            Visibility = Visibility.Collapsed
        };
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Equal(Size.Zero, view.Extent);
        Assert.Equal(Point.Zero, view.Offset);
        manager.Close();
    }

    [Fact]
    public void ReplacingContentMovesNodesThroughTheViewport()
    {
        var first = new Panel();
        var second = new Panel();
        var view = new ScrollView { Content = first };

        Assert.NotNull(first.Parent);
        Assert.Same(view, first.Parent!.Parent);
        Assert.Null(second.Parent);

        view.Content = second;

        Assert.Null(first.Parent);
        Assert.NotNull(second.Parent);
        Assert.Same(view, second.Parent!.Parent);

        view.Content = null;

        Assert.Null(second.Parent);
        Assert.Null(first.Parent);
    }

    [Fact]
    public void ContentMovedToAnotherParentClearsTheProperty()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView { Content = content };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        var other = new Panel();
        other.Children.Add(content);

        Assert.Null(view.Content);
        Assert.Same(other, content.Parent);
        Assert.Contains(content, other.Children);

        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Equal(Size.Zero, view.Extent);
        manager.Close();
    }

    [Fact]
    public void ContentMovedToAnotherParentCanBeReattachedThroughTheViewport()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        var other = new Panel();
        other.Children.Add(content);
        Assert.Null(view.Content);

        view.Content = content;

        Assert.Same(content, view.Content);
        Assert.Same(view, content.Parent!.Parent);
        Assert.DoesNotContain(content, other.Children);

        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Equal(new Size(50, 600), view.Extent);
        manager.Close();
    }

    [Fact]
    public void ConsecutiveFramesStayLayoutValidAfterContentChange()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView { Content = content };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);

        content.Height = 800;
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);

        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);
        manager.Close();
    }

    [Fact]
    public void InfiniteMeasureResolvesFiniteViewportWithoutBars()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 400, Height = 600 }
        };

        view.Measure(Size.Infinite);

        Assert.Equal(new Size(400, 600), view.DesiredSize);
        Assert.Equal(new Size(400, 600), view.Extent);
        Assert.Equal(new Size(400, 600), view.Viewport);
    }

    [Fact]
    public void InternalBarsDisableFocusAndTheirOwnWheelHandling()
    {
        var view = new ScrollView();
        var bars = view.Children.OfType<ScrollBar>().ToArray();

        Assert.Equal(2, bars.Length);
        foreach (var bar in bars)
        {
            Assert.False(bar.Focusable);
            Assert.False(bar.IsWheelScrollingEnabled);
        }
    }

    [Fact]
    public void WheelPreservesFractionalDeltasAndShiftRoutesHorizontally()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Panel { Width = 400, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        manager.ProcessPointerMoved(new Point(50, 50));

        Assert.True(manager.ProcessPointerWheel(new Point(50, 50), 0, -0.5, KeyModifiers.None));
        Assert.Equal(24, view.Offset.Y);

        manager.PrepareFrame(new Size(200, 200), 0);

        var verticalBar = view.Children.OfType<ScrollBar>()
            .Single(bar => bar.Orientation == Orientation.Vertical);
        var barPosition = verticalBar.LocalToScreen(new Point(
            verticalBar.LayoutBounds.Width / 2,
            verticalBar.LayoutBounds.Height / 2));
        Assert.True(manager.ProcessPointerWheel(barPosition, 0, -1, KeyModifiers.Shift));
        Assert.Equal(48, view.Offset.X);
        Assert.Equal(24, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void WheelAtBoundaryRespectsScrollChaining()
    {
        var chained = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (chainedManager, _, _) = OpenScene(chained, new Size(200, 200));
        chained.ScrollTo(0, 400);
        chainedManager.PrepareFrame(new Size(200, 200), 0);
        chainedManager.ProcessPointerMoved(new Point(50, 50));

        Assert.False(chainedManager.ProcessPointerWheel(new Point(50, 50), 0, -1, KeyModifiers.None));
        chainedManager.Close();

        var blocking = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsScrollChainingEnabled = false,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (blockingManager, _, _) = OpenScene(blocking, new Size(200, 200));
        blocking.ScrollTo(0, 400);
        blockingManager.PrepareFrame(new Size(200, 200), 0);
        blockingManager.ProcessPointerMoved(new Point(50, 50));

        Assert.True(blockingManager.ProcessPointerWheel(new Point(50, 50), 0, -1, KeyModifiers.None));
        blockingManager.Close();
    }

    [Fact]
    public void KeyboardNavigationHonorsAxisModifiersAndHandled()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        Assert.True(view.Focus());

        Assert.True(manager.ProcessKeyDown(Key.Down, KeyModifiers.None));
        Assert.Equal(16, view.Offset.Y);

        Assert.False(manager.ProcessKeyDown(Key.Down, KeyModifiers.Shift));
        Assert.Equal(16, view.Offset.Y);

        Assert.False(manager.ProcessKeyDown(Key.Right, KeyModifiers.None));

        Assert.True(manager.ProcessKeyDown(Key.PageDown, KeyModifiers.None));
        Assert.Equal(216, view.Offset.Y);

        Assert.True(manager.ProcessKeyDown(Key.End, KeyModifiers.None));
        Assert.Equal(400, view.Offset.Y);

        Assert.True(manager.ProcessKeyDown(Key.Home, KeyModifiers.None));
        Assert.Equal(0, view.Offset.Y);

        view.KeyDown += static (_, eventArgs) => eventArgs.Handled = true;
        Assert.True(manager.ProcessKeyDown(Key.Down, KeyModifiers.None));
        Assert.Equal(0, view.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void HitTestingFollowsOffsetAndClipsOutsideTheViewport()
    {
        var content = new Canvas { Width = 400, Height = 600 };
        var child = new TestNode { Width = 50, Height = 50 };
        Canvas.SetLeft(child, 0);
        Canvas.SetTop(child, 100);
        content.Children.Add(child);
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        Assert.Same(child, view.HitTest(new Point(5, 105)));

        view.ScrollTo(0, 50);
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Same(child, view.HitTest(new Point(5, 55)));
        Assert.NotSame(child, view.HitTest(new Point(190, 5)));
        manager.Close();
    }

    [Fact]
    public void BarDrivenOffsetInvalidatesAndRearrangesTheContent()
    {
        var content = new Panel { Width = 100, Height = 600 };
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        var bar = view.Children.OfType<ScrollBar>()
            .Single(item => item.Orientation == Orientation.Vertical);
        Assert.True(view.IsArrangeValid);

        bar.Value = 100;

        Assert.Equal(100, view.Offset.Y);
        Assert.False(view.IsArrangeValid);

        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.True(view.IsArrangeValid);
        Assert.True(content.IsArrangeValid);
        Assert.Equal(-100, content.LayoutBounds.Y);
        manager.Close();
    }

    [Fact]
    public void HiddenBarsIgnoreExplicitSizeAndStayInvisibleAndUnhittable()
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Panel { Width = 400, Height = 600 }
        };
        var bars = view.Children.OfType<ScrollBar>().ToArray();
        foreach (var bar in bars)
        {
            bar.Width = 200;
            bar.Height = 16;
        }

        var (manager, _, _) = OpenScene(view, new Size(200, 200));

        foreach (var bar in bars)
        {
            Assert.False(bar.IsHitTestVisible);
            Assert.Equal(0d, bar.Opacity);
        }

        var hit = view.HitTest(new Point(10, 10));
        foreach (var bar in bars)
            Assert.False(IsSelfOrDescendant(hit, bar));
        manager.Close();
    }

    [Fact]
    public void AutoBarTogglesChromeWithItsVisibility()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        var bar = view.Children.OfType<ScrollBar>().Single(item => item.Orientation == Orientation.Vertical);

        Assert.True(bar.IsHitTestVisible);
        Assert.Equal(1d, bar.Opacity);

        content.Height = 100;
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.False(bar.IsHitTestVisible);
        Assert.Equal(0d, bar.Opacity);

        content.Height = 600;
        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.True(bar.IsHitTestVisible);
        Assert.Equal(1d, bar.Opacity);
        manager.Close();
    }

    [Fact]
    public void ArrangeSizeDifferentFromMeasureConvergesInOneLayoutPass()
    {
        var scroll = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };
        var root = new Canvas { Width = 300, Height = 200 };
        root.Children.Add(scroll);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        screen.SetBaseStyleSheets([]);
        manager.Open(screen);

        manager.PrepareFrame(new Size(300, 200), 0);

        Assert.True(scroll.IsMeasureValid);
        Assert.True(scroll.IsArrangeValid);
        Assert.Equal(new Size(50, 600), scroll.Viewport);
        Assert.Equal(Point.Zero, scroll.Offset);

        manager.PrepareFrame(new Size(300, 200), 0);

        Assert.True(scroll.IsMeasureValid);
        Assert.True(scroll.IsArrangeValid);
        manager.Close();
    }

    [Fact]
    public void OneWayBoundContentIsClearedWithoutThrowingWhenMovedOut()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var source = new ContentSource { Child = content };
        var view = new ScrollView { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        view.Bind(ScrollView.ContentProperty, source, item => item.Child);
        Assert.Same(content, view.Content);

        var other = new Panel();
        other.Children.Add(content);

        Assert.Null(view.Content);
        Assert.Same(other, content.Parent);
        Assert.Same(content, source.Child);
        manager.Close();
    }

    [Fact]
    public void ContentRemovalNotificationCanInstallAReplacementOnTheNextFrame()
    {
        var content = new Panel { Width = 50, Height = 600 };
        var replacement = new Panel { Width = 50, Height = 300 };
        var view = new ScrollView { Content = content };
        var (manager, screen, _) = OpenScene(view, new Size(200, 200));
        var notificationRaised = false;

        view.PropertyChanged += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Property, ScrollView.ContentProperty) &&
                eventArgs.NewValue is null &&
                eventArgs.OldValue is not null)
            {
                notificationRaised = true;
                screen.Post(() => view.Content = replacement);
            }
        };

        var other = new Panel();
        other.Children.Add(content);

        Assert.True(notificationRaised);
        Assert.Null(view.Content);

        manager.PrepareFrame(new Size(200, 200), 0);

        Assert.Same(replacement, view.Content);
        Assert.NotNull(replacement.Parent);
        Assert.Same(view, replacement.Parent!.Parent);
        Assert.Same(other, content.Parent);
        manager.Close();
    }

    [Fact]
    public void NestedScrollViewChainsWheelAtInnerBoundary()
    {
        var inner = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 300,
            Height = 300,
            Content = new Panel { Width = 100, Height = 800 }
        };
        var outerContent = new Canvas { Width = 300, Height = 1000 };
        outerContent.Children.Add(inner);
        var outer = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = outerContent
        };
        var (manager, _, _) = OpenScene(outer, new Size(300, 300));

        inner.ScrollTo(0, 500);
        manager.PrepareFrame(new Size(300, 300), 0);
        Assert.Equal(500, inner.Offset.Y);

        manager.ProcessPointerMoved(new Point(100, 150));
        Assert.True(manager.ProcessPointerWheel(new Point(100, 150), 0, -1, KeyModifiers.None));

        Assert.Equal(500, inner.Offset.Y);
        Assert.Equal(48, outer.Offset.Y);
        manager.Close();
    }

    [Fact]
    public void HandledTextBoxKeyDoesNotScrollTheView()
    {
        using var context = new UiTextTestContext();
        var textBox = new TextBox { Width = 400, Height = 100, Text = "abc" };
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = textBox
        };
        var (manager, _, _) = OpenScene(view, new Size(200, 200));
        Assert.True(textBox.Focus());

        Assert.True(manager.ProcessKeyDown(Key.Left, KeyModifiers.None));

        Assert.Equal(0, view.Offset.X);
        manager.Close();
    }

    [Fact]
    public void LocalAndScreenCoordinatesMatchAfterScrolling()
    {
        var content = new Panel { Width = 100, Height = 600 };
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        var (manager, screen, _) = OpenScene(view, new Size(200, 200));
        view.ScrollTo(0, 100);
        manager.PrepareFrame(new Size(200, 200), 0);

        var local = new Point(5, 105);
        var screenPoint = content.LocalToScreen(local);

        Assert.Equal(local, content.ScreenToLocal(screenPoint));
        Assert.Same(content, screen.HitTest(screenPoint));
        manager.Close();
    }

    [Fact]
    public void ManualArrangeWithDifferentSizeStaysValid()
    {
        var view = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Panel { Width = 50, Height = 600 }
        };

        view.Measure(new Size(200, 200));
        view.Arrange(new Rect(0, 0, 300, 150));

        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);
    }

    [Theory]
    [InlineData(1d, true)]
    [InlineData(1.25d, true)]
    [InlineData(1d, false)]
    public void FractionalBarThicknessMatchesViewportAndCornerGeometry(double scale, bool useLayoutRounding)
    {
        var view = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Panel { Width = 600, Height = 600 }
        };
        var (manager, screen, _) = OpenScene(view, new Size(200, 200));
        screen.Scale = scale;
        screen.UseLayoutRounding = useLayoutRounding;
        screen.SetStyleSheets([UiStyleSheet.Parse("ScrollBar { bar-thickness: 16.5px; }")]);
        manager.PrepareFrame(new Size(200 * scale, 200 * scale), 0);
        try
        {
            var horizontal = view.Children.OfType<ScrollBar>().Single(node => node.Orientation == Orientation.Horizontal);
            var vertical = view.Children.OfType<ScrollBar>().Single(node => node.Orientation == Orientation.Vertical);
            var viewport = Assert.IsType<ScrollViewport>(view.Children[0]);
            var corner = view.Children.Single(node => node.Classes.Contains("scrollview-corner"));

            Assert.Equal(viewport.LayoutBounds.Size, view.Viewport);
            Assert.Equal(view.Viewport.Width, vertical.LayoutBounds.X, 8);
            Assert.Equal(view.Viewport.Height, horizontal.LayoutBounds.Y, 8);
            Assert.Equal(200, vertical.LayoutBounds.X + vertical.LayoutBounds.Width, 8);
            Assert.Equal(200, horizontal.LayoutBounds.Y + horizontal.LayoutBounds.Height, 8);
            Assert.Equal(new Rect(vertical.LayoutBounds.X, horizontal.LayoutBounds.Y,
                vertical.LayoutBounds.Width, horizontal.LayoutBounds.Height), corner.LayoutBounds);

            view.ScrollTo(600, 600);
            Assert.Equal(new Point(view.Extent.Width - view.Viewport.Width, view.Extent.Height - view.Viewport.Height), view.Offset);
        }
        finally
        {
            manager.Close();
        }
    }

    private static bool IsSelfOrDescendant(UiNode? node, UiNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    private static (UiManager Manager, UiScreen Screen, ScrollView View) OpenScene(
        ScrollView view,
        Size viewport)
    {
        var manager = new UiManager();
        var screen = new UiScreen(view);
        screen.SetBaseStyleSheets([]);
        manager.Open(screen);
        manager.PrepareFrame(viewport, 0);
        return (manager, screen, view);
    }

    private sealed class TestNode : UiNode
    {
    }

    private sealed class ContentSource : INotifyPropertyChanged
    {
        private UiNode? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UiNode? Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                    return;

                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class OffsetSource : INotifyPropertyChanged
    {
        private Point _offset;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Point Offset
        {
            get => _offset;
            set
            {
                if (_offset == value)
                    return;

                _offset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Offset)));
            }
        }
    }

    private sealed class ClampingOffsetSource : INotifyPropertyChanged
    {
        private readonly double _maximum;
        private Point _offset;

        public ClampingOffsetSource(double maximum) => _maximum = maximum;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Point Offset
        {
            get => _offset;
            set
            {
                var clamped = new Point(value.X, Math.Min(value.Y, _maximum));
                if (_offset == clamped)
                    return;

                _offset = clamped;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Offset)));
            }
        }
    }
}
