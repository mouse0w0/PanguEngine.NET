using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class SliderTests
{
    [Fact]
    public void NoValueStateChangesTheDefaultTrackStyleAndRestoresItWhenValuesReturn()
    {
        var items = new ObservableList<int>([60]);
        var slider = new Slider<int>(new ListSliderValueModel<int>(items));
        _ = new UiScreen(slider);
        slider.UpdateStyles();
        var track = Part(slider, "track");
        var normalBackground = track.GetValue(Region.BackgroundProperty);
        var noValue = UiPseudoClass.NoValue;
        Assert.False(slider.HasPseudoClass(noValue));

        items.Clear();
        slider.UpdateStyles();
        Assert.False(slider.HasValue);
        Assert.True(slider.HasPseudoClass(noValue));
        Assert.NotEqual(normalBackground, track.GetValue(Region.BackgroundProperty));

        items.Add(120);
        slider.UpdateStyles();
        Assert.True(slider.HasValue);
        Assert.False(slider.HasPseudoClass(noValue));
        Assert.Equal(normalBackground, track.GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void AvailabilityTransitionsPublishOnlyCommittedAvailability()
    {
        var items = new ObservableList<int>();
        var slider = new Slider<int>(new ListSliderValueModel<int>(items));
        var changes = new List<bool>();
        var values = new List<int>();
        using var valueSubscription = slider.Subscribe(Slider<int>.ValueProperty, (_, change) => values.Add(change.NewValue));
        using var availabilitySubscription = slider.Subscribe(Slider<int>.HasValueProperty, (_, change) =>
        {
            Assert.Equal(change.NewValue, slider.HasValue);
            if (change.NewValue)
                Assert.Equal(60, slider.Value);
            else
                Assert.Throws<InvalidOperationException>(() => slider.Value);
            changes.Add(change.NewValue);
        });

        items.Add(60);
        items.Clear();

        Assert.Equal(new[] { true, false }, changes);
        Assert.Empty(values);
        Assert.Throws<InvalidOperationException>(() => slider.SetValue(Slider<int>.HasValueProperty, true));
    }

    [Fact]
    public void ClrAndRegisteredWritesAndClearingUseTheSameAssignmentRules()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>
        {
            Minimum = 2, Maximum = 10, Step = 3, AssignmentPolicy = SliderValueAssignmentPolicy.Snap
        });
        var changes = new List<int>();
        using var subscription = slider.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            Assert.True(slider.HasValue);
            Assert.Equal(change.NewValue, slider.Value);
            changes.Add(change.NewValue);
        });
        slider.Value = 100;
        Assert.Equal(10, slider.Value);
        slider.SetValue(Slider<int>.ValueProperty, 6);
        slider.ClearValue(Slider<int>.ValueProperty);
        Assert.Equal(new[] { 10, 5, 2 }, changes);

        var list = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>([30, 60]))) { Value = 60 };
        Assert.Throws<ArgumentException>(() => list.ClearValue(Slider<int>.ValueProperty));
        Assert.Equal(60, list.Value);
    }

    [Fact]
    public void TwoWayBindingPreservesNumericAssignmentPolicies()
    {
        var source = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 15 };
        var target = new Slider<int>(new NumericSliderValueModel<int> { Step = 2, AssignmentPolicy = SliderValueAssignmentPolicy.Snap });
        target.BindTwoWay(Slider<int>.ValueProperty, source, Slider<int>.ValueProperty);
        Assert.Equal(16, target.Value);
        Assert.Equal(16, source.Value);
        target.Value = 21;
        Assert.Equal(22, source.Value);
        source.Value = 31;
        Assert.Equal(32, target.Value);
        Assert.Equal(32, source.Value);
    }

    [Fact]
    public void BoundListRejectsNonMembersAndRequiresExplicitResynchronizationAfterEmpty()
    {
        var source = new Slider<int>(new NumericSliderValueModel<int> { Maximum = 200 }) { Value = 60 };
        var items = new ObservableList<int>([30, 60, 120]);
        var target = new Slider<int>(new ListSliderValueModel<int>(items));
        target.Bind(Slider<int>.ValueProperty, source, Slider<int>.ValueProperty);
        Assert.Throws<ArgumentException>(() => source.Value = 90);
        Assert.Equal(60, target.Value);
        items.Clear();
        Assert.Throws<InvalidOperationException>(() => source.Value = 120);
        Assert.False(target.HasValue);
        items.AddRange([30, 60, 120]);
        Assert.Equal(30, target.Value);
        target.Value = source.Value;
        Assert.Equal(120, target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoWayBindingPreservesUserInteractionWhenTheSourceAcceptsTheValue(bool keyboard)
    {
        var source = new Slider<int>(new NumericSliderValueModel<int>());
        var target = new Slider<int>(new NumericSliderValueModel<int>());
        target.BindTwoWay(Slider<int>.ValueProperty, source, Slider<int>.ValueProperty);
        var committed = new List<SliderValueCommittedEventArgs<int>>();
        target.ValueCommitted += (_, change) => committed.Add(change);
        var (manager, _) = Open(target);
        try
        {
            if (keyboard)
            {
                Assert.True(target.Focus());
                Assert.True(manager.ProcessKeyDown(Key.Right, KeyModifiers.None));
            }
            else
            {
                manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
                var initialValue = target.Value;
                manager.ProcessPointerMoved(new Point(160, 16));
                Assert.True(target.Value > initialValue);
                manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            }

            Assert.Equal(target.Value, source.Value);
            var change = Assert.Single(committed);
            Assert.Equal(0, change.StartValue);
            Assert.Equal(target.Value, change.EndValue);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void SilentSourceNormalizationPreservesTheTargetsKeyboardChange()
    {
        var source = new Slider<int>(new NumericSliderValueModel<int> { Maximum = 10 }) { Value = 10 };
        var target = new Slider<int>(new NumericSliderValueModel<int>());
        target.BindTwoWay(Slider<int>.ValueProperty, source, Slider<int>.ValueProperty);
        var committed = new List<SliderValueCommittedEventArgs<int>>();
        target.ValueCommitted += (_, change) => committed.Add(change);
        var (manager, _) = Open(target);
        try
        {
            Assert.True(target.Focus());
            manager.ProcessKeyDown(Key.Right, KeyModifiers.None);

            Assert.Equal(10, source.Value);
            Assert.Equal(11, target.Value);
            var change = Assert.Single(committed);
            Assert.Equal(10, change.StartValue);
            Assert.Equal(11, change.EndValue);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ClearingTheRequiredModelIsRejectedWithoutReplacingIt()
    {
        var model = new NumericSliderValueModel<int>();
        var slider = new Slider<int>(model) { Value = 50 };

        Assert.Throws<ArgumentNullException>(() => slider.ClearValue(Slider<int>.ValueModelProperty));

        Assert.Same(model, slider.ValueModel);
        Assert.Equal(50, slider.Value);
        model.Maximum = 20;
        Assert.Equal(20, slider.Value);
    }

    [Fact]
    public void ReplacingModelsPreservesLegalValuesOrUsesTheNewInitialValue()
    {
        var oldModel = new NumericSliderValueModel<int>();
        var slider = new Slider<int>(oldModel) { Value = 60 };
        slider.ValueModel = new ListSliderValueModel<int>(new ObservableList<int>([30, 60, 120]));
        Assert.Equal(60, slider.Value);
        oldModel.Maximum = 5;
        Assert.Equal(60, slider.Value);
        slider.ValueModel = new NumericSliderValueModel<int> { Minimum = 10, Maximum = 20 };
        Assert.Equal(10, slider.Value);
        slider.ValueModel = new ListSliderValueModel<int>(new ObservableList<int>());
        Assert.False(slider.HasValue);
    }

    [Fact]
    public void ModelReplacementCallbacksDoNotDuplicatePreservedValueChanges()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 60 };
        var changes = new List<(int OldValue, int NewValue)>();
        using var valueSubscription = slider.Subscribe(Slider<int>.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));
        using var modelSubscription = slider.Subscribe(Slider<int>.ValueModelProperty, (_, _) => slider.Value = 20);

        slider.ValueModel = new NumericSliderValueModel<int>();

        Assert.Equal(20, slider.Value);
        Assert.Equal((60, 20), Assert.Single(changes));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    public void ModelReplacementPublishesValueChangesBeforeExternalModelCallbacks(int assignedValue)
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 60 };
        var changes = new List<(int OldValue, int NewValue)>();
        using var valueSubscription = slider.Subscribe(Slider<int>.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));
        using var modelSubscription = slider.Subscribe(Slider<int>.ValueModelProperty, (_, _) => slider.Value = assignedValue);

        slider.ValueModel = new NumericSliderValueModel<int> { Minimum = 10, Maximum = 20 };

        Assert.Equal(assignedValue, slider.Value);
        Assert.Equal((60, 10), changes[0]);
        if (assignedValue == 10)
            Assert.Single(changes);
        else
            Assert.Equal(new[] { (60, 10), (10, 15) }, changes);
    }

    [Fact]
    public void ReentrantModelReplacementDoesNotInventValueChangesAcrossAvailabilityTransitions()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 60 };
        var emptyModel = new ListSliderValueModel<int>(new ObservableList<int>());
        var availabilityChanges = new List<(bool OldValue, bool NewValue)>();
        var valueChanges = new List<int>();
        using var valueSubscription = slider.Subscribe(Slider<int>.ValueProperty,
            (_, change) => valueChanges.Add(change.NewValue));
        using var availabilitySubscription = slider.Subscribe(Slider<int>.HasValueProperty,
            (_, change) => availabilityChanges.Add((change.OldValue, change.NewValue)));
        using var modelSubscription = slider.Subscribe(Slider<int>.ValueModelProperty, (_, change) =>
        {
            if (ReferenceEquals(change.NewValue, emptyModel))
                slider.ValueModel = new NumericSliderValueModel<int>();
        });

        slider.ValueModel = emptyModel;

        Assert.True(slider.HasValue);
        Assert.Equal(0, slider.Value);
        Assert.Equal(new[] { (true, false), (false, true) }, availabilityChanges);
        Assert.Empty(valueChanges);
    }

    [Fact]
    public void ReentrantValueWritesPreserveAvailabilityNotificationsAfterModelReplacement()
    {
        var slider = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>()));
        var availabilityChanges = new List<(bool OldValue, bool NewValue)>();
        using var availabilitySubscription = slider.Subscribe(Slider<int>.HasValueProperty, (_, change) =>
        {
            Assert.True(slider.HasValue);
            Assert.Equal(0, slider.Value);
            availabilityChanges.Add((change.OldValue, change.NewValue));
        });
        using var modelSubscription = slider.Subscribe(Slider<int>.ValueModelProperty, (_, _) => slider.Value = 20);

        slider.ValueModel = new NumericSliderValueModel<int>();

        Assert.True(slider.HasValue);
        Assert.Equal(20, slider.Value);
        Assert.Equal((false, true), Assert.Single(availabilityChanges));
    }

    [Fact]
    public void TrackClickContinuesDraggingAndOutsideReleaseCommitsOnce()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>());
        var committed = new List<SliderValueCommittedEventArgs<int>>();
        slider.ValueCommitted += (_, change) => committed.Add(change);
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);
            Assert.Empty(committed);
            manager.ProcessPointerMoved(new Point(160, 16));
            Assert.True(slider.Value > 50);
            manager.ProcessPointerReleased(new Point(300, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(100, slider.Value);
            var change = Assert.Single(committed);
            Assert.Equal(0, change.StartValue);
            Assert.Equal(100, change.EndValue);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ThumbPressKeepsOffsetAndDoesNotSnapUntilMovement()
    {
        var slider = new Slider<double>(new NumericSliderValueModel<double> { Step = 20 }) { Value = 50 };
        var (manager, _) = Open(slider);
        try
        {
            var thumb = Part(slider, "thumb").LayoutBounds;
            var press = new Point(thumb.X + thumb.Width - 2, thumb.Y + thumb.Height / 2);
            manager.ProcessPointerPressed(press, MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);
            manager.ProcessPointerReleased(press, MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);

            manager.ProcessPointerPressed(press, MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerMoved(new Point(press.X + 20, press.Y));
            Assert.Equal(60, slider.Value);
            manager.ProcessPointerReleased(new Point(press.X + 20, press.Y), MouseButton.Left, KeyModifiers.None);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ReturningToTheStartValueDoesNotCommit()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 50 };
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerMoved(new Point(160, 16));
            manager.ProcessPointerReleased(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);
            Assert.Equal(0, count);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ProgrammaticAssignmentCancelsTheOldDrag()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>());
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            slider.Value = 20;
            manager.ProcessPointerMoved(new Point(180, 16));
            manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(20, slider.Value);
            Assert.Equal(0, count);
        }
        finally { manager.Close(); }
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("orientation")]
    [InlineData("geometry")]
    [InlineData("model")]
    public void UnavailabilityAndDomainOrGeometryChangesCancelDragging(string change)
    {
        var model = new NumericSliderValueModel<int>();
        var slider = new Slider<int>(model);
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            switch (change)
            {
                case "disabled": slider.IsEnabled = false; break;
                case "hidden": slider.Visibility = Visibility.Hidden; break;
                case "orientation": slider.Orientation = Orientation.Vertical; break;
                case "geometry": slider.Width = 220; manager.UpdateFrame(new Size(320, 320), 0); break;
                case "model": model.Maximum = 80; break;
            }
            var retained = slider.Value;
            manager.ProcessPointerMoved(new Point(180, 16));
            manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(retained, slider.Value);
            Assert.Equal(0, count);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ListMutationCancelsDraggingAndEmptyModelsStopInteraction()
    {
        var items = new ObservableList<int>([30, 60, 120]);
        var slider = new Slider<int>(new ListSliderValueModel<int>(items));
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            items.Reverse();
            manager.UpdateFrame(new Size(320, 320), 0);
            manager.ProcessPointerReleased(new Point(190, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(60, slider.Value);
            Assert.Equal(0, count);
            items.Clear();
            manager.UpdateFrame(new Size(320, 320), 0);
            Assert.False(manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None));
            Assert.False(manager.ProcessKeyDown(Key.End, KeyModifiers.None));
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void KeyboardAdjustmentsAndRepeatsEachCommitWhileWheelRemainsUnhandled()
    {
        var slider = new Slider<double>(new NumericSliderValueModel<double> { Minimum = 2, Maximum = 10, Step = 2, LargeChange = 1 }) { Value = 2.5 };
        var committed = new List<SliderValueCommittedEventArgs<double>>();
        slider.ValueCommitted += (_, change) => committed.Add(change);
        var (manager, _) = Open(slider);
        try
        {
            Assert.True(slider.Focus());
            manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
            Assert.Equal(4, slider.Value);
            manager.ProcessKeyDown(Key.Right, KeyModifiers.None, true);
            Assert.Equal(6, slider.Value);
            manager.ProcessKeyDown(Key.PageUp, KeyModifiers.None);
            Assert.Equal(8, slider.Value);
            Assert.Equal(3, committed.Count);
            Assert.False(manager.ProcessKeyDown(Key.Left, KeyModifiers.Shift));
            Assert.False(manager.ProcessPointerWheel(new Point(100, 16), 0, -1));
            Assert.Equal(8, slider.Value);
            manager.ProcessKeyDown(Key.Home, KeyModifiers.None);
            Assert.Equal(2, slider.Value);
            manager.ProcessKeyDown(Key.End, KeyModifiers.None);
            Assert.Equal(10, slider.Value);
            manager.ProcessKeyDown(Key.End, KeyModifiers.None);
            Assert.Equal(5, committed.Count);
        }
        finally { manager.Close(); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public void ReentrantProgrammaticWritesCancelKeyboardCompletionOnlyWhenTheValueChanges(int assignedValue)
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>());
        var committed = new List<SliderValueCommittedEventArgs<int>>();
        slider.ValueCommitted += (_, change) => committed.Add(change);
        using var subscription = slider.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            if (change.NewValue == 1)
                slider.Value = assignedValue;
        });
        var (manager, _) = Open(slider);
        try
        {
            Assert.True(slider.Focus());
            Assert.True(manager.ProcessKeyDown(Key.Right, KeyModifiers.None));
            Assert.Equal(assignedValue, slider.Value);
            if (assignedValue == 1)
            {
                var change = Assert.Single(committed);
                Assert.Equal(0, change.StartValue);
                Assert.Equal(1, change.EndValue);
            }
            else
                Assert.Empty(committed);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void VerticalPointerAndKeyboardValuesIncreaseUpwards()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Orientation = Orientation.Vertical };
        var (manager, _) = Open(slider, 32, 200);
        try
        {
            manager.ProcessPointerPressed(new Point(16, 8), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(100, slider.Value);
            manager.ProcessPointerReleased(new Point(16, 192), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(0, slider.Value);
            manager.ProcessKeyDown(Key.Up, KeyModifiers.None);
            Assert.Equal(1, slider.Value);
            manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
            Assert.Equal(0, slider.Value);
        }
        finally { manager.Close(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LosingInputStateDuringValueNotificationsSuppressesCompletion(bool pointerRelease, bool closeScreen)
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Value = 50 };
        var committed = new List<SliderValueCommittedEventArgs<int>>();
        slider.ValueCommitted += (_, change) => committed.Add(change);
        var (manager, screen) = Open(slider);
        using var subscription = slider.Subscribe(Slider<int>.ValueProperty, (_, _) =>
        {
            if (closeScreen)
                manager.Close();
            else
                screen.ClearFocus();
        });
        try
        {
            if (pointerRelease)
            {
                manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
                manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            }
            else
            {
                Assert.True(slider.Focus());
                manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
            }

            Assert.True(slider.Value > 50);
            Assert.False(slider.IsFocused);
            Assert.Empty(committed);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void MeasuredLabelsAvoidOverlapAndPreferTheSelectedTick()
    {
        using var textContext = new UiTextTestContext();
        var slider = new Slider<int>(new NumericSliderValueModel<int> { Maximum = 10, TickFrequency = 1 })
        {
            Value = 5, ShowTickLabels = true, ValueFormatter = value => $"choice {value}"
        };
        var (manager, screen) = Open(slider, 240, 80);
        try
        {
            var labels = slider.Children.OfType<Text>().Where(label => label.Visibility == Visibility.Visible).ToArray();
            Assert.Contains(labels, label => label.Content == "choice 5");
            var ordered = labels.OrderBy(label => label.LayoutBounds.X).ToArray();
            for (var index = 1; index < ordered.Length; index++)
                Assert.True(ordered[index - 1].LayoutBounds.X + ordered[index - 1].LayoutBounds.Width <= ordered[index].LayoutBounds.X);
            Assert.DoesNotContain(slider.Children, node => node.Classes.Contains("slider-tick") && node.Visibility == Visibility.Visible);
            foreach (var label in labels)
                Assert.NotSame(label, screen.HitTest(slider.LocalToScreen(new Point(label.LayoutBounds.X, label.LayoutBounds.Y))));
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void EqualStringCandidatesKeepTheirActualPositionWithoutDuplicateNotifications()
    {
        var first = new string('x', 3);
        var second = new string('x', 3);
        var items = new ObservableList<string>([first, second]);
        var slider = new Slider<string>(new ListSliderValueModel<string>(items));
        var values = 0;
        var commits = 0;
        using var subscription = slider.Subscribe(Slider<string>.ValueProperty, (_, _) => values++);
        slider.ValueCommitted += (_, _) => commits++;
        var (manager, _) = Open(slider);
        try
        {
            Assert.True(slider.Focus());
            manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
            Assert.Same(second, slider.Value);
            slider.Value = first;
            Assert.Same(second, slider.Value);
            Assert.Equal(0, values);
            Assert.Equal(0, commits);
            items[1] = "changed";
            Assert.Equal("changed", slider.Value);
            Assert.Equal(1, values);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void ModelReplacementUsesTheCandidateListsMatchingRule()
    {
        var slider = new Slider<string>(new ListSliderValueModel<string>(new ObservableList<string>(["HIGH"])));
        slider.ValueModel = new ListSliderValueModel<string>(new CaseInsensitiveList(["low", "high"]));
        Assert.Equal("high", slider.Value);
    }

    [Fact]
    public void AutomaticSizeWithManyTicksStabilizesMeasurementAndArrangement()
    {
        var slider = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>(Enumerable.Range(0, 600))))
        {
            ShowTicks = true, HorizontalAlignment = HorizontalAlignment.Left
        };
        var screen = new GameScreen(slider);
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        try
        {
            manager.UpdateFrame(new Size(320, 80), 0);
            Assert.True(slider.IsMeasureValid);
            Assert.True(slider.IsArrangeValid);
            manager.UpdateFrame(new Size(100, 80), 0);
            Assert.True(slider.IsArrangeValid);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void TickLabelsCanBeEnabledAndHiddenSelectedLabelsCanBecomeVisible()
    {
        using var textContext = new UiTextTestContext();
        var slider = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>(Enumerable.Range(0, 600))))
        {
            ShowTicks = true, ValueFormatter = value => $"choice {value}"
        };
        var (manager, _) = Open(slider, 240, 80);
        try
        {
            slider.ShowTickLabels = true;
            manager.UpdateFrame(new Size(320, 320), 0);
            slider.Value = 1;
            manager.UpdateFrame(new Size(320, 320), 0);
            Assert.Contains(slider.Children.OfType<Text>(), label => label.Content == "choice 1" && label.Visibility == Visibility.Visible);
            slider.ShowTickLabels = false;
            manager.UpdateFrame(new Size(320, 320), 0);
            slider.ShowTickLabels = true;
            manager.UpdateFrame(new Size(320, 320), 0);
            Assert.True(slider.IsMeasureValid);
            Assert.True(slider.IsArrangeValid);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void MovingTheSliderOnScreenCancelsDraggingBeforeRelease()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>()) { Width = 200, Height = 32, Value = 50 };
        var canvas = new Canvas();
        canvas.Children.Add(slider);
        var screen = new GameScreen(canvas);
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        try
        {
            manager.UpdateFrame(new Size(320, 80), 0);
            var point = slider.LocalToScreen(new Point(100, 16));
            manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
            Canvas.SetLeft(slider, 50);
            manager.UpdateFrame(new Size(320, 80), 0);
            manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);
            Assert.Equal(0, count);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void FailedAssignmentsAndFutureAssignmentPoliciesKeepTheActiveDrag()
    {
        var model = new NumericSliderValueModel<int> { Step = 20, AssignmentPolicy = SliderValueAssignmentPolicy.Reject };
        var slider = new Slider<int>(model);
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Throws<ArgumentException>(() => slider.Value = 15);
            model.AssignmentPolicy = SliderValueAssignmentPolicy.Clamp;
            manager.UpdateFrame(new Size(320, 320), 0);
            manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(100, slider.Value);
            Assert.Equal(1, count);
        }
        finally { manager.Close(); }
    }

    [Fact]
    public void WindowFocusLossCancelsDraggingWithoutRevertingTheValue()
    {
        var slider = new Slider<int>(new NumericSliderValueModel<int>());
        var count = 0;
        slider.ValueCommitted += (_, _) => count++;
        var (manager, _) = Open(slider);
        try
        {
            manager.ProcessPointerPressed(new Point(100, 16), MouseButton.Left, KeyModifiers.None);
            manager.ProcessFocusChanged(false);
            manager.ProcessPointerReleased(new Point(180, 16), MouseButton.Left, KeyModifiers.None);
            Assert.Equal(50, slider.Value);
            Assert.Equal(0, count);
        }
        finally { manager.Close(); }
    }

    private static (UiManager Manager, GameScreen Screen) Open<T>(Slider<T> slider, double width = 200, double height = 32)
    {
        slider.Width = width;
        slider.Height = height;
        slider.HorizontalAlignment = HorizontalAlignment.Left;
        slider.VerticalAlignment = VerticalAlignment.Top;
        var screen = new GameScreen(slider);
        screen.SetBaseStyleSheets([]);
        var manager = new UiManager();
        manager.Open(screen);
        manager.UpdateFrame(new Size(320, 320), 0);
        return (manager, screen);
    }

    private static UiNode Part<T>(Slider<T> slider, string role) => slider.Children.Single(node => node.Classes.Contains($"slider-{role}"));

    private sealed class CaseInsensitiveList(IEnumerable<string> items) : ObservableList<string>(items)
    {
        protected override int IndexOfItem(string? item)
        {
            for (var index = 0; index < Count; index++)
                if (string.Equals(this[index], item, StringComparison.OrdinalIgnoreCase))
                    return index;
            return -1;
        }
    }
}
