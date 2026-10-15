using System.Globalization;
using System.Numerics;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class NumericUpDownTests
{
    [Fact]
    public void DefaultsAndDirectWritesShareTheRangeContract()
    {
        var node = new NumericUpDown<int>();
        Assert.Equal(0, node.Value);
        Assert.Equal(0, node.Minimum);
        Assert.Equal(100, node.Maximum);
        Assert.Equal(1, node.Increment);
        Assert.True(NumericUpDown<int>.ValueProperty.IsDirect);
        Assert.True(NumericUpDown<int>.MinimumProperty.IsDirect);
        Assert.True(NumericUpDown<int>.MaximumProperty.IsDirect);
        node.SetValue(NumericUpDown<int>.ValueProperty, 150);
        Assert.Equal(100, node.Value);
        node.Value = -10;
        Assert.Equal(0, node.Value);
    }

    [Fact]
    public void LatestBoundaryWinsAndNotificationsObserveAnOrderedRange()
    {
        var node = new NumericUpDown<int> { Value = 10 };
        node.PropertyChanged += (_, _) =>
        {
            Assert.True(node.Minimum <= node.Maximum);
            Assert.InRange(node.Value, node.Minimum, node.Maximum);
        };
        node.Minimum = 120;
        Assert.Equal(120, node.Maximum);
        Assert.Equal(120, node.Value);
        node.Maximum = -20;
        Assert.Equal(-20, node.Minimum);
        Assert.Equal(-20, node.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoundaryNotificationReentrancyPublishesEachCommittedValueChangeOnce(bool changeMinimum)
    {
        var node = new NumericUpDown<int> { Value = 10 };
        var changes = new List<(int OldValue, int NewValue)>();
        using var valueSubscription = node.Subscribe(NumericUpDown<int>.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));
        var dependentProperty = changeMinimum
            ? NumericUpDown<int>.MaximumProperty
            : NumericUpDown<int>.MinimumProperty;
        using var boundarySubscription = node.Subscribe(dependentProperty, (_, change) =>
        {
            if (change.NewValue != (changeMinimum ? 120 : -20))
                return;
            if (changeMinimum)
                node.Maximum = 150;
            else
                node.Minimum = -50;
            node.Value = changeMinimum ? 140 : -40;
        });

        if (changeMinimum)
            node.Minimum = 120;
        else
            node.Maximum = -20;

        Assert.Equal(changeMinimum ? new[] { (10, 120), (120, 140) } : new[] { (10, -20), (-20, -40) }, changes);
        Assert.Equal(changeMinimum ? 140 : -40, node.Value);
        Assert.InRange(node.Value, node.Minimum, node.Maximum);
    }

    [Fact]
    public void InvalidNumericAssignmentsAreRejected()
    {
        var node = new NumericUpDown<double>();
        Assert.Throws<ArgumentException>(() => node.Value = double.NaN);
        Assert.Throws<ArgumentException>(() => node.Minimum = double.NegativeInfinity);
        Assert.Throws<ArgumentException>(() => node.Maximum = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => node.Increment = double.NaN);
        Assert.Throws<ArgumentException>(() => node.Increment = -1);
        Assert.Equal(0, node.Value);
        Assert.Equal(1, node.Increment);
    }

    [Fact]
    public void ValueBindingUsesEffectiveValuesAndOnlyNotifiesRealChanges()
    {
        var source = new NumericUpDown<int> { Value = 40 };
        var target = new NumericUpDown<int>();
        target.BindTwoWay(NumericUpDown<int>.ValueProperty, source, NumericUpDown<int>.ValueProperty);
        var changes = 0;
        target.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, NumericUpDown<int>.ValueProperty))
                changes++;
        };
        target.Value = 200;
        Assert.Equal(100, target.Value);
        Assert.Equal(100, source.Value);
        target.Value = 100;
        Assert.Equal(1, changes);
        source.Value = 15;
        Assert.Equal(15, target.Value);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void ValidInputUpdatesValueWithoutRewritingUserText()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<decimal>(new() { Value = 10 });
        scene.Edit("001.00");
        Assert.Equal(1m, scene.Node.Value);
        Assert.Equal("001.00", scene.Editor.Text);
        Assert.True(scene.Editor.CanUndo);
        scene.Key(Key.Enter);
        Assert.Equal("1.00", scene.Editor.Text);
        Assert.False(scene.Editor.CanUndo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("no")]
    [InlineData("2147483648")]
    public void InvalidInputRemainsUntilConfirmation(string input)
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit(input);
        Assert.Equal(10, scene.Node.Value);
        Assert.Equal(input, scene.Editor.Text);
        scene.Key(Key.Enter);
        Assert.Equal(10, scene.Node.Value);
        Assert.Equal("10", scene.Editor.Text);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    public void NonFiniteParsedTextDoesNotBecomeAValue(string input)
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<double>(new() { Value = 10 });
        scene.Edit(input);
        scene.Key(Key.Enter);
        Assert.Equal(10, scene.Node.Value);
        Assert.Equal("10", scene.Editor.Text);
    }

    [Fact]
    public void ConfirmationClampsParsableOutOfRangeText()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("150");
        Assert.Equal(10, scene.Node.Value);
        scene.Key(Key.Enter);
        Assert.Equal(100, scene.Node.Value);
        scene.Edit("-20");
        scene.Screen.ClearFocus();
        Assert.Equal(0, scene.Node.Value);
        Assert.Equal("0", scene.Editor.Text);
    }

    [Fact]
    public void EscapeRestoresLatestAcceptedValue()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("20");
        scene.Edit("-");
        scene.Key(Key.Escape);
        Assert.Equal(20, scene.Node.Value);
        Assert.Equal("20", scene.Editor.Text);
    }

    [Fact]
    public void ExternalWritesPreserveEditingTextAndInvalidConfirmationUsesLatestValue()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("-");
        scene.Node.Value = 25;
        Assert.Equal("-", scene.Editor.Text);
        scene.Key(Key.Enter);
        Assert.Equal("25", scene.Editor.Text);
        scene.Edit("30");
        scene.Node.Value = 40;
        scene.Key(Key.Enter);
        Assert.Equal(30, scene.Node.Value);
        scene.Edit("80");
        scene.Node.Maximum = 50;
        Assert.Equal("80", scene.Editor.Text);
        scene.Key(Key.Enter);
        Assert.Equal(50, scene.Node.Value);
    }

    [Fact]
    public void UntouchedRoundedTextNeverOverwritesFullPrecisionOrExternalValue()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<decimal>(new() { FormatString = "F2", Value = 1.234m });
        scene.Node.Focus();
        Assert.Equal("1.23", scene.Editor.Text);
        scene.Key(Key.Enter);
        Assert.Equal(1.234m, scene.Node.Value);
        scene.Node.Value = 2.345m;
        Assert.Equal("1.23", scene.Editor.Text);
        scene.Screen.ClearFocus();
        Assert.Equal(2.345m, scene.Node.Value);
        Assert.Equal("2.35", scene.Editor.Text);
    }

    [Fact]
    public void ProviderIsCapturedPerInstanceAndUsedForParsingAndFormatting()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var first = new NumericUpDown<decimal>();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var second = new NumericUpDown<decimal>();
            Assert.Equal("de-DE", ((CultureInfo)first.FormatProvider).Name);
            Assert.Equal("en-US", ((CultureInfo)second.FormatProvider).Name);
            using var context = new UiTextTestContext();
            using var scene = new Scene<decimal>(first, useInvariantProvider: false);
            scene.Edit("1,26");
            Assert.Equal(1.26m, first.Value);
            first.FormatString = "F1";
            Assert.Equal("1,26", scene.Editor.Text);
            scene.Key(Key.Enter);
            Assert.Equal("1,3", scene.Editor.Text);
            Assert.Equal(1.26m, first.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ClearingFormatProviderRestoresInvariantCultureAndDetachesBinding()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var node = new NumericUpDown<decimal> { Value = 1.25m };
            var source = new NumericUpDown<decimal>
            {
                FormatProvider = CultureInfo.GetCultureInfo("fr-FR")
            };
            node.Bind(NumericUpDown.FormatProviderProperty, source, NumericUpDown.FormatProviderProperty);
            using var context = new UiTextTestContext();
            using var scene = new Scene<decimal>(node, useInvariantProvider: false);
            Assert.Equal("1,25", scene.Editor.Text);

            node.ClearValue(NumericUpDown.FormatProviderProperty);
            source.FormatProvider = CultureInfo.GetCultureInfo("de-DE");

            Assert.False(node.IsBound(NumericUpDown.FormatProviderProperty));
            Assert.Same(CultureInfo.InvariantCulture, node.FormatProvider);
            Assert.Equal("1.25", scene.Editor.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void SteppingConfirmsTextBeforeApplyingTheRequestedDirection()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new());
        scene.Edit("150");
        scene.Key(Key.Down);
        Assert.Equal(99, scene.Node.Value);
        Assert.Equal("99", scene.Editor.Text);
    }

    [Fact]
    public void UnsignedStepsSaturateWithoutNegatingIncrementOrWrapping()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<uint>(new() { Maximum = uint.MaxValue, Value = uint.MaxValue - 1, Increment = 10 });
        scene.Node.Focus();
        scene.Key(Key.Up);
        Assert.Equal(uint.MaxValue, scene.Node.Value);
        scene.Node.Value = 2;
        scene.Key(Key.Down);
        Assert.Equal(0u, scene.Node.Value);
    }

    [Fact]
    public void DecimalAndArbitraryPrecisionIntegerStepsKeepTheirTypePrecision()
    {
        using var context = new UiTextTestContext();
        using var decimalScene = new Scene<decimal>(new() { Value = 1.000000000000000000000000001m, Increment = 0.000000000000000000000000001m });
        decimalScene.Node.Focus();
        decimalScene.Key(Key.Up);
        Assert.Equal(1.000000000000000000000000002m, decimalScene.Node.Value);
        var value = BigInteger.Pow(10, 100);
        using var integerScene = new Scene<BigInteger>(new() { Maximum = value + 100, Value = value });
        integerScene.Node.Focus();
        integerScene.Key(Key.Up);
        Assert.Equal(value + 1, integerScene.Node.Value);
    }

    [Fact]
    public void FloatingOverflowSaturatesToTheFiniteBoundary()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<double>(new() { Minimum = -double.MaxValue, Maximum = double.MaxValue, Value = double.MaxValue / 2, Increment = double.MaxValue });
        scene.Node.Focus();
        scene.Key(Key.Up);
        Assert.Equal(double.MaxValue, scene.Node.Value);
        scene.Node.Value = -double.MaxValue / 2;
        scene.Key(Key.Down);
        Assert.Equal(-double.MaxValue, scene.Node.Value);
    }

    [Fact]
    public void ReadOnlyDiscardsUnacceptedTextAndStillAllowsSelectionAndExternalUpdates()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("150");
        scene.Node.IsReadOnly = true;
        Assert.Equal("10", scene.Editor.Text);
        scene.Manager.ProcessTextInput("20");
        scene.Key(Key.Up);
        Assert.Equal(10, scene.Node.Value);
        scene.Key(Key.A, KeyModifiers.Control);
        Assert.Equal("10", scene.Editor.SelectedText);
        scene.Node.Value = 25;
        Assert.Equal("25", scene.Editor.Text);
        scene.Node.IsReadOnly = false;
        scene.Edit("30");
        Assert.Equal(30, scene.Node.Value);
    }

    [Fact]
    public void FocusAndPointerStepsKeepTheTextEditorAsTheOnlyActualFocus()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        Assert.True(scene.Node.Focus());
        Assert.False(scene.Node.IsFocused);
        Assert.Same(scene.Editor, scene.Screen.FocusedNode);
        Assert.True(scene.Editor.IsFocusWithin);
        Assert.True(scene.Node.IsFocusWithin);
        Assert.True(scene.Root.IsFocusWithin);
        Assert.True(scene.Editor.HasPseudoClass(UiPseudoClass.FocusWithin));
        Assert.True(scene.Node.HasPseudoClass(UiPseudoClass.FocusWithin));
        Assert.True(scene.Root.HasPseudoClass(UiPseudoClass.FocusWithin));
        var point = scene.PartCenter("increase");
        scene.Press(point);
        Assert.Equal(11, scene.Node.Value);
        Assert.Same(scene.Editor, scene.Screen.FocusedNode);
        Assert.True(scene.Node.IsFocusWithin);
        Assert.True(scene.Node.HasPseudoClass(UiPseudoClass.FocusWithin));
        scene.Release(point);
        Assert.Equal(11, scene.Node.Value);
        scene.Edit("150");
        Assert.True(scene.Other.Focus());
        Assert.Equal(100, scene.Node.Value);
        Assert.False(scene.Editor.IsFocusWithin);
        Assert.False(scene.Node.IsFocusWithin);
        Assert.True(scene.Root.IsFocusWithin);
        Assert.False(scene.Editor.HasPseudoClass(UiPseudoClass.FocusWithin));
        Assert.False(scene.Node.HasPseudoClass(UiPseudoClass.FocusWithin));
        Assert.True(scene.Root.HasPseudoClass(UiPseudoClass.FocusWithin));
    }

    [Fact]
    public void PointerCanDecreaseOutOfRangeTextEvenWhenTheOldValueIsAtMinimum()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new());
        scene.Edit("150");
        Assert.Equal(0, scene.Node.Value);
        Assert.Equal("150", scene.Editor.Text);
        scene.Frame(0);
        var point = scene.PartCenter("decrease");
        Assert.True(scene.Manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None));
        Assert.Equal(99, scene.Node.Value);
        Assert.Same(scene.Editor, scene.Screen.FocusedNode);
        scene.Release(point);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingAndRefocusingDuringRangeCleanupKeepsTheNewEditingSession(bool remove)
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        var otherRoot = new Canvas();
        var otherScreen = new GameScreen(otherRoot);
        var otherManager = new UiManager();
        var transferred = false;
        using var subscription = scene.Editor.Subscribe(UiNode.IsFocusWithinProperty, (_, args) =>
        {
            if (args.NewValue || transferred)
                return;
            transferred = true;
            otherRoot.Children.Add(scene.Node);
            Assert.True(scene.Node.Focus());
        });
        otherManager.Open(otherScreen);
        try
        {
            Assert.True(scene.Node.Focus());
            if (remove)
                scene.Root.Children.Remove(scene.Node);
            else
                Assert.True(scene.Other.Focus());
            Assert.True(transferred);
            Assert.Same(scene.Editor, otherScreen.FocusedNode);
            Assert.True(scene.Editor.IsFocused);
            Assert.True(scene.Node.IsFocusWithin);
            otherManager.UpdateFrame(new Size(400, 120), 0, TimeSpan.Zero);
            otherManager.ProcessKeyDown(Key.A, KeyModifiers.Control);
            otherManager.ProcessTextInput("25");
            Assert.Equal("25", scene.Editor.Text);
            Assert.Equal(25, scene.Node.Value);
            otherManager.ProcessKeyDown(Key.A, KeyModifiers.Control);
            otherManager.ProcessTextInput("150");
            Assert.Equal("150", scene.Editor.Text);
            Assert.Equal(25, scene.Node.Value);
            otherManager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
            Assert.Equal(100, scene.Node.Value);
            Assert.Equal("100", scene.Editor.Text);
        }
        finally
        {
            otherManager.Close();
        }
    }

    [Fact]
    public void LongPressRepeatsAfterDelayAndResetsDelayWhenPointerReturns()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        var point = scene.PartCenter("increase");
        scene.Press(point);
        scene.Frame(0);
        scene.Frame(399);
        Assert.Equal(11, scene.Node.Value);
        scene.Frame(400);
        Assert.Equal(12, scene.Node.Value);
        scene.Frame(450);
        Assert.Equal(13, scene.Node.Value);
        scene.Manager.ProcessPointerMoved(new Point(300, 80));
        scene.Frame(500);
        scene.Manager.ProcessPointerMoved(point);
        scene.Frame(550);
        scene.Frame(949);
        Assert.Equal(13, scene.Node.Value);
        scene.Frame(950);
        Assert.Equal(14, scene.Node.Value);
        scene.Release(point);
        scene.Frame(2000);
        Assert.Equal(14, scene.Node.Value);
    }

    [Fact]
    public void LayoutMovementPausesLongPressAndReturningWaitsForTheFullDelay()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Press(scene.PartCenter("increase"));
        scene.Frame(0);
        Canvas.SetTop(scene.Node, 60);
        scene.Frame(50);
        scene.Frame(400);
        Assert.Equal(11, scene.Node.Value);
        Canvas.SetTop(scene.Node, 0);
        scene.Frame(450);
        scene.Frame(500);
        scene.Frame(899);
        Assert.Equal(11, scene.Node.Value);
        scene.Frame(900);
        Assert.Equal(12, scene.Node.Value);
    }

    [Theory]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Control)]
    public void EnterAndEscapeWorkWithModifiers(KeyModifiers modifiers)
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("150");
        scene.Key(Key.Enter, modifiers);
        Assert.Equal(100, scene.Node.Value);
        scene.Edit("25");
        scene.Edit("-");
        scene.Key(Key.Escape, modifiers);
        Assert.Equal(25, scene.Node.Value);
        Assert.Equal("25", scene.Editor.Text);
    }

    [Fact]
    public void ClickingInsideTheFocusedEditorPreservesFractionalWheelInput()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Node.Focus();
        var point = scene.Editor.LocalToScreen(new Point(10, 10));
        scene.Manager.ProcessPointerWheel(point, 0, 0.5);
        scene.Press(point);
        scene.Release(point);
        scene.Manager.ProcessPointerWheel(point, 0, 0.5);
        Assert.Equal(11, scene.Node.Value);
    }

    [Fact]
    public void EqualBoundariesStopLongPressAndRejectFurtherSteps()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Press(scene.PartCenter("increase"));
        scene.Frame(0);
        scene.Node.Minimum = 20;
        scene.Node.Maximum = 20;
        scene.Frame(400);
        scene.Frame(800);
        scene.Key(Key.Up);
        scene.Key(Key.Down);
        Assert.Equal(20, scene.Node.Value);
    }

    [Fact]
    public void ChangingProviderDuringEditingPreservesTextAndUsesTheNewParser()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<decimal>(new() { Value = 10 });
        scene.Edit("-");
        scene.Node.FormatProvider = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("-", scene.Editor.Text);
        scene.Edit("2,5");
        Assert.Equal(2.5m, scene.Node.Value);
        scene.Key(Key.Enter);
        Assert.Equal("2,5", scene.Editor.Text);
    }

    [Fact]
    public void DisabledNumericControlCannotReceiveFocusOrPointerSteps()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10, IsEnabled = false });
        Assert.False(scene.Node.Focus());
        scene.Press(scene.PartCenter("increase"));
        scene.Frame(0);
        scene.Frame(400);
        Assert.Equal(10, scene.Node.Value);
        Assert.Null(scene.Screen.FocusedNode);
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("zero")]
    [InlineData("detach")]
    [InlineData("window")]
    [InlineData("close")]
    public void LifecycleChangesCancelLongPressWithoutResuming(string change)
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Press(scene.PartCenter("increase"));
        scene.Frame(0);
        switch (change)
        {
            case "readonly": scene.Node.IsReadOnly = true; scene.Node.IsReadOnly = false; break;
            case "disabled": scene.Node.IsEnabled = false; scene.Node.IsEnabled = true; break;
            case "hidden": scene.Node.Visibility = Visibility.Hidden; scene.Node.Visibility = Visibility.Visible; break;
            case "zero": scene.Node.Increment = 0; scene.Node.Increment = 1; break;
            case "detach": scene.Root.Children.Remove(scene.Node); scene.Root.Children.Add(scene.Node); break;
            case "window": scene.Manager.ProcessFocusChanged(false); scene.Manager.ProcessFocusChanged(true); break;
            case "close": scene.Manager.Close(); scene.Manager.Open(scene.Screen); break;
        }
        scene.Frame(400);
        scene.Frame(1000);
        Assert.Equal(11, scene.Node.Value);
    }

    [Fact]
    public void PrecisionLimitedLongPressStopsWithoutRepeatedNotifications()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<double>(new() { Maximum = 1e30, Value = 1e20, Increment = 1 });
        var changes = 0;
        scene.Node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, NumericUpDown<double>.ValueProperty))
                changes++;
        };
        scene.Press(scene.PartCenter("increase"));
        scene.Frame(0);
        scene.Frame(400);
        scene.Frame(800);
        Assert.Equal(1e20, scene.Node.Value);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void WheelAccumulatesWholeUnitsOnlyWhileFocusedAndConsumesBoundaryInput()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        var point = scene.Editor.LocalToScreen(new Point(10, 10));
        Assert.False(scene.Manager.ProcessPointerWheel(point, 0, 1));
        scene.Node.Focus();
        Assert.True(scene.Manager.ProcessPointerWheel(point, 0, 0.4));
        Assert.Equal(10, scene.Node.Value);
        Assert.True(scene.Manager.ProcessPointerWheel(point, 0, 0.6));
        Assert.Equal(11, scene.Node.Value);
        scene.Node.Value = 100;
        scene.Frame(0);
        Assert.Same(scene.Editor, scene.Screen.HitTest(point));
        Assert.True(scene.Manager.ProcessPointerWheel(point, 0, 1));
        Assert.Equal(100, scene.Node.Value);
        scene.Node.IsReadOnly = true;
        scene.Frame(0);
        Assert.Same(scene.Editor, scene.Screen.HitTest(point));
        Assert.False(scene.Manager.ProcessPointerWheel(point, 0, 1));
        scene.Node.IsReadOnly = false;
        scene.Node.Increment = 0;
        scene.Frame(0);
        Assert.Same(scene.Editor, scene.Screen.HitTest(point));
        Assert.False(scene.Manager.ProcessPointerWheel(point, 0, 1));
    }

    [Fact]
    public void ModifiedArrowKeysDoNotStepAndZeroIncrementLeavesEditingTextAlone()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("150");
        scene.Key(Key.Up, KeyModifiers.Control);
        Assert.Equal(10, scene.Node.Value);
        scene.Node.Increment = 0;
        scene.Key(Key.Down);
        Assert.Equal(10, scene.Node.Value);
        Assert.Equal("150", scene.Editor.Text);
    }

    [Fact]
    public void UndoAndRedoTextUseTheSameLiveValueRules()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Edit("20");
        scene.Key(Key.Z, KeyModifiers.Control);
        Assert.Equal(10, scene.Node.Value);
        scene.Key(Key.Y, KeyModifiers.Control);
        Assert.Equal(20, scene.Node.Value);
    }

    [Fact]
    public void DescendantStylesApplyToDifferentClosedNumericTypes()
    {
        var first = new NumericUpDown<int>();
        var second = new NumericUpDown<decimal>();
        var root = new StackPanel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("NumericUpDown .numeric-up-down-editor { font-size: 23px; }")]);
        root.UpdateStyles();
        Assert.Equal(23, first.ReadOnlyChildren.OfType<TextBox>().Single().FontSize);
        Assert.Equal(23, second.ReadOnlyChildren.OfType<TextBox>().Single().FontSize);
    }

    [Fact]
    public void CustomReferenceNumberSupportsEditingAndTypedArithmetic()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<NumericUpDownTestNumber>(new()
        {
            Value = new(1.000000000000000000000000001m),
            Increment = new(0.000000000000000000000000001m)
        });
        scene.Node.Focus();
        scene.Key(Key.Up);
        Assert.Equal(1.000000000000000000000000002m, scene.Node.Value.Number);
        scene.Edit("2.25");
        Assert.Equal(2.25m, scene.Node.Value.Number);
    }

    [Fact]
    public void NaNFromCustomArithmeticIsRejected()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<NumericUpDownTestNumber>(new() { Value = new(10) { ProduceNaN = true } });
        scene.Node.Focus();
        Assert.Throws<InvalidOperationException>(() => scene.Key(Key.Up));
        Assert.Equal(10m, scene.Node.Value.Number);
    }

    [Fact]
    public void CustomParsingOverflowIsPropagatedRatherThanTreatedAsInvalidText()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<NumericUpDownTestNumber>(new());
        try
        {
            Assert.Throws<OverflowException>(() => scene.Edit("throw"));
        }
        finally
        {
            scene.Key(Key.Escape);
        }
        Assert.Equal(0m, scene.Node.Value.Number);
    }

    [Fact]
    public void FormattingOverflowOutsideArithmeticIsNotSaturated()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<NumericUpDownTestNumber>(new() { Value = new(10) });
        scene.Node.Focus();
        scene.Node.FormatString = "throw";
        try
        {
            Assert.Throws<OverflowException>(() => scene.Key(Key.Up));
            Assert.Equal(10m, scene.Node.Value.Number);
        }
        finally
        {
            scene.Node.FormatString = string.Empty;
        }
    }

    [Fact]
    public void NotificationOverflowOutsideArithmeticIsNotSaturated()
    {
        using var context = new UiTextTestContext();
        using var scene = new Scene<int>(new() { Value = 10 });
        scene.Node.Focus();
        var failure = new OverflowException("notification");
        scene.Node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, NumericUpDown<int>.ValueProperty))
                throw failure;
        };
        Assert.Same(failure, Assert.Throws<OverflowException>(() => scene.Key(Key.Up)));
        Assert.Equal(11, scene.Node.Value);
    }

    private sealed class Scene<T> : IDisposable where T : INumber<T>
    {
        internal Scene(NumericUpDown<T> node, bool useInvariantProvider = true)
        {
            Node = node;
            if (useInvariantProvider)
                node.FormatProvider = CultureInfo.InvariantCulture;
            node.Width = 240;
            node.Height = 40;
            Root.Children.Add(node);
            Canvas.SetTop(Other, 60);
            Root.Children.Add(Other);
            Screen = new GameScreen(Root);
            Manager.Open(Screen);
            Frame(0);
            Editor = node.ReadOnlyChildren.OfType<TextBox>().Single();
        }

        internal UiManager Manager { get; } = new();
        internal Canvas Root { get; } = new();
        internal Button Other { get; } = new() { Width = 100, Height = 30, Text = "other" };
        internal GameScreen Screen { get; }
        internal NumericUpDown<T> Node { get; }
        internal TextBox Editor { get; }

        internal void Edit(string text)
        {
            Assert.True(Node.Focus());
            Key(PanguEngine.Input.Key.A, KeyModifiers.Control);
            if (text.Length == 0)
                Key(PanguEngine.Input.Key.Backspace);
            else
                Manager.ProcessTextInput(text);
        }

        internal void Key(Key key, KeyModifiers modifiers = KeyModifiers.None) => Manager.ProcessKeyDown(key, modifiers);
        internal void Frame(int milliseconds) => Manager.UpdateFrame(new Size(400, 120), 0, TimeSpan.FromMilliseconds(milliseconds));
        internal void Press(Point point) => Manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        internal void Release(Point point) => Manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
        internal Point PartCenter(string role)
        {
            var part = Node.ReadOnlyChildren.Single(child => child.Classes.Contains($"numeric-up-down-{role}"));
            return part.LocalToScreen(new Point(part.LayoutBounds.Width / 2, part.LayoutBounds.Height / 2));
        }

        public void Dispose() => Manager.Close();
    }
}
