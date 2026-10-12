using System.Runtime.CompilerServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class SliderValueModelTests
{
    [Fact]
    public void NumericAssignmentsCanPreserveSnapOrRejectOffStepValues()
    {
        var model = new NumericSliderValueModel<int> { Minimum = 2, Maximum = 32, Step = 2 };
        var slider = new Slider<int>(model) { Value = 15 };
        Assert.Equal(15, slider.Value);

        model.AssignmentPolicy = SliderValueAssignmentPolicy.Snap;
        slider.Value = 15;
        Assert.Equal(16, slider.Value);

        model.AssignmentPolicy = SliderValueAssignmentPolicy.Reject;
        Assert.Throws<ArgumentException>(() => slider.Value = 15);
        Assert.Equal(16, slider.Value);
    }

    [Fact]
    public void DirectionalStepsFromAnOffStepValueChooseTheNextGridPoint()
    {
        var model = new NumericSliderValueModel<double> { Minimum = 2, Maximum = 32, Step = 2 };
        var state = model.ResolveValue(2.5);
        Assert.Equal(4, model.Move(state, 1, false).Value);
        Assert.Equal(2, model.Move(state, -1, false).Value);
    }

    [Fact]
    public void MaximumIsSelectableWhenItIsNotOnTheStepGrid()
    {
        var model = new NumericSliderValueModel<int> { Maximum = 10, Step = 3 };
        Assert.Equal(10, model.ResolvePosition(1).Value);
        Assert.Equal(10, model.Move(model.ResolveValue(9), 1, false).Value);
        Assert.Equal(9, model.Move(model.ResolveValue(10), -1, false).Value);
    }

    [Fact]
    public void LargeLongValuesKeepTheirExactValueAndLocalStep()
    {
        const long minimum = 9_007_199_254_740_992;
        var model = new NumericSliderValueModel<long> { Minimum = minimum, Maximum = minimum + 10, Step = 1 };
        var state = model.ResolveValue(minimum + 1);
        Assert.Equal(minimum + 1, state.Value);
        Assert.Equal(0.1, model.GetPosition(state), 12);
        Assert.Equal(minimum + 2, model.Move(state, 1, false).Value);
        Assert.Equal(minimum + 5, model.ResolvePosition(.5).Value);
    }

    [Fact]
    public void EntireSignedIntegerRangeDoesNotOverflow()
    {
        var model = new NumericSliderValueModel<long> { Minimum = long.MinValue, Maximum = long.MaxValue };
        Assert.Equal(long.MinValue, model.ResolvePosition(0).Value);
        Assert.Equal(long.MaxValue, model.ResolvePosition(1).Value);
        Assert.Equal(1, model.GetPosition(model.ResolveValue(long.MaxValue)));
        Assert.Equal(0, model.ResolvePosition(.5).Value);
    }

    [Fact]
    public void EntireUnsignedIntegerRangeKeepsExactEndpointsAndMidpoint()
    {
        var model = new NumericSliderValueModel<ulong> { Maximum = ulong.MaxValue };
        Assert.Equal(0UL, model.ResolvePosition(0).Value);
        Assert.Equal(ulong.MaxValue, model.ResolvePosition(1).Value);
        Assert.Equal(9_223_372_036_854_775_808UL, model.ResolvePosition(.5).Value);
    }

    [Fact]
    public void ReversedNumericRangeActsAsASingleValueAtTheCenter()
    {
        var model = new NumericSliderValueModel<int> { Minimum = 10, Maximum = 5, Step = 3 };
        var state = model.ResolveValue(100);
        Assert.Equal(10, state.Value);
        Assert.Equal(.5, model.GetPosition(state));
        Assert.Equal(10, model.Move(state, 1, false).Value);
        Assert.Equal(10, model.ResolvePosition(0).Value);
        Assert.Equal(10, model.ResolvePosition(1).Value);
    }

    [Fact]
    public void DecimalStepsAndProgramValuesDoNotLosePrecision()
    {
        var model = new NumericSliderValueModel<decimal> { Minimum = .1m, Maximum = .9m, Step = .1m };
        Assert.Equal(.3m, model.ResolvePosition(.25).Value);
        Assert.Equal(.4m, model.Move(model.ResolveValue(.3m), 1, false).Value);
        Assert.Equal(.1234567890123456789012345678m, model.ResolveValue(.1234567890123456789012345678m).Value);
    }

    [Fact]
    public void ExtremeFloatingPointRangeHasFinitePositions()
    {
        var model = new NumericSliderValueModel<double> { Minimum = -double.MaxValue, Maximum = double.MaxValue };
        Assert.Equal(0, model.ResolvePosition(.5).Value);
        Assert.Equal(.5, model.GetPosition(model.ResolveValue(0)), 12);
        Assert.Equal(double.MaxValue, model.ResolvePosition(1).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.ResolveValue(double.NaN));
        Assert.Throws<ArgumentException>(() => model.Step = double.PositiveInfinity);
    }

    [Fact]
    public void SmallFloatingPointValuesRemainRepresentable()
    {
        var model = new NumericSliderValueModel<double> { Minimum = 0, Maximum = 1e-300 };
        Assert.Equal(5e-301, model.ResolvePosition(.5).Value);
        Assert.Equal(.5, model.GetPosition(model.ResolveValue(5e-301)), 12);
    }

    [Fact]
    public void TickDisplayDoesNotConstrainContinuousValues()
    {
        var model = new NumericSliderValueModel<double> { Minimum = 0, Maximum = 1, TickFrequency = .25 };
        Assert.Equal(.65, model.ResolvePosition(.65).Value, 12);
        Assert.Equal(new[] { 0d, .25, .5, .75, 1d }, model.GetTicks(100).Select(tick => tick.Value));
    }

    [Fact]
    public void DenseTicksHaveABoundedVisibleRepresentation()
    {
        var model = new NumericSliderValueModel<double> { Minimum = 1e100, Maximum = 1e101, TickFrequency = 1e-100 };
        var ticks = model.GetTicks(32);
        Assert.InRange(ticks.Count, 2, 34);
        Assert.Equal(model.Minimum, ticks[0].Value);
        Assert.Equal(model.Maximum, ticks[^1].Value);
    }

    [Fact]
    public void RemovingSelectionChoosesSuccessorAndAnEmptyListHasNoValue()
    {
        var items = new ObservableList<int>([30, 60, 120, 144]);
        var slider = new Slider<int>(new ListSliderValueModel<int>(items)) { Value = 60 };
        items.Remove(60);
        Assert.Equal(120, slider.Value);
        items.Remove(144);
        items.Remove(120);
        Assert.Equal(30, slider.Value);
        items.Clear();
        Assert.False(slider.HasValue);
        Assert.False(slider.TryGetValue(out _));
        Assert.Throws<InvalidOperationException>(() => slider.Value);
        Assert.Throws<InvalidOperationException>(() => slider.Value = 90);
        items.Add(90);
        Assert.True(slider.HasValue);
        Assert.Equal(90, slider.Value);
    }

    [Fact]
    public void ListAssignmentsRejectNonMembersAndRetainTheSelection()
    {
        var slider = new Slider<int>(new ListSliderValueModel<int>(new ObservableList<int>([30, 60, 120]))) { Value = 60 };
        Assert.Throws<ArgumentException>(() => slider.Value = 90);
        Assert.Equal(60, slider.Value);
    }

    [Fact]
    public void ObjectAssignmentsStoreTheActualMatchingMember()
    {
        var preset = new Preset("high", 32);
        var slider = new Slider<Preset>(new ListSliderValueModel<Preset>(new ObservableList<Preset>([preset])));
        slider.Value = new Preset("high", 32);
        Assert.Same(preset, slider.Value);
    }

    [Fact]
    public void ReentrantEqualityComparisonPreservesTheOuterValueAndCandidatePosition()
    {
        Slider<ReentrantValue> slider = null!;
        var candidateResolved = false;
        var reentered = false;
        void OnCompare(int left, int right)
        {
            if (left == 2 && right == 2)
                candidateResolved = true;
            if (candidateResolved && left == 0 && right == 2)
            {
                candidateResolved = false;
                reentered = true;
                slider.Value = new ReentrantValue(1);
            }
        }
        var items = new ObservableList<ReentrantValue>([
            new(0, OnCompare), new(1, OnCompare), new(2, OnCompare)]);
        slider = new Slider<ReentrantValue>(new ListSliderValueModel<ReentrantValue>(items));
        var values = new List<int>();
        using var subscription = slider.Subscribe(Slider<ReentrantValue>.ValueProperty,
            (_, change) => values.Add(change.NewValue.Number));

        slider.Value = new ReentrantValue(2);

        Assert.True(reentered);
        Assert.Equal(2, slider.Value.Number);
        Assert.True(slider.TryGetValue(out var value));
        Assert.Equal(2, value.Number);
        Assert.Equal(new[] { 1, 2 }, values);
        items[2] = new ReentrantValue(4);
        Assert.Equal(4, slider.Value.Number);
    }

    [Fact]
    public void SharedModelsKeepIndependentSelectionsThroughReorderAndReplacement()
    {
        var items = new ObservableList<int>([30, 60, 120]);
        var model = new ListSliderValueModel<int>(items);
        var first = new Slider<int>(model) { Value = 30 };
        var second = new Slider<int>(model) { Value = 120 };
        items.Reverse();
        Assert.Equal(30, first.Value);
        Assert.Equal(120, second.Value);
        items[0] = 144;
        Assert.Equal(144, second.Value);
        Assert.Equal(30, first.Value);
    }

    [Fact]
    public void SharedModelAndValueCallbacksObserveAllUpdatedSelections()
    {
        var model = new NumericSliderValueModel<int>();
        var first = new Slider<int>(model) { Value = 50 };
        var second = new Slider<int>(model) { Value = 80 };
        var valueChanges = 0;
        var modelChanges = 0;
        using var valueSubscription = first.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            Assert.Equal(20, change.NewValue);
            Assert.Equal(20, first.Value);
            Assert.Equal(20, second.Value);
            valueChanges++;
        });
        using var modelSubscription = model.Subscribe(NumericSliderValueModel<int>.MaximumProperty, (_, change) =>
        {
            Assert.Equal(20, change.NewValue);
            Assert.Equal(20, first.Value);
            Assert.Equal(20, second.Value);
            modelChanges++;
        });

        model.Maximum = 20;

        Assert.Equal(20, first.Value);
        Assert.Equal(20, second.Value);
        Assert.Equal(1, valueChanges);
        Assert.Equal(1, modelChanges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantSourceReplacementDiscardsPendingChangesFromThePreviousSource(bool restoreOriginalSource)
    {
        var original = new ObservableList<int>([10, 20, 30]);
        var replacement = new ObservableList<int>([100, 30, 20]);
        var model = new ListSliderValueModel<int>(original);
        var first = new Slider<int>(model) { Value = 10 };
        var second = new Slider<int>(model) { Value = 30 };
        using var subscription = first.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            if (change.NewValue != 20)
                return;
            model.Items = replacement;
            if (restoreOriginalSource)
                model.Items = original;
        });

        original.RemoveAt(0);

        Assert.Equal(20, first.Value);
        Assert.Equal(30, second.Value);
        var current = restoreOriginalSource ? original : replacement;
        current[current.IndexOf(30)] = 40;
        Assert.Equal(20, first.Value);
        Assert.Equal(40, second.Value);
    }

    [Fact]
    public void DisplayChangesDuringListNotificationsPreserveOtherSelections()
    {
        var items = new ObservableList<int>([10, 20, 30]);
        var model = new ListSliderValueModel<int>(items);
        var first = new Slider<int>(model) { Value = 10 };
        var second = new Slider<int>(model) { Value = 30 };
        using var subscription = first.Subscribe(Slider<int>.ValueProperty, (_, _) => model.PageStep++);

        items.RemoveAt(0);
        items[1] = 40;

        Assert.Equal(20, first.Value);
        Assert.Equal(40, second.Value);
    }

    [Fact]
    public void NewSourceMutationsDuringReplacementPreserveOtherSelections()
    {
        var original = new ObservableList<int>([10, 20, 30]);
        var replacement = new ObservableList<int>([100, 30, 20]);
        var model = new ListSliderValueModel<int>(original);
        var first = new Slider<int>(model) { Value = 10 };
        var second = new Slider<int>(model) { Value = 30 };
        using var subscription = first.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            if (change.NewValue == 100)
                replacement.Insert(0, 200);
        });

        model.Items = replacement;

        Assert.Equal(100, first.Value);
        Assert.Equal(30, second.Value);
        replacement[replacement.IndexOf(30)] = 40;
        Assert.Equal(100, first.Value);
        Assert.Equal(40, second.Value);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    public void AssignmentsDuringSharedListNotificationsDoNotApplyTheSameChangeTwice(int assignedValue)
    {
        var items = new ObservableList<int>([10, 20, 30, 40]);
        var model = new ListSliderValueModel<int>(items);
        var first = new Slider<int>(model) { Value = 10 };
        var second = new Slider<int>(model) { Value = 40 };
        using var subscription = first.Subscribe(Slider<int>.ValueProperty, (_, change) =>
        {
            if (change.NewValue == 20)
                second.Value = assignedValue;
        });

        items.RemoveAt(0);

        Assert.Equal(20, first.Value);
        Assert.Equal(assignedValue, second.Value);
        items[items.IndexOf(assignedValue)] = 50;
        Assert.Equal(50, second.Value);
    }

    [Fact]
    public void DisplayChangesDuringAvailabilityNotificationsDoNotInitializeOtherSelections()
    {
        var items = new ObservableList<int>();
        var model = new ListSliderValueModel<int>(items);
        var first = new Slider<int>(model);
        var second = new Slider<int>(model);
        var values = new List<int>();
        using var valueSubscription = second.Subscribe(Slider<int>.ValueProperty,
            (_, change) => values.Add(change.NewValue));
        using var availabilitySubscription = first.Subscribe(Slider<int>.HasValueProperty, (_, change) =>
        {
            if (change.NewValue)
                model.PageStep++;
        });

        items.AddRange([10, 20]);

        Assert.True(first.HasValue);
        Assert.True(second.HasValue);
        Assert.Equal(10, first.Value);
        Assert.Equal(10, second.Value);
        Assert.Empty(values);
        items[0] = 30;
        Assert.Equal(30, first.Value);
        Assert.Equal(30, second.Value);
        Assert.Equal(30, Assert.Single(values));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearingListSourcesIsRejectedBeforeChangingTheModel(bool ticks)
    {
        var items = new ObservableList<int>([30, 60]);
        SliderValueModel<int> model = ticks
            ? new NumericSliderValueModel<int> { TickValues = items }
            : new ListSliderValueModel<int>(items);
        Property<IObservableList<int>> property = ticks
            ? NumericSliderValueModel<int>.TickValuesProperty
            : ListSliderValueModel<int>.ItemsProperty;
        var changes = 0;
        var slider = new Slider<int>(model);
        slider.Measure(new Size(200, 32));
        Assert.True(slider.IsMeasureValid);
        using var subscription = model.Subscribe(property, (_, _) => changes++);

        Assert.Throws<ArgumentNullException>(() => model.ClearValue(property));

        Assert.Same(items, model.GetValue(property));
        Assert.Equal(0, changes);
        Assert.True(slider.IsMeasureValid);
        items.Add(120);
        Assert.False(slider.IsMeasureValid);
    }

    [Fact]
    public void ValueEqualItemSourcesSwitchSubscriptionsBeforePropertyNotifications()
    {
        var oldItems = new ValueEqualList<int>([30, 60]);
        var newItems = new ValueEqualList<int>([30, 60]);
        Assert.True(oldItems.Equals(newItems));
        var model = new ListSliderValueModel<int>(oldItems);
        var slider = new Slider<int>(model) { Value = 60 };
        var changes = 0;
        using var subscription = model.Subscribe(ListSliderValueModel<int>.ItemsProperty, (_, change) =>
        {
            Assert.Same(oldItems, change.OldValue);
            Assert.Same(newItems, change.NewValue);
            Assert.Same(newItems, model.Items);
            changes++;
            oldItems.Clear();
            Assert.True(slider.HasValue);
            Assert.Equal(60, slider.Value);
            newItems[1] = 120;
            Assert.Equal(120, slider.Value);
        });

        model.Items = newItems;
        model.Items = newItems;

        Assert.Equal(1, changes);
        Assert.Same(newItems, model.Items);
        newItems.Clear();
        Assert.False(slider.HasValue);
    }

    [Fact]
    public void ValueEqualTickSourcesSwitchSubscriptionsBeforePropertyNotifications()
    {
        var oldTicks = new ValueEqualList<int>([0, 50, 100]);
        var newTicks = new ValueEqualList<int>([0, 50, 100]);
        Assert.True(oldTicks.Equals(newTicks));
        var model = new NumericSliderValueModel<int> { TickValues = oldTicks };
        var slider = new Slider<int>(model) { ShowTicks = true };
        var changes = 0;
        using var subscription = model.Subscribe(NumericSliderValueModel<int>.TickValuesProperty, (_, change) =>
        {
            Assert.Same(oldTicks, change.OldValue);
            Assert.Same(newTicks, change.NewValue);
            Assert.Same(newTicks, model.TickValues);
            changes++;
            slider.Measure(new Size(200, 32));
            slider.Measure(new Size(200, 32));
            Assert.True(slider.IsMeasureValid);
            oldTicks.Add(75);
            Assert.True(slider.IsMeasureValid);
            newTicks.Add(25);
            Assert.False(slider.IsMeasureValid);
        });

        model.TickValues = newTicks;
        model.TickValues = newTicks;

        Assert.Equal(1, changes);
        Assert.Equal(new[] { 0, 25, 50, 100 }, model.GetTicks(100).Select(tick => tick.Value));
        slider.Measure(new Size(200, 32));
        slider.Measure(new Size(200, 32));
        Assert.True(slider.IsMeasureValid);
        newTicks.Add(75);
        Assert.False(slider.IsMeasureValid);
    }

    [Fact]
    public void SingleListItemUsesTheMiddleOfTheTrack()
    {
        var model = new ListSliderValueModel<string>(new ObservableList<string>(["高"]));
        Assert.Equal(.5, model.GetPosition(model.CreateInitialState()));
        Assert.Equal("高", model.ResolvePosition(0).Value);
        Assert.Equal("高", model.ResolvePosition(1).Value);
    }

    [Fact]
    public void DuplicateCandidatesRetainTheSelectedPositionDuringMutation()
    {
        var items = new ObservableList<string>(["same", "other", "same"]);
        var model = new ListSliderValueModel<string>(items);
        var state = model.ResolvePosition(1);
        items.Changed += (_, change) => state = model.RefreshState(state, new SliderValueModelChange<string>(true, change));
        items.Insert(0, "first");
        Assert.Equal(3, state.Index);
        items.Reverse();
        Assert.Equal(0, state.Index);
        items[0] = "replaced";
        Assert.Equal("replaced", state.Value);
        Assert.Equal(0, model.ResolveValue("replaced").Index);
    }

    [Fact]
    public void WholeListReplacementPreservesAMatchThenClampsTheOldIndex()
    {
        var items = new ObservableList<int>([30, 60, 120, 144]);
        var slider = new Slider<int>(new ListSliderValueModel<int>(items)) { Value = 120 };
        items.ReplaceAll([120, 60, 30]);
        Assert.Equal(120, slider.Value);
        slider.Value = 30;
        items.ReplaceAll([90, 100]);
        Assert.Equal(100, slider.Value);
    }

    [Fact]
    public void ManuallyCreatedStatesKeepActualDuplicateCandidatesAcrossListChanges()
    {
        var first = new string('x', 3);
        var second = new string('x', 3);
        var items = new ObservableList<string>([first, second]);
        var model = new ListSliderValueModel<string>(items);
        items.Add("other");
        var state = new SliderValueState<string>(second, 1);
        items.Changed += (_, change) => state = model.RefreshState(state,
            new SliderValueModelChange<string>(true, change));

        items.Insert(0, "first");

        Assert.Same(second, state.Value);
        Assert.Equal(2, state.Index);
    }

    [Fact]
    public void AssignmentPolicyChangesOnlyAffectFutureWritesAndDomainChangesReconcileValues()
    {
        var model = new NumericSliderValueModel<int> { Step = 2 };
        var slider = new Slider<int>(model) { Value = 15 };
        model.AssignmentPolicy = SliderValueAssignmentPolicy.Reject;
        Assert.Equal(15, slider.Value);
        model.Step = 4;
        Assert.Equal(16, slider.Value);
        model.Maximum = 14;
        Assert.Equal(14, slider.Value);
    }

    [Fact]
    public void ExplicitTicksOverrideFrequencyAndFilterDuplicatesAndOutsideValues()
    {
        var model = new NumericSliderValueModel<int> { Maximum = 10, TickFrequency = 2 };
        model.TickValues.AddRange([-1, 0, 3, 3, 10, 11]);
        Assert.Equal(new[] { 0, 3, 10 }, model.GetTicks(100).Select(tick => tick.Value));
        Assert.Equal(7, model.ResolveValue(7).Value);
    }

    [Fact]
    public void FloatingPointStepsSkipGridPointsThatRoundBackToTheCurrentValue()
    {
        var model = new NumericSliderValueModel<float> { Maximum = 2, Step = .33333334f, LargeChange = .33333334f };
        var state = model.ResolveValue(1);
        Assert.Equal(1.3333334f, model.Move(state, 1, false).Value);
        Assert.Equal(1.3333334f, model.Move(state, 1, true).Value);

        model.Step = 1e-20f;
        model.LargeChange = 1e-20f;
        var increased = model.Move(state, 1, false).Value;
        var decreased = model.Move(state, -1, false).Value;
        Assert.Equal(1.0000001f, increased);
        Assert.Equal(.99999994f, decreased);
        Assert.Equal(increased, model.Move(state, 1, true).Value);
        Assert.Equal(decreased, model.Move(state, -1, true).Value);
    }

    [Fact]
    public void SelectedDisplayTicksSurviveBudgetingWithoutAddingContinuousValueTicks()
    {
        var list = new ListSliderValueModel<int>(new ObservableList<int>(Enumerable.Range(0, 600)));
        var state = list.ResolveValue(1);
        Assert.Contains(list.GetTicks(20, state), tick => tick.Index == 1);

        var numeric = new NumericSliderValueModel<double> { Maximum = 1, TickFrequency = .25 };
        Assert.Contains(numeric.GetTicks(2, numeric.ResolveValue(.25)), tick => tick.Value == .25);
        Assert.DoesNotContain(numeric.GetTicks(2, numeric.ResolveValue(.65)), tick => tick.Value == .65);
    }

    [Fact]
    public void NullCandidatesAreActualValuesRatherThanEmptySelections()
    {
        var slider = new Slider<string?>(new ListSliderValueModel<string?>(new ObservableList<string?>([null, "high"])));
        Assert.True(slider.HasValue);
        Assert.True(slider.TryGetValue(out var value));
        Assert.Null(value);
        slider.Value = "high";
        slider.Value = null;
        Assert.Null(slider.Value);
    }

    [Fact]
    public void LongLivedModelDoesNotKeepTheSliderAlive()
    {
        var model = new NumericSliderValueModel<int>();
        var reference = CreateSliderReference(model);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.TryGetTarget(out _));
        model.Maximum = 200;
        GC.KeepAlive(model);
    }

    [Fact]
    public void LongLivedListDoesNotKeepTheModelAlive()
    {
        var items = new ObservableList<int>([30, 60]);
        var reference = CreateModelReference(items);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.TryGetTarget(out _));
        items.Add(120);
        GC.KeepAlive(items);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Slider<int>> CreateSliderReference(SliderValueModel<int> model) => new(new Slider<int>(model));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ListSliderValueModel<int>> CreateModelReference(ObservableList<int> items) => new(new ListSliderValueModel<int>(items));

    private sealed class ValueEqualList<T>(IEnumerable<T> items) : ObservableList<T>(items)
    {
        public override bool Equals(object? obj) => obj is ValueEqualList<T> other && this.SequenceEqual(other);
        public override int GetHashCode() => 0;
    }

    private readonly struct ReentrantValue(int number, Action<int, int>? onCompare = null) : IEquatable<ReentrantValue>
    {
        internal int Number { get; } = number;

        public bool Equals(ReentrantValue other)
        {
            onCompare?.Invoke(Number, other.Number);
            return Number == other.Number;
        }

        public override bool Equals(object? obj) => obj is ReentrantValue other && Equals(other);
        public override int GetHashCode() => Number.GetHashCode();
    }

    private sealed record Preset(string Name, int Distance);
}
