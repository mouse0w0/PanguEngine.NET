using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class RadioGroupTests
{
    [Fact]
    public void NonGenericGroupsShareTypedSelectionNotificationsBindingAndClear()
    {
        var numberGroup = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = numberGroup, Value = 1, IsChecked = true };
        var second = new RadioButton<int?> { Group = numberGroup, Value = 1 };
        var textGroup = new RadioGroup<string>();
        var textButton = new RadioButton<string> { Group = textGroup, Value = "option", IsChecked = true };
        RadioGroup group = numberGroup;
        var projection = new SelectedButtonSource();
        projection.Bind(SelectedButtonSource.ButtonProperty, group, RadioGroup.SelectedButtonProperty);
        Assert.Same(first, projection.Button);
        var changes = 0;
        using var subscription = group.Subscribe(RadioGroup.SelectedButtonProperty, (_, change) =>
        {
            RadioButton<int?>? typedSelection = numberGroup.SelectedButton;
            Assert.Same(typedSelection, group.SelectedButton);
            Assert.Same(typedSelection, change.NewValue);
            changes++;
        });

        second.IsChecked = true;

        Assert.Same(second, group.GetValue(RadioGroup.SelectedButtonProperty));
        Assert.Same(second, projection.Button);
        Assert.Equal(1, numberGroup.SelectedValue);
        Assert.Equal(1, changes);
        RadioGroup[] groups = [numberGroup, textGroup];
        foreach (var current in groups)
        {
            current.Clear();
            Assert.Null(current.SelectedButton);
        }
        Assert.Null(numberGroup.SelectedValue);
        Assert.Null(textGroup.SelectedValue);
        Assert.Null(projection.Button);
        Assert.False(second.IsChecked);
        Assert.False(textButton.IsChecked);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void IsCheckedAssignmentsSelectTheExactMemberAndOnlyClearItsOwnSelection()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1, IsChecked = true };
        var second = new RadioButton<int?> { Group = group, Value = 1 };

        second.SetValue(RadioButton<int?>.IsCheckedProperty, true);

        Assert.Same(second, group.SelectedButton);
        Assert.Equal(1, group.SelectedValue);
        Assert.False(first.IsChecked);
        Assert.True(second.IsChecked);
        first.IsChecked = false;
        Assert.Same(second, group.SelectedButton);
        second.IsChecked = false;
        Assert.Null(group.SelectedButton);
        Assert.Null(group.SelectedValue);
        Assert.False(second.IsChecked);

        second.IsChecked = true;
        second.ClearValue(RadioButton<int?>.IsCheckedProperty);
        Assert.Null(group.SelectedButton);
        Assert.False(second.IsChecked);
    }

    [Fact]
    public void IsCheckedRequestsWithoutAGroupOrValueDoNotSelectOrRemainPending()
    {
        var ungrouped = new RadioButton<int?> { Value = 1, IsChecked = true };
        Assert.False(ungrouped.IsChecked);
        var group = new RadioGroup<int?>();
        ungrouped.Group = group;
        Assert.Null(group.SelectedButton);
        var unconfigured = new RadioButton<int?> { Group = group, IsChecked = true };
        Assert.False(unconfigured.IsChecked);
        unconfigured.Value = 2;
        Assert.Null(group.SelectedButton);
        ungrouped.IsChecked = true;
        unconfigured.Value = null;
        unconfigured.IsChecked = true;
        unconfigured.IsChecked = false;
        Assert.Same(ungrouped, group.SelectedButton);
        Assert.False(unconfigured.IsChecked);
    }

    [Fact]
    public void IsCheckedTwoWayBindingReflectsSelectionChangesAndPreservesTheBinding()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        var source = new CheckedSource();
        RadioButton target = first;
        target.BindTwoWay(RadioButton.IsCheckedProperty, source, CheckedSource.IsSelectedProperty);

        source.IsSelected = true;
        Assert.Same(first, group.SelectedButton);
        second.IsChecked = true;
        Assert.False(source.IsSelected);
        Assert.Same(second, group.SelectedButton);
        source.IsSelected = true;
        Assert.Same(first, group.SelectedButton);
        Assert.False(second.IsChecked);
        first.IsChecked = false;
        Assert.False(source.IsSelected);
        Assert.Null(group.SelectedButton);
        Assert.True(first.IsBound(RadioButton<int?>.IsCheckedProperty));
        source.IsSelected = true;
        Assert.Same(first, group.SelectedButton);
    }

    [Fact]
    public void ZeroIsAValidValueAndClearRemovesTheSelection()
    {
        var group = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = group, Value = 0 };
        Assert.Null(group.SelectedValue);
        Assert.False(button.IsChecked);

        group.SelectedValue = 0;

        Assert.Same(button, group.SelectedButton);
        Assert.Equal(0, group.SelectedValue);
        Assert.True(button.IsChecked);
        group.Clear();
        Assert.Null(group.SelectedValue);
        Assert.Null(group.SelectedButton);
        Assert.False(button.IsChecked);
    }

    [Fact]
    public void UnmatchedValueClearsAndLaterMembersDoNotRestoreSelection()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 0 };
        group.Select(first);

        group.SelectedValue = 7;

        Assert.Null(group.SelectedValue);
        Assert.Null(group.SelectedButton);
        Assert.False(first.IsChecked);
        _ = new RadioButton<int?> { Group = group, Value = 7 };
        Assert.Null(group.SelectedButton);
    }

    [Fact]
    public void DuplicateValuesKeepButtonIdentityAndValueAssignmentMatchesTheFirst()
    {
        var group = new RadioGroup<string>();
        var first = new RadioButton<string> { Group = group, Value = "same" };
        var second = new RadioButton<string> { Group = group, Value = "same" };
        group.Select(first);
        var valueChanges = 0;
        var buttonChanges = 0;
        group.PropertyChanged += (_, change) =>
        {
            if (ReferenceEquals(change.Property, RadioGroup<string>.SelectedValueProperty)) valueChanges++;
            if (ReferenceEquals(change.Property, RadioGroup<string>.SelectedButtonProperty)) buttonChanges++;
        };

        group.Select(second);

        Assert.Same(second, group.SelectedButton);
        Assert.False(first.IsChecked);
        Assert.True(second.IsChecked);
        Assert.Equal(0, valueChanges);
        Assert.Equal(1, buttonChanges);

        group.SelectedValue = "same";

        Assert.Same(first, group.SelectedButton);
        Assert.True(first.IsChecked);
        Assert.False(second.IsChecked);
        Assert.Equal(0, valueChanges);
        Assert.Equal(2, buttonChanges);
    }

    [Fact]
    public void ReferenceBusinessValuesCompareByValueWhileSelectionKeepsButtonIdentity()
    {
        var group = new RadioGroup<BusinessValue>();
        var firstValue = new BusinessValue(1);
        var secondValue = new BusinessValue(1);
        var first = new RadioButton<BusinessValue> { Group = group, Value = firstValue };
        var second = new RadioButton<BusinessValue> { Group = group, Value = secondValue };
        group.SetValue(RadioGroup<BusinessValue>.SelectedValueProperty, new BusinessValue(1));
        Assert.Same(first, group.SelectedButton);
        var valueChanges = 0;
        var memberChanges = 0;
        using var selectionSubscription = group.Subscribe(RadioGroup<BusinessValue>.SelectedValueProperty,
            (_, _) => valueChanges++);
        using var memberSubscription = second.Subscribe(RadioButton<BusinessValue>.ValueProperty,
            (_, _) => memberChanges++);

        second.IsChecked = true;
        second.SetValue(RadioButton<BusinessValue>.ValueProperty, new BusinessValue(1));

        Assert.Same(second, group.SelectedButton);
        Assert.Same(secondValue, second.Value);
        Assert.Same(secondValue, group.SelectedValue);
        Assert.Equal(0, memberChanges);
        Assert.Equal(0, valueChanges);
        group.SelectedValue = new BusinessValue(1);
        Assert.Same(first, group.SelectedButton);
        Assert.Same(firstValue, group.SelectedValue);
        Assert.Equal(0, valueChanges);
    }

    [Fact]
    public void TwoWayBindingAcceptsEqualReferenceValueConfirmationWithoutChangingTheDuplicate()
    {
        var group = new RadioGroup<BusinessValue>();
        var first = new RadioButton<BusinessValue> { Group = group, Value = new BusinessValue(1) };
        var second = new RadioButton<BusinessValue> { Group = group, Value = new BusinessValue(1) };
        var source = new ReferenceValueSource();
        group.BindTwoWay(RadioGroup<BusinessValue>.SelectedValueProperty, source, ReferenceValueSource.ValueProperty);
        var sourceChanges = 0;
        using var subscription = source.Subscribe(ReferenceValueSource.ValueProperty, (_, _) => sourceChanges++);

        second.IsChecked = true;

        Assert.Same(second, group.SelectedButton);
        Assert.False(first.IsChecked);
        Assert.Equal(second.Value, source.Value);
        Assert.NotSame(second.Value, source.Value);
        Assert.Equal(1, sourceChanges);
        group.Clear();
        Assert.Null(source.Value);
        Assert.Equal(2, sourceChanges);
    }

    [Fact]
    public void SourceWritesWithoutNotificationsDoNotRefreshTheSelectedValue()
    {
        var group = new RadioGroup<int?>();
        var zero = new RadioButton<int?> { Group = group, Value = 0 };
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        var source = new NormalizingValueSource { Value = 0 };
        group.BindTwoWay(RadioGroup<int?>.SelectedValueProperty, source, NormalizingValueSource.ValueProperty);

        group.Select(second);

        Assert.Equal(1, source.Value);
        Assert.Equal(2, group.SelectedValue);
        Assert.Same(second, group.SelectedButton);
        Assert.False(first.IsChecked);
        source.NotifyWrites = true;
        source.Value = 0;
        Assert.Same(zero, group.SelectedButton);
        source.Value = 2;
        Assert.Same(first, group.SelectedButton);
        Assert.Equal(1, group.SelectedValue);
        Assert.True(group.IsBound(RadioGroup<int?>.SelectedValueProperty));
    }

    [Fact]
    public void UnconfiguredIsCheckedBindingDoesNotWriteBackWithoutATargetChange()
    {
        var button = new RadioButton<int?>();
        var source = new CheckedSource { IsSelected = true };
        button.BindTwoWay(RadioButton.IsCheckedProperty, source, CheckedSource.IsSelectedProperty);

        Assert.False(button.IsChecked);
        Assert.True(source.IsSelected);
        var group = new RadioGroup<int?>();
        button.Group = group;
        button.Value = 1;
        Assert.Null(group.SelectedButton);
        source.IsSelected = false;
        source.IsSelected = true;
        Assert.Same(button, group.SelectedButton);
    }

    [Fact]
    public void RejoiningMovesAMemberToTheEndOfValueMatchingOrder()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 1 };
        first.Group = null;
        first.Group = group;

        group.SelectedValue = 1;

        Assert.Same(second, group.SelectedButton);
    }

    [Fact]
    public void MovingSelectedMemberClearsTheOldGroupAndPreservesTheNewSelection()
    {
        var oldGroup = new RadioGroup<int?>();
        var newGroup = new RadioGroup<int?>();
        var moving = new RadioButton<int?> { Group = oldGroup, Value = 1 };
        var existing = new RadioButton<int?> { Group = newGroup, Value = 2 };
        oldGroup.Select(moving);
        newGroup.Select(existing);

        moving.Group = newGroup;

        Assert.Null(oldGroup.SelectedButton);
        Assert.Null(oldGroup.SelectedValue);
        Assert.Same(existing, newGroup.SelectedButton);
        Assert.False(moving.IsChecked);
        Assert.True(existing.IsChecked);
        newGroup.SelectedValue = 1;
        Assert.Same(moving, newGroup.SelectedButton);
    }

    [Fact]
    public void SelectedValueChangesFollowTheButtonAndNullClearsIt()
    {
        var group = new RadioGroup<int?>();
        var selected = new RadioButton<int?> { Group = group, Value = 1 };
        var other = new RadioButton<int?> { Group = group, Value = 2 };
        group.Select(selected);

        selected.Value = 2;

        Assert.Same(selected, group.SelectedButton);
        Assert.Equal(2, group.SelectedValue);
        Assert.False(other.IsChecked);
        selected.Value = null;
        Assert.Null(group.SelectedValue);
        Assert.False(selected.IsChecked);
    }

    [Fact]
    public void MemberValueNotificationsObserveTheUpdatedGroupAndPreserveOtherSelections()
    {
        var group = new RadioGroup<int?>();
        var selected = new RadioButton<int?> { Group = group, Value = 1, IsChecked = true };
        var other = new RadioButton<int?> { Group = group, Value = 3 };
        var changes = 0;
        void Observe(object? sender, PropertyChangedEventArgs change)
        {
            Assert.Equal(2, selected.Value);
            Assert.Equal(2, group.SelectedValue);
            Assert.Same(selected, group.SelectedButton);
            Assert.True(selected.IsChecked);
            changes++;
        }
        using (selected.Subscribe(RadioButton<int?>.ValueProperty, Observe))
        using (group.Subscribe(RadioGroup<int?>.SelectedValueProperty, Observe))
            selected.Value = 2;
        Assert.Equal(2, changes);

        using var subscription = other.Subscribe(RadioButton<int?>.ValueProperty, (_, change) =>
        {
            Assert.Equal(4, change.NewValue);
            Assert.Same(selected, group.SelectedButton);
            Assert.Equal(2, group.SelectedValue);
            Assert.False(other.IsChecked);
            changes++;
        });
        other.Value = 4;
        Assert.Equal(3, changes);
    }

    [Fact]
    public void MemberValueCallbackFailuresPreserveCommittedFieldsAndReleaseTheGuard()
    {
        var group = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = group, Value = 1, IsChecked = true };
        var error = new InvalidOperationException("group value observer");
        var valueNotifications = 0;
        using var valueSubscription = button.Subscribe(RadioButton<int?>.ValueProperty, (_, _) => valueNotifications++);
        using (group.Subscribe(RadioGroup<int?>.SelectedValueProperty, (_, _) => throw error))
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => button.Value = 2));

        Assert.Equal(2, button.Value);
        Assert.Equal(2, group.SelectedValue);
        Assert.Same(button, group.SelectedButton);
        Assert.True(button.IsChecked);
        Assert.Equal(0, valueNotifications);
        button.Value = 3;
        Assert.Equal(3, group.SelectedValue);
        Assert.Equal(1, valueNotifications);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GroupNotificationsFollowMembershipAndSelectionSynchronization(bool selected)
    {
        var oldGroup = new RadioGroup<int?>();
        var newGroup = new RadioGroup<int?>();
        var previous = new RadioButton<int?> { Group = oldGroup, Value = 3, IsChecked = true };
        var moving = new RadioButton<int?> { Group = oldGroup, Value = 1, IsChecked = selected };
        var existing = new RadioButton<int?> { Group = newGroup, Value = 2, IsChecked = true };
        var changes = 0;
        using var subscription = moving.Subscribe(RadioButton<int?>.GroupProperty, (_, change) =>
        {
            Assert.Same(oldGroup, change.OldValue);
            Assert.Same(newGroup, change.NewValue);
            Assert.Same(newGroup, moving.Group);
            Assert.Same(selected ? null : previous, oldGroup.SelectedButton);
            Assert.Equal(selected ? null : (int?)3, oldGroup.SelectedValue);
            Assert.False(moving.IsChecked);
            Assert.Same(existing, newGroup.SelectedButton);
            Assert.True(existing.IsChecked);
            moving.Group = newGroup;
            moving.Value = 1;
            Assert.Throws<InvalidOperationException>(() => moving.Group = null);
            Assert.Throws<InvalidOperationException>(() => moving.Value = 4);
            Assert.Throws<InvalidOperationException>(() => oldGroup.SelectedValue = selected ? (int?)3 : null);
            Assert.Throws<InvalidOperationException>(() => newGroup.Clear());
            changes++;
        });

        moving.Group = newGroup;

        Assert.Equal(1, changes);
    }

    [Fact]
    public void RejectedMigrationPreservesTheDestinationGuardAndReleasesTheSourceGuard()
    {
        var oldGroup = new RadioGroup<int?>();
        var newGroup = new RadioGroup<int?>();
        var moving = new RadioButton<int?> { Group = oldGroup, Value = 1, IsChecked = true };
        var existing = new RadioButton<int?> { Group = newGroup, Value = 2 };
        using (newGroup.Subscribe(RadioGroup<int?>.SelectedButtonProperty, (_, _) =>
        {
            Assert.Throws<InvalidOperationException>(() => moving.Group = newGroup);
            Assert.Same(oldGroup, moving.Group);
            Assert.Same(moving, oldGroup.SelectedButton);
            Assert.Throws<InvalidOperationException>(() => newGroup.Clear());
            oldGroup.Clear();
            Assert.Null(oldGroup.SelectedButton);
        }))
            newGroup.Select(existing);

        moving.Group = newGroup;
        newGroup.Select(moving);
        Assert.Same(moving, newGroup.SelectedButton);
        Assert.False(existing.IsChecked);
    }

    [Fact]
    public void ProgramSelectionAllowsDisabledHiddenAndDetachedMembers()
    {
        var group = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = group, Value = 1, IsEnabled = false };
        var root = new Canvas();
        root.Children.Add(button);
        group.Select(button);
        root.Children.Remove(button);
        button.Visibility = Visibility.Hidden;

        Assert.Same(button, group.SelectedButton);
        group.Clear();
        group.SelectedValue = 1;
        Assert.True(button.IsChecked);
    }

    [Fact]
    public void NullValuesCannotBeSelectedAndForeignMembersAreRejected()
    {
        var group = new RadioGroup<string>();
        var empty = new RadioButton<string> { Group = group };
        group.Select(empty);
        Assert.Null(group.SelectedButton);
        Assert.Throws<ArgumentException>(() => group.Select(new RadioButton<string> { Value = "foreign" }));
    }

    [Fact]
    public void NonNullableValueTypesAreRejectedAtConstruction()
    {
        Assert.Throws<NotSupportedException>(() => new RadioGroup<int>());
        Assert.Throws<NotSupportedException>(() => new RadioButton<int>());
    }

    [Fact]
    public void AllSelectionFieldsAreCommittedBeforeAnyNotification()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        group.Select(first);
        var observed = 0;
        void Observe(object? sender, PropertyChangedEventArgs change)
        {
            Assert.Same(second, group.SelectedButton);
            Assert.Equal(2, group.SelectedValue);
            Assert.False(first.IsChecked);
            Assert.True(second.IsChecked);
            observed++;
        }
        first.PropertyChanged += Observe;
        second.PropertyChanged += Observe;
        group.PropertyChanged += Observe;

        group.Select(second);

        Assert.True(observed > 0);
    }

    [Fact]
    public void SelectionNotificationsFollowTheDeclaredOrderAndSkipUnchangedValues()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        group.Select(first);
        var notifications = new List<(object? Sender, PropertyChangedEventArgs Change)>();
        void Record(object? sender, PropertyChangedEventArgs change) => notifications.Add((sender, change));
        first.PropertyChanged += Record;
        second.PropertyChanged += Record;
        group.PropertyChanged += Record;

        group.Select(second);

        Assert.Collection(notifications,
            entry => AssertNotification(entry, first, RadioButton<int?>.IsCheckedProperty, true, false),
            entry => AssertNotification(entry, second, RadioButton<int?>.IsCheckedProperty, false, true),
            entry => AssertNotification(entry, group, RadioGroup<int?>.SelectedButtonProperty, first, second),
            entry => AssertNotification(entry, group, RadioGroup<int?>.SelectedValueProperty, 1, 2));
        notifications.Clear();
        group.Select(second);
        Assert.Empty(notifications);
        second.Value = 1;
        notifications.Clear();
        group.Select(first);
        Assert.Collection(notifications,
            entry => AssertNotification(entry, second, RadioButton<int?>.IsCheckedProperty, true, false),
            entry => AssertNotification(entry, first, RadioButton<int?>.IsCheckedProperty, false, true),
            entry => AssertNotification(entry, group, RadioGroup<int?>.SelectedButtonProperty, second, first));
    }

    [Fact]
    public void NotificationExceptionsKeepTheCommittedSelectionAndReleaseTheGuard()
    {
        var group = new RadioGroup<int?>();
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        group.Select(first);
        var error = new InvalidOperationException("subscriber");
        EventHandler<PropertyChangedEventArgs> fail = (_, _) => throw error;
        first.PropertyChanged += fail;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => group.Select(second)));

        Assert.Same(second, group.SelectedButton);
        Assert.True(second.IsChecked);
        Assert.False(first.IsChecked);
        first.PropertyChanged -= fail;
        group.Clear();
        Assert.Null(group.SelectedButton);
    }

    [Fact]
    public void MembershipIsCommittedBeforeAnOldGroupCallbackFails()
    {
        var oldGroup = new RadioGroup<int?>();
        var newGroup = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = oldGroup, Value = 1 };
        oldGroup.Select(button);
        var error = new InvalidOperationException("old group");
        EventHandler<PropertyChangedEventArgs> fail = (_, _) => throw error;
        oldGroup.PropertyChanged += fail;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => button.Group = newGroup));

        Assert.Same(newGroup, button.Group);
        Assert.Null(oldGroup.SelectedButton);
        Assert.False(button.IsChecked);
        oldGroup.PropertyChanged -= fail;
        newGroup.Select(button);
        Assert.True(button.IsChecked);
    }

    [Fact]
    public void NotificationReentryRejectsChangesButAllowsConfirmation()
    {
        var group = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = group, Value = 1 };
        var other = new RadioButton<int?> { Group = group, Value = 2 };
        group.PropertyChanged += (_, _) =>
        {
            group.Select(button);
            group.SelectedValue = 1;
            button.Value = 1;
            button.Group = group;
            button.IsChecked = true;
            other.IsChecked = false;
            other.Value = 2;
            Assert.Throws<InvalidOperationException>(() => button.IsChecked = false);
            Assert.Throws<InvalidOperationException>(() => other.IsChecked = true);
            Assert.Throws<InvalidOperationException>(() => group.Clear());
            Assert.Throws<InvalidOperationException>(() => group.SelectedValue = 2);
            Assert.Throws<InvalidOperationException>(() => button.Value = 2);
            Assert.Throws<InvalidOperationException>(() => other.Value = 3);
            Assert.Throws<InvalidOperationException>(() => button.Group = null);
            Assert.Throws<InvalidOperationException>(() => new RadioButton<int?> { Group = group });
        };

        group.Select(button);

        Assert.Equal(1, group.SelectedValue);
    }

    [Fact]
    public void TwoWayBindingPreservesTheExactDuplicateAndWritesClearBack()
    {
        var group = new RadioGroup<int?>();
        var source = new ValueSource { Value = 0 };
        var zero = new RadioButton<int?> { Group = group, Value = 0 };
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 1 };
        group.BindTwoWay(RadioGroup<int?>.SelectedValueProperty, source, ValueSource.ValueProperty);
        Assert.Same(zero, group.SelectedButton);

        second.IsChecked = true;

        Assert.Equal(1, source.Value);
        Assert.Same(second, group.SelectedButton);
        Assert.False(first.IsChecked);
        second.IsChecked = false;
        Assert.Null(source.Value);
        source.Value = 1;
        Assert.Same(first, group.SelectedButton);
    }

    [Fact]
    public void OneWayBindingDoesNotWriteBackAndUnbindPreservesSelection()
    {
        var group = new RadioGroup<int?>();
        var source = new ValueSource { Value = 1 };
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        var second = new RadioButton<int?> { Group = group, Value = 2 };
        group.Bind(RadioGroup<int?>.SelectedValueProperty, source, ValueSource.ValueProperty);
        group.Select(second);
        Assert.Equal(1, source.Value);
        group.Unbind(RadioGroup<int?>.SelectedValueProperty);
        Assert.False(group.IsBound(RadioGroup<int?>.SelectedValueProperty));
        source.Value = null;
        Assert.Same(second, group.SelectedButton);
        group.ClearValue(RadioGroup<int?>.SelectedValueProperty);
        Assert.Null(group.SelectedButton);
        Assert.False(first.IsChecked);
    }

    [Fact]
    public void ClearValueRemovesAnActiveBindingAndLaterSourceChangesDoNotSelect()
    {
        var group = new RadioGroup<int?>();
        var source = new ValueSource { Value = 1 };
        var first = new RadioButton<int?> { Group = group, Value = 1 };
        _ = new RadioButton<int?> { Group = group, Value = 2 };
        group.BindTwoWay(RadioGroup<int?>.SelectedValueProperty, source, ValueSource.ValueProperty);

        group.ClearValue(RadioGroup<int?>.SelectedValueProperty);
        source.Value = 2;

        Assert.False(group.IsBound(RadioGroup<int?>.SelectedValueProperty));
        Assert.Null(group.SelectedValue);
        Assert.Null(group.SelectedButton);
        Assert.False(first.IsChecked);
    }

    [Fact]
    public void GroupWritesHonorTheThreadOfItsScreenOwnedMembers()
    {
        var group = new RadioGroup<int?>();
        var button = new RadioButton<int?> { Group = group, Value = 1 };
        var manager = new UiManager();
        manager.Open(new GameScreen(button));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { group.SelectedValue = 1; }
            catch (Exception error) { failure = error; }
        });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Null(group.SelectedButton);
        manager.Close();
    }

    private static void AssertNotification((object? Sender, PropertyChangedEventArgs Change) entry,
        object sender, Property property, object? oldValue, object? newValue)
    {
        Assert.Same(sender, entry.Sender);
        Assert.Same(property, entry.Change.Property);
        Assert.Equal(oldValue, entry.Change.OldValue);
        Assert.Equal(newValue, entry.Change.NewValue);
    }

    private sealed class CheckedSource : ObservableObject
    {
        internal static readonly DirectProperty<CheckedSource, bool> IsSelectedProperty =
            Property.RegisterDirect<CheckedSource, bool>(nameof(IsSelected),
                static source => source.IsSelected, static (source, value) => source.IsSelected = value);

        private bool _isSelected;

        internal bool IsSelected
        {
            get => _isSelected;
            set => SetField(IsSelectedProperty, ref _isSelected, value);
        }
    }

    private sealed class SelectedButtonSource : ObservableObject
    {
        internal static readonly DirectProperty<SelectedButtonSource, RadioButton?> ButtonProperty =
            Property.RegisterDirect<SelectedButtonSource, RadioButton?>(nameof(Button),
                static source => source.Button, static (source, value) => source.Button = value);

        private RadioButton? _button;

        internal RadioButton? Button
        {
            get => _button;
            set => SetField(ButtonProperty, ref _button, value);
        }
    }

    private sealed class ValueSource : ObservableObject
    {
        internal static readonly DirectProperty<ValueSource, int?> ValueProperty =
            Property.RegisterDirect<ValueSource, int?>(nameof(Value),
                static source => source.Value, static (source, value) => source.Value = value);
        private int? _value;
        internal int? Value
        {
            get => _value;
            set => SetField(ValueProperty, ref _value, value);
        }
    }

    private sealed record BusinessValue(int Id);

    private sealed class ReferenceValueSource : ObservableObject
    {
        internal static readonly DirectProperty<ReferenceValueSource, BusinessValue?> ValueProperty =
            Property.RegisterDirect<ReferenceValueSource, BusinessValue?>(nameof(Value),
                static source => source.Value, static (source, value) => source.Value = value);

        private BusinessValue? _value;

        internal BusinessValue? Value
        {
            get => _value;
            set => SetField(ValueProperty, ref _value, value is null ? null : new BusinessValue(value.Id));
        }
    }

    private sealed class NormalizingValueSource : ObservableObject
    {
        internal static readonly DirectProperty<NormalizingValueSource, int?> ValueProperty =
            Property.RegisterDirect<NormalizingValueSource, int?>(nameof(Value),
                static source => source.Value, static (source, value) => source.Value = value);

        private int? _value;
        internal bool NotifyWrites { get; set; }

        internal int? Value
        {
            get => _value;
            set
            {
                var normalized = value == 2 ? 1 : value;
                if (NotifyWrites)
                    SetField(ValueProperty, ref _value, normalized);
                else
                    _value = normalized;
            }
        }
    }
}
