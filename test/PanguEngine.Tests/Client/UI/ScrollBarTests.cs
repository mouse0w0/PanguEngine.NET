using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;
using Path = PanguEngine.Client.UI.Controls.Path;

namespace PanguEngine.Tests.Client.UI;

public sealed class ScrollBarTests
{
    private static readonly UiPseudoClass HorizontalPseudoClass = UiPseudoClass.Get("horizontal");
    private static readonly UiPseudoClass VerticalPseudoClass = UiPseudoClass.Get("vertical");

    [Fact]
    public void PublicSurfaceAndDefaultsMatchTheScrollBarContract()
    {
        var bar = new ScrollBar();

        Assert.True(typeof(ScrollBar).IsSealed);
        Assert.Equal(typeof(Control), typeof(ScrollBar).BaseType);
        Assert.True(bar.Focusable);
        Assert.Equal(Orientation.Vertical, bar.Orientation);
        Assert.Equal(0d, bar.Minimum);
        Assert.Equal(100d, bar.Maximum);
        Assert.Equal(0d, bar.Value);
        Assert.Equal(0d, bar.ViewportSize);
        Assert.Equal(16d, bar.SmallChange);
        Assert.Equal(100d, bar.LargeChange);
        Assert.True(bar.ShowArrows);
        Assert.True(bar.IsWheelScrollingEnabled);
        Assert.Equal(16d, bar.BarThickness);
        Assert.Equal(16d, bar.MinimumThumbLength);
        Assert.Equal(4, bar.Children.Count);

        Assert.Equal(nameof(ScrollBar.Value), ScrollBar.ValueProperty.Name);
        Assert.Equal(typeof(ScrollBar), ScrollBar.ValueProperty.OwnerType);
        Assert.Equal(Orientation.Vertical, ScrollBar.OrientationProperty.DefaultValue);
        Assert.Equal(0d, ScrollBar.MinimumProperty.DefaultValue);
        Assert.Equal(100d, ScrollBar.MaximumProperty.DefaultValue);
        Assert.Equal(0d, ScrollBar.ValueProperty.DefaultValue);
        Assert.Equal(16d, ScrollBar.SmallChangeProperty.DefaultValue);
        Assert.Equal(100d, ScrollBar.LargeChangeProperty.DefaultValue);
        Assert.True(ScrollBar.ShowArrowsProperty.DefaultValue);
        Assert.True(ScrollBar.IsWheelScrollingEnabledProperty.DefaultValue);
        Assert.Equal(16d, ScrollBar.BarThicknessProperty.DefaultValue);
        Assert.Equal(16d, ScrollBar.MinimumThumbLengthProperty.DefaultValue);
        Assert.True(ScrollBar.MinimumProperty.IsDirect);
        Assert.True(ScrollBar.MaximumProperty.IsDirect);
        Assert.True(ScrollBar.ValueProperty.IsDirect);
        Assert.True(ScrollBar.ViewportSizeProperty.IsDirect);
        Assert.False(ScrollBar.SmallChangeProperty.IsDirect);
        Assert.False(ScrollBar.LargeChangeProperty.IsDirect);
        Assert.False(ScrollBar.BarThicknessProperty.IsDirect);
        Assert.False(ScrollBar.MinimumThumbLengthProperty.IsDirect);
    }

    [Fact]
    public void PartsCarryStableRoleClassesAndTheOrientationPseudoClass()
    {
        var bar = new ScrollBar();
        var expected = new[]
        {
            "scrollbar-track",
            "scrollbar-decrease",
            "scrollbar-increase",
            "scrollbar-thumb"
        };

        var actual = bar.Children
            .Select(child => child.Classes.Single(name => name.StartsWith("scrollbar-", StringComparison.Ordinal)))
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.True(bar.HasPseudoClass(VerticalPseudoClass));
        Assert.False(bar.HasPseudoClass(HorizontalPseudoClass));

        bar.Orientation = Orientation.Horizontal;

        Assert.True(bar.HasPseudoClass(HorizontalPseudoClass));
        Assert.False(bar.HasPseudoClass(VerticalPseudoClass));
        Assert.Equal(expected, bar.Children
            .Select(child => child.Classes.Single(name => name.StartsWith("scrollbar-", StringComparison.Ordinal)))
            .ToArray());
    }

    [Fact]
    public void ArrowPartsUseBarThicknessAndTrackFillsRemainder()
    {
        var bar = new ScrollBar();
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var decrease = FindPart(bar, "decrease");
            var track = FindPart(bar, "track");
            var increase = FindPart(bar, "increase");

            Assert.Equal(new Rect(0, 0, 16, 16), decrease.LayoutBounds);
            Assert.Equal(new Rect(0, 16, 16, 168), track.LayoutBounds);
            Assert.Equal(new Rect(0, 184, 16, 16), increase.LayoutBounds);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void HiddenArrowsReleaseTheirLayoutSpace()
    {
        var bar = new ScrollBar
        {
            ShowArrows = false,
            Minimum = 10,
            Maximum = 110,
            Value = 60
        };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var track = FindPart(bar, "track");
            var thumb = FindPart(bar, "thumb");

            Assert.Equal(new Rect(0, 0, 16, 200), track.LayoutBounds);
            Assert.Equal(16, thumb.LayoutBounds.Height);
            Assert.Equal(92, thumb.LayoutBounds.Y);
            Assert.Equal(Visibility.Collapsed, FindPart(bar, "decrease").Visibility);
            Assert.Equal(Visibility.Collapsed, FindPart(bar, "increase").Visibility);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ZeroRangeFillsTrackAndDisablesBothArrows()
    {
        var bar = new ScrollBar { Minimum = 40, Maximum = 40, Value = 40 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var decrease = FindPart(bar, "decrease");
            var increase = FindPart(bar, "increase");

            Assert.Equal(40, bar.Value);
            Assert.Equal(168, thumb.LayoutBounds.Height);
            Assert.Equal(16, thumb.LayoutBounds.Y);
            Assert.False(decrease.IsEnabled);
            Assert.False(increase.IsEnabled);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ViewportSizeScalesTheThumbProportionally()
    {
        var bar = new ScrollBar { ViewportSize = 100, Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            Assert.Equal(84, thumb.LayoutBounds.Height);
            Assert.Equal(16, thumb.LayoutBounds.Y);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void TrackShorterThanMinimumThumbAndZeroTrackStayInBounds()
    {
        var shortBar = new ScrollBar { ShowArrows = false, Maximum = 1, Value = 0 };
        var (shortManager, _, _) = OpenBarScene(shortBar, 16, 8);
        try
        {
            var thumb = FindPart(shortBar, "thumb");
            Assert.Equal(8, thumb.LayoutBounds.Height);
        }
        finally
        {
            shortManager.Close();
        }

        var emptyBar = new ScrollBar { Value = 0 };
        var (emptyManager, _, _) = OpenBarScene(emptyBar, 16, 0);
        try
        {
            var thumb = FindPart(emptyBar, "thumb");
            var track = FindPart(emptyBar, "track");
            Assert.Equal(0, track.LayoutBounds.Height);
            Assert.Equal(0, thumb.LayoutBounds.Height);
        }
        finally
        {
            emptyManager.Close();
        }
    }

    [Fact]
    public void ValueIsCoercedToTheCurrentRange()
    {
        var bar = new ScrollBar();

        bar.Value = 150;
        Assert.Equal(100, bar.Value);

        bar.Value = -5;
        Assert.Equal(0, bar.Value);

        bar.Maximum = 50;
        bar.Value = 80;
        Assert.Equal(50, bar.Value);

        bar.Minimum = 60;
        Assert.Equal(60, bar.Value);

        bar.Maximum = 200;
        Assert.Equal(60, bar.Value);

        bar.Minimum = 0;
        Assert.Equal(60, bar.Value);
    }

    [Fact]
    public void SetValueAndClrWritesShareTheSameCoercedEffectiveValue()
    {
        var bar = new ScrollBar();
        var changes = new List<double>();
        using var subscription = bar.Subscribe(ScrollBar.ValueProperty, (_, args) => changes.Add(args.NewValue));

        bar.SetValue(ScrollBar.ValueProperty, 150);

        Assert.Equal(100, bar.Value);
        Assert.Equal([100d], changes);

        bar.SetValue(ScrollBar.ValueProperty, 100);
        Assert.Equal([100d], changes);
    }

    [Fact]
    public void NonFiniteInputFailsBeforeCommittingAndPreservesTheOldValue()
    {
        var bar = new ScrollBar { Value = 25 };

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Value = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Minimum = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SmallChange = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.BarThickness = double.NaN);

        Assert.Equal(25, bar.Value);
    }

    [Theory]
    [InlineData(10d)]
    [InlineData(70d)]
    public void ClearingValueClampsTheResetValueAndOnlyNotifiesARealChange(double initialValue)
    {
        var bar = new ScrollBar { Minimum = 10, Value = initialValue };
        var changes = new List<double>();
        using var subscription = bar.Subscribe(ScrollBar.ValueProperty, (_, args) => changes.Add(args.NewValue));

        bar.ClearValue(ScrollBar.ValueProperty);

        Assert.Equal(10, bar.Value);
        if (initialValue == 10)
            Assert.Empty(changes);
        else
            Assert.Equal([10d], changes);

        bar.ClearValue(ScrollBar.ValueProperty);
        Assert.Equal(10, bar.Value);

        bar.Minimum = 0;
        Assert.Equal(10, bar.Value);
    }

    [Fact]
    public void DirectRangeWritesRejectInvalidInputBeforeChangingFields()
    {
        var bar = new ScrollBar { Minimum = 10, Maximum = 200, ViewportSize = 50, Value = 80 };

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ScrollBar.MinimumProperty, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ScrollBar.MaximumProperty, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ScrollBar.ViewportSizeProperty, -1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetValue(ScrollBar.ValueProperty, double.NaN));

        Assert.Equal(10, bar.Minimum);
        Assert.Equal(200, bar.Maximum);
        Assert.Equal(50, bar.ViewportSize);
        Assert.Equal(80, bar.Value);

        bar.ClearValue(ScrollBar.MaximumProperty);
        Assert.Equal(100, bar.Maximum);
        Assert.Equal(80, bar.Value);
    }

    [Fact]
    public void OneWayValueBindingClampsSourceWritesAndKeepsTheSourceUnchanged()
    {
        var source = new ScrollBar { Maximum = 1000, Value = 300 };
        var target = new ScrollBar();
        target.Bind(ScrollBar.ValueProperty, source, ScrollBar.ValueProperty);

        Assert.Equal(100, target.Value);
        Assert.Equal(300, source.Value);

        target.Maximum = 50;
        target.Maximum = 200;
        Assert.Equal(50, target.Value);
        Assert.Equal(300, source.Value);
        Assert.True(target.IsBound(ScrollBar.ValueProperty));

        source.Value = 150;
        Assert.Equal(150, target.Value);
    }

    [Fact]
    public void TwoWayValueBindingAcceptsTheSourceSettersFinalValue()
    {
        var source = new ScrollBar { Maximum = 50, Value = 48 };
        var target = new ScrollBar();
        target.BindTwoWay(ScrollBar.ValueProperty, source, ScrollBar.ValueProperty);

        target.Value = 64;

        Assert.Equal(50, source.Value);
        Assert.Equal(50, target.Value);
    }

    [Fact]
    public void ArrowPressStepsOnceAndPointerClickedDoesNotRepeatIt()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerReleased(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 8));
            manager.ProcessPointerPressed(new Point(8, 8), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, bar.Value);
            manager.ProcessPointerReleased(new Point(8, 8), MouseButton.Left, KeyModifiers.None);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void TrackPressPagesTowardThePointer()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 120));
            manager.ProcessPointerPressed(new Point(8, 120), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(100, bar.Value);
            manager.ProcessPointerReleased(new Point(8, 120), MouseButton.Left, KeyModifiers.None);

            manager.UpdateFrame(new Size(240, 320), 0);
            manager.ProcessPointerMoved(new Point(8, 40));
            manager.ProcessPointerPressed(new Point(8, 40), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(0, bar.Value);
            manager.ProcessPointerReleased(new Point(8, 40), MouseButton.Left, KeyModifiers.None);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ThumbDragKeepsGrabOffsetAndClampsOutsideTheBar()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 24));
            manager.ProcessPointerPressed(new Point(8, 24), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(0, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 32));
            Assert.Equal(8d / 152d * 100d, bar.Value, 9);

            manager.ProcessPointerMoved(new Point(8, 400));
            Assert.Equal(100, bar.Value);

            manager.ProcessPointerReleased(new Point(8, 400), MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerMoved(new Point(8, 100));
            Assert.Equal(100, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void StandaloneWheelMovesAlongTheBarAxisAndConsumesChangedValues()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 100));
            var handled = manager.ProcessPointerWheel(new Point(8, 100), 0, -1, KeyModifiers.None);

            Assert.True(handled);
            Assert.Equal(98, bar.Value);

            bar.Value = 100;
            manager.UpdateFrame(new Size(240, 320), 0);
            manager.ProcessPointerMoved(new Point(8, 100));
            Assert.False(manager.ProcessPointerWheel(new Point(8, 100), 0, -1, KeyModifiers.None));
            Assert.Equal(100, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void HorizontalBarIgnoresAPlainVerticalWheel()
    {
        var bar = new ScrollBar { Orientation = Orientation.Horizontal, Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 200, 16);
        try
        {
            manager.ProcessPointerMoved(new Point(100, 8));
            Assert.False(manager.ProcessPointerWheel(new Point(100, 8), 0, -1, KeyModifiers.None));
            Assert.Equal(50, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void DisablingFocusAndWheelHandlingStillAllowsPointerSteps()
    {
        var bar = new ScrollBar { Focusable = false, IsWheelScrollingEnabled = false };
        Assert.False(bar.Focusable);

        var (manager, screen, _) = OpenBarScene(bar, 16, 200);
        try
        {
            bar.Value = 50;
            manager.UpdateFrame(new Size(240, 320), 0);
            manager.ProcessPointerMoved(new Point(8, 100));

            Assert.False(manager.ProcessPointerWheel(new Point(8, 100), 0, -1, KeyModifiers.None));
            Assert.Equal(50, bar.Value);

            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);
            Assert.Null(screen.FocusedNode);
            manager.ProcessPointerReleased(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
        }
        finally
        {
            manager.Close();
        }

        bar.Focusable = true;
        bar.IsWheelScrollingEnabled = true;
        Assert.True(bar.Focusable);
        Assert.True(bar.IsWheelScrollingEnabled);
    }

    [Fact]
    public void UnmodifiedNavigationKeysMoveAlongTheAxisAndConsume()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            Assert.True(bar.Focus());

            manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessKeyDown(Key.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessKeyDown(Key.PageDown, KeyModifiers.None);
            Assert.Equal(100, bar.Value);

            manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
            Assert.Equal(0, bar.Value);

            manager.ProcessKeyDown(Key.End, KeyModifiers.None);
            Assert.Equal(100, bar.Value);

            manager.ProcessKeyDown(Key.Down, KeyModifiers.Shift);
            Assert.Equal(100, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void HorizontalNavigationUsesLeftAndRight()
    {
        var bar = new ScrollBar { Orientation = Orientation.Horizontal, Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 200, 16);
        try
        {
            Assert.True(bar.Focus());

            manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessKeyDown(Key.Up, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessKeyDown(Key.Left, KeyModifiers.None);
            Assert.Equal(50, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void StyleDrivenOrientationUpdatesThePseudoClassWithoutChangingPartClasses()
    {
        var bar = new ScrollBar { Width = 100, Height = 200 };
        var manager = new UiManager();
        var screen = new GameScreen(bar);
        manager.Open(screen);
        manager.UpdateFrame(new Size(100, 200), 0);

        var before = bar.Children
            .Select(child => child.Classes.ToArray())
            .ToArray();
        Assert.True(bar.HasPseudoClass(VerticalPseudoClass));

        screen.SetStyleSheets([UiStyleSheet.Parse("ScrollBar { orientation: horizontal; }")]);
        manager.UpdateFrame(new Size(100, 200), 0);

        Assert.Equal(Orientation.Horizontal, bar.Orientation);
        Assert.True(bar.HasPseudoClass(HorizontalPseudoClass));
        Assert.False(bar.HasPseudoClass(VerticalPseudoClass));
        Assert.Equal(before, bar.Children.Select(child => child.Classes.ToArray()).ToArray());
        manager.Close();
    }

    [Fact]
    public void OpenScreenCanChangeOrientationWhileTheThumbIsPressed()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, screen, _) = OpenBarScene(bar, 100, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            Assert.True(thumb.IsPressed);

            screen.SetStyleSheets([UiStyleSheet.Parse("ScrollBar { orientation: horizontal; }")]);
            manager.UpdateFrame(new Size(100, 200), 0);

            Assert.Equal(Orientation.Horizontal, bar.Orientation);
            var value = bar.Value;
            manager.ProcessPointerMoved(new Point(90, 190));
            Assert.Equal(value, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void StyleHidingAPressedBarCancelsInputWithoutSelectorMutationErrors()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, screen, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);

            screen.SetStyleSheets([UiStyleSheet.Parse("ScrollBar { visibility: hidden; }")]);
            manager.UpdateFrame(new Size(16, 200), 0);

            Assert.False(thumb.IsPressed);
            Assert.False(thumb.IsHovered);
            var value = bar.Value;
            manager.UpdateFrame(new Size(16, 200), 0, TimeSpan.FromMilliseconds(1000));
            manager.UpdateFrame(new Size(16, 200), 0, TimeSpan.FromMilliseconds(1400));
            Assert.Equal(value, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void DragWithoutMovementKeepsValueWithArrowsAndPadding()
    {
        var bar = new ScrollBar { Value = 50, Padding = new Thickness(3) };
        var (manager, _, _) = OpenBarScene(bar, 30, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerMoved(point);

            Assert.Equal(50, bar.Value, 8);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void FocusShowsABlueBorderAndPreservesThumbPointerFeedback()
    {
        var bar = new ScrollBar { Width = 16, Height = 200 };
        var manager = new UiManager();
        var screen = new GameScreen(bar);
        manager.Open(screen);
        manager.UpdateFrame(new Size(16, 200), 0);

        var thumb = FindPart(bar, "thumb");
        Assert.Equal(new SolidColorBrush(92, 103, 116), thumb.Background);
        Assert.Null(bar.BorderBrush);
        Assert.Equal(Thickness.Zero, bar.BorderThickness);
        Assert.Equal(Thickness.Zero, thumb.BorderThickness);

        Assert.True(bar.Focus());
        manager.UpdateFrame(new Size(16, 200), 0);

        Assert.Equal(new SolidColorBrush(84, 169, 255), bar.BorderBrush);
        Assert.Equal(new Thickness(1), bar.BorderThickness);
        Assert.Equal(new SolidColorBrush(92, 103, 116), thumb.Background);

        var thumbPosition = thumb.LocalToScreen(new Point(2, 2));
        manager.ProcessPointerMoved(thumbPosition);
        manager.UpdateFrame(new Size(16, 200), 0);
        Assert.Equal(new SolidColorBrush(110, 120, 132), thumb.Background);

        manager.ProcessPointerPressed(thumbPosition, MouseButton.Left, KeyModifiers.None);
        manager.UpdateFrame(new Size(16, 200), 0);
        Assert.Equal(new SolidColorBrush(73, 81, 92), thumb.Background);

        manager.ProcessPointerReleased(thumbPosition, MouseButton.Left, KeyModifiers.None);
        manager.UpdateFrame(new Size(16, 200), 0);
        Assert.Equal(new SolidColorBrush(110, 120, 132), thumb.Background);

        screen.ClearFocus();
        manager.ProcessPointerMoved(new Point(-1, -1));
        manager.UpdateFrame(new Size(16, 200), 0);
        Assert.False(bar.IsFocused);
        Assert.Null(bar.BorderBrush);
        Assert.Equal(Thickness.Zero, bar.BorderThickness);
        Assert.Equal(new SolidColorBrush(92, 103, 116), thumb.Background);
        manager.Close();
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void FirstThumbDragSurvivesTheDefaultFocusBorderLayout(Orientation orientation)
    {
        var vertical = orientation == Orientation.Vertical;
        var size = vertical ? new Size(16, 200) : new Size(200, 16);
        var bar = new ScrollBar
        {
            Orientation = orientation,
            Width = size.Width,
            Height = size.Height,
            Value = 40
        };
        var manager = new UiManager();
        manager.Open(new GameScreen(bar));
        manager.UpdateFrame(size, 0);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            Assert.False(bar.IsFocused);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            manager.UpdateFrame(size, 0);

            Assert.True(bar.IsFocused);
            Assert.Equal(new Thickness(1), bar.BorderThickness);
            manager.ProcessPointerMoved(point);
            Assert.Equal(40, bar.Value);

            var moved = vertical ? new Point(point.X, point.Y + 20) : new Point(point.X + 20, point.Y);
            manager.ProcessPointerMoved(moved);
            Assert.True(bar.Value > 40);

            manager.ProcessPointerReleased(moved, MouseButton.Left, KeyModifiers.None);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(Orientation.Vertical, 1d)]
    [InlineData(Orientation.Horizontal, 1d)]
    [InlineData(Orientation.Vertical, 1.25d)]
    [InlineData(Orientation.Horizontal, 1.25d)]
    public void RoundedThumbStaysInsideTrackAndZeroPointerMovementDoesNotChangeValue(
        Orientation orientation, double scale)
    {
        var vertical = orientation == Orientation.Vertical;
        var bar = new ScrollBar { Orientation = orientation, ViewportSize = 17, Value = 100, Focusable = false };
        var (manager, screen, _) = OpenBarScene(bar, vertical ? 16 : 200, vertical ? 200 : 16);
        screen.Scale = scale;
        manager.UpdateFrame(new Size(240, 320), 0);
        try
        {
            var track = FindPart(bar, "track");
            var thumb = FindPart(bar, "thumb");
            var trackEnd = vertical ? track.LayoutBounds.Y + track.LayoutBounds.Height : track.LayoutBounds.X + track.LayoutBounds.Width;
            var thumbEnd = vertical ? thumb.LayoutBounds.Y + thumb.LayoutBounds.Height : thumb.LayoutBounds.X + thumb.LayoutBounds.Width;
            Assert.Equal(trackEnd, thumbEnd, 8);

            bar.Value = 50;
            var local = thumb.LocalToScreen(new Point(2, 2));
            var point = new Point(local.X * scale, local.Y * scale);
            var changes = 0;
            bar.PropertyChanged += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Property, ScrollBar.ValueProperty))
                    changes++;
            };
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerMoved(point);

            Assert.Equal(50, bar.Value);
            Assert.Equal(0, changes);
            manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DisabledBarOrAncestorDimsThumbAndBothArrows(int disabledLevel)
    {
        var view = new ScrollView
        {
            Width = 200,
            Height = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Panel { Width = 100, Height = 600 }
        };
        var host = new Panel();
        host.Children.Add(view);
        var manager = new UiManager();
        manager.Open(new GameScreen(host));
        manager.UpdateFrame(new Size(200, 200), 0);
        try
        {
            view.ScrollTo(0, 50);
            var bar = view.Children.OfType<ScrollBar>().Single(node => node.Orientation == Orientation.Vertical);
            var thumb = FindPart(bar, "thumb");
            var decrease = FindPart(bar, "decrease");
            var increase = FindPart(bar, "increase");
            UiNode disabledNode = disabledLevel switch { 0 => bar, 1 => view, _ => host };
            disabledNode.IsEnabled = false;
            manager.UpdateFrame(new Size(200, 200), 0);

            Assert.Equal(new SolidColorBrush(61, 70, 82), thumb.Background);
            Assert.Equal(new SolidColorBrush(27, 30, 35), decrease.Background);
            Assert.Equal(new SolidColorBrush(27, 30, 35), increase.Background);
            Assert.Equal(new SolidColorBrush(139, 148, 160), Assert.IsType<Path>(decrease.Children.Single()).Fill);
            Assert.Equal(new SolidColorBrush(139, 148, 160), Assert.IsType<Path>(increase.Children.Single()).Fill);

            disabledNode.IsEnabled = true;
            manager.UpdateFrame(new Size(200, 200), 0);

            Assert.Equal(new SolidColorBrush(92, 103, 116), thumb.Background);
            Assert.Equal(new SolidColorBrush(48, 54, 62), decrease.Background);
            Assert.Equal(new SolidColorBrush(48, 54, 62), increase.Background);
            Assert.Equal(new SolidColorBrush(242, 244, 247), Assert.IsType<Path>(decrease.Children.Single()).Fill);
            Assert.Equal(new SolidColorBrush(242, 244, 247), Assert.IsType<Path>(increase.Children.Single()).Fill);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ArrowHoldRepeatsUntilReleaseAndStopsAfterwards()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 195));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1000));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1400));
            Assert.Equal(82, bar.Value);

            manager.ProcessPointerReleased(new Point(8, 195), MouseButton.Left, KeyModifiers.None);
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1800));
            Assert.Equal(82, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void DisablingTheBarCancelsAnInFlightHold()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 195));
            bar.IsEnabled = false;

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1000));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1400));
            Assert.Equal(66, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeavingAndReturningBetweenFramesRestartsTheInitialRepeatDelay(bool track)
    {
        var bar = new ScrollBar { Value = 50, Maximum = 1000, LargeChange = 100 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var inside = new Point(8, track ? 150 : 192);
            manager.ProcessPointerMoved(inside);
            manager.ProcessPointerPressed(inside, MouseButton.Left, KeyModifiers.None);
            var value = bar.Value;
            Assert.Equal(track ? 150 : 66, value);

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.Zero);
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(350));
            Assert.Equal(value, bar.Value);

            manager.ProcessPointerMoved(new Point(100, 150));
            manager.ProcessPointerMoved(inside);
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(400));
            Assert.Equal(value, bar.Value);

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(799));
            Assert.Equal(value, bar.Value);
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(800));
            Assert.Equal(value + (track ? 100 : 16), bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void StandaloneRangeChangeRefreshesGeometryAndKeepsArrangeValid()
    {
        var bar = new ScrollBar
        {
            ShowArrows = false,
            Value = 0,
            BorderThickness = Thickness.Zero,
            Padding = Thickness.Zero
        };
        bar.Measure(new Size(16, 200));
        bar.Arrange(new Rect(0, 0, 16, 200));
        var thumb = FindPart(bar, "thumb");
        Assert.True(bar.IsArrangeValid);
        Assert.Equal(0, thumb.LayoutBounds.Y);

        bar.Value = 50;

        Assert.True(bar.IsArrangeValid);
        Assert.Equal(50, bar.Value);
        Assert.Equal(92, thumb.LayoutBounds.Y);
    }

    [Fact]
    public void SynchronizeRangeDuringDirectArrangeDoesNotInvalidateTheHostPass()
    {
        var bar = new ScrollBar();
        var host = new SyncHost(bar);
        host.Measure(new Size(300, 200));
        host.Arrange(new Rect(0, 0, 250, 150));

        Assert.True(host.IsArrangeValid);
        Assert.True(bar.IsArrangeValid);
        Assert.Equal(25, bar.Value);

        host.Arrange(new Rect(0, 0, 120, 90));

        Assert.True(host.IsArrangeValid);
        Assert.True(bar.IsArrangeValid);
        Assert.Equal(25, bar.Value);
    }

    [Fact]
    public void AncestorDisableStopsAnInFlightArrowHoldWithoutReattaching()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, host) = OpenHostedBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 195));
            host.IsEnabled = false;

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1000));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1400));
            Assert.Equal(66, bar.Value);

            host.IsEnabled = true;
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1800));
            Assert.Equal(66, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void AncestorVisibilityLossStopsAnInFlightArrowHold()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, host) = OpenHostedBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 195));
            host.Visibility = Visibility.Hidden;

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1000));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1400));
            Assert.Equal(66, bar.Value);

            host.Visibility = Visibility.Visible;
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(1800));
            Assert.Equal(66, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void AncestorDisableCancelsAThumbDrag()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, host) = OpenHostedBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            var value = bar.Value;

            host.IsEnabled = false;
            manager.ProcessPointerMoved(new Point(8, 150));
            Assert.Equal(value, bar.Value);

            host.IsEnabled = true;
            manager.UpdateFrame(new Size(240, 320), 0);
            manager.ProcessPointerMoved(new Point(8, 150));
            Assert.Equal(value, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ReattachedBarDoesNotResumeAStaleThumbDrag()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, host) = OpenHostedBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            var value = bar.Value;

            Assert.True(host.Children.Remove(bar));
            manager.UpdateFrame(new Size(240, 320), 0);
            host.Children.Add(bar);
            manager.UpdateFrame(new Size(240, 320), 0);

            manager.ProcessPointerMoved(new Point(8, 150));
            Assert.Equal(value, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void SizeChangeCancelsTheStaleThumbGrabOffset()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            Assert.Equal(0, bar.Value);

            bar.Height = 300;
            manager.UpdateFrame(new Size(240, 320), 0);
            Assert.Equal(0, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 150));
            Assert.Equal(0, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ThumbDragSurvivesValueUpdates()
    {
        var bar = new ScrollBar { Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            var point = thumb.LocalToScreen(new Point(2, 2));
            manager.ProcessPointerMoved(point);
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);

            manager.ProcessPointerMoved(new Point(8, 150));
            var first = bar.Value;
            Assert.True(first > 0);

            manager.ProcessPointerMoved(new Point(8, 160));
            Assert.True(bar.Value > first);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void LongFrameStallExecutesOneRepeatWithoutCatchingUp()
    {
        var bar = new ScrollBar { Value = 50 };
        var (manager, _, _) = OpenBarScene(bar, 16, 200);
        try
        {
            manager.ProcessPointerMoved(new Point(8, 192));
            manager.ProcessPointerPressed(new Point(8, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(66, bar.Value);

            manager.ProcessPointerMoved(new Point(8, 195));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(0));
            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(5000));
            Assert.Equal(82, bar.Value);

            manager.UpdateFrame(new Size(240, 320), 0, TimeSpan.FromMilliseconds(5016));
            Assert.Equal(82, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void FocusedMinimumThumbLengthStyleRefreshesGeometry()
    {
        var bar = new ScrollBar { ShowArrows = false, Value = 0 };
        var (manager, screen, _) = OpenBarScene(bar, 16, 200);
        try
        {
            var thumb = FindPart(bar, "thumb");
            screen.SetStyleSheets([UiStyleSheet.Parse("ScrollBar:focus { minimum-thumb-length: 60; }")]);
            manager.UpdateFrame(new Size(16, 200), 0);
            Assert.Equal(16, thumb.LayoutBounds.Height);

            Assert.True(bar.Focus());
            manager.UpdateFrame(new Size(16, 200), 0);

            Assert.Equal(60, thumb.LayoutBounds.Height);
            Assert.True(bar.IsArrangeValid);
        }
        finally
        {
            manager.Close();
        }
    }

    [Theory]
    [InlineData("bar-thickness")]
    [InlineData("minimum-thumb-length")]
    public void NegativeStyleLengthsAreRejectedBeforeCommit(string propertyName)
    {
        var bar = new ScrollBar();
        var manager = new UiManager();
        var screen = new GameScreen(bar);
        manager.Open(screen);
        manager.UpdateFrame(new Size(16, 200), 0);

        screen.SetStyleSheets([UiStyleSheet.Parse($"ScrollBar {{ {propertyName}: -4; }}")]);
        var error = Assert.Throws<UiStyleParseException>(() => manager.UpdateFrame(new Size(16, 200), 0));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        manager.Close();
    }

    [Fact]
    public void HorizontalBarMergesDeltaXWithShiftDeltaY()
    {
        var bar = new ScrollBar { Orientation = Orientation.Horizontal, Value = 0 };
        var (manager, _, _) = OpenBarScene(bar, 200, 16);
        try
        {
            manager.ProcessPointerMoved(new Point(100, 8));
            var handled = manager.ProcessPointerWheel(new Point(100, 8), -1, -1, KeyModifiers.Shift);

            Assert.True(handled);
            Assert.Equal(96, bar.Value);
        }
        finally
        {
            manager.Close();
        }
    }

    private static (UiManager Manager, GameScreen Screen, ScrollBar Bar) OpenBarScene(
        ScrollBar bar,
        double width,
        double height)
    {
        bar.Width = width;
        bar.Height = height;
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        bar.VerticalAlignment = VerticalAlignment.Top;
        var screen = new GameScreen(bar);
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        manager.UpdateFrame(new Size(240, 320), 0);
        return (manager, screen, bar);
    }

    private static (UiManager Manager, GameScreen Screen, Panel Host) OpenHostedBarScene(
        ScrollBar bar,
        double width,
        double height)
    {
        bar.Width = width;
        bar.Height = height;
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        bar.VerticalAlignment = VerticalAlignment.Top;
        var host = new Panel();
        host.Children.Add(bar);
        var screen = new GameScreen(host);
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        manager.UpdateFrame(new Size(240, 320), 0);
        return (manager, screen, host);
    }

    private static ScrollBarPart FindPart(ScrollBar bar, string role) =>
        Assert.IsType<ScrollBarPart>(bar.Children.Single(
            node => node.Classes.Contains($"scrollbar-{role}")));

    private sealed class SyncHost : Control
    {
        internal SyncHost(ScrollBar bar) => Children.Add(bar);

        protected override Size MeasureContent(Size availableSize)
        {
            var bar = (ScrollBar)Children[0];
            bar.Measure(new Size(16, availableSize.Height));
            return new Size(16, availableSize.Height);
        }

        protected override void ArrangeContent(Rect contentBounds)
        {
            var bar = (ScrollBar)Children[0];
            bar.SynchronizeRange(0, 100, 50, 25);
            bar.Arrange(new Rect(contentBounds.X, contentBounds.Y, 16, contentBounds.Height));
        }
    }
}
