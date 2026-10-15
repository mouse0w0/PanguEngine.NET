using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.ComponentModel;

public sealed class DirectPropertyTests
{
    [Fact]
    public void ReadsFieldInsteadOfRegisteredResetValue()
    {
        var host = new Host();

        Assert.True(Host.ValueProperty.IsDirect);
        Assert.False(Host.ValueProperty.IsReadOnly);
        Assert.Equal(-1, Host.ValueProperty.UnsetValue);
        Assert.Equal(-1, ((Property)Host.ValueProperty).DefaultValue);
        Assert.Equal(7, host.GetValue(Host.ValueProperty));
        Assert.False(host.HasStoredValue);
    }

    [Fact]
    public void WritesThroughSetterAndNotifiesOnceInOrder()
    {
        var host = new Host();
        host.PropertyChanged += (_, e) =>
        {
            Assert.Equal(9, host.Value);
            var change = Assert.IsType<PropertyChangedEventArgs<int>>(e);
            Assert.Equal(7, change.OldValue);
            Assert.Equal(9, change.NewValue);
            host.Notifications.Add("event");
        };
        using var subscription = host.Subscribe(Host.ValueProperty, (_, _) => host.Notifications.Add("subscription"));

        host.SetValue(Host.ValueProperty, 9);
        host.Value = 9;

        Assert.Equal(2, host.SetterCalls);
        Assert.Equal(new[] { "callback", "event", "subscription" }, host.Notifications);
        Assert.False(host.HasStoredValue);
    }

    [Fact]
    public void SetFieldReportsWhetherTheFieldChanged()
    {
        var host = new Host();

        Assert.False(host.UpdateValue(7));
        Assert.True(host.UpdateValue(8));
        Assert.Equal(8, host.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetFieldUsesReferenceEqualityForRecordsUnlessValueEqualityIsConfigured(bool useValueComparer)
    {
        var original = new EqualValue(7);
        var host = new ValueHost<EqualValue>(original);
        var changes = new List<PropertyChangedEventArgs<EqualValue>>();
        var property = useValueComparer ? ValueHost<EqualValue>.ValueComparedProperty : ValueHost<EqualValue>.ValueProperty;
        using var subscription = host.Subscribe(property, (_, change) => changes.Add(change));
        var replacement = new EqualValue(7);

        Assert.Equal(!useValueComparer, host.UpdateValue(replacement, useValueComparer));
        Assert.False(host.UpdateValue(replacement, useValueComparer));
        Assert.Same(useValueComparer ? original : replacement, host.Value);
        if (useValueComparer)
            Assert.Empty(changes);
        else
        {
            var change = Assert.Single(changes);
            Assert.Same(original, change.OldValue);
            Assert.Same(replacement, change.NewValue);
        }
    }

    [Fact]
    public void SetFieldUsesContentEqualityForStrings()
    {
        var original = new string('a', 1);
        var host = new ValueHost<string>(original);
        var changes = 0;
        using var subscription = host.Subscribe(ValueHost<string>.ValueProperty, (_, _) => changes++);

        Assert.False(host.UpdateValue(new string('a', 1)));
        Assert.Same(original, host.Value);
        Assert.Equal(0, changes);
        Assert.True(host.UpdateValue("b"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SetFieldUsesValueEqualityForNullableValueTypes()
    {
        var host = new ValueHost<int?>(7);
        var changes = new List<(int? OldValue, int? NewValue)>();
        using var subscription = host.Subscribe(ValueHost<int?>.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));

        Assert.False(host.UpdateValue(7));
        Assert.True(host.UpdateValue(null));
        Assert.False(host.UpdateValue(null));
        Assert.True(host.UpdateValue(7));

        Assert.Equal(new (int?, int?)[] { (7, null), (null, 7) }, changes);
    }

    [Fact]
    public void ClearCallsSetterEvenWithoutALocalValue()
    {
        var host = new Host();

        host.ClearValue(Host.ValueProperty);
        host.ClearValue(Host.ValueProperty);

        Assert.Equal(-1, host.Value);
        Assert.Equal(2, host.SetterCalls);
        Assert.Single(host.Notifications);
    }

    [Fact]
    public void ReadOnlyPropertyAllowsInternalUpdatesAndRejectsPublicWrites()
    {
        var host = new Host();
        var changes = 0;
        using var subscription = host.Subscribe(Host.ActiveProperty, (_, _) => changes++);

        Assert.True(Host.ActiveProperty.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => host.SetValue(Host.ActiveProperty, true));
        Assert.Throws<InvalidOperationException>(() => host.ClearValue(Host.ActiveProperty));
        host.SetActive(true);
        host.SetActive(true);

        Assert.True(host.GetValue(Host.ActiveProperty));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AccessPolicyRejectsFieldChangesBeforeAssignment()
    {
        var host = new Host { RejectWrites = true };

        Assert.Throws<InvalidOperationException>(() => host.Value = 8);
        Assert.Throws<InvalidOperationException>(() => host.SetActive(true));
        Assert.Equal(7, host.Value);
        Assert.False(host.GetValue(Host.ActiveProperty));
        Assert.Empty(host.Notifications);
    }

    [Fact]
    public void ForeignOwnersCannotReadWriteOrSetField()
    {
        var foreign = new ForeignHost();

        Assert.Throws<ArgumentException>(() => foreign.GetValue(Host.ValueProperty));
        Assert.Throws<ArgumentException>(() => foreign.SetValue(Host.ValueProperty, 1));
        Assert.Throws<ArgumentException>(() => foreign.ClearValue(Host.ValueProperty));
        Assert.Throws<ArgumentException>(() => foreign.UpdateForeignField());
        Assert.Equal(0, foreign.Value);
    }

    [Fact]
    public void InvalidGetterDoesNotReserveRegistrationName()
    {
        var name = Guid.NewGuid().ToString();
        Assert.Throws<ArgumentNullException>(() => Property.RegisterDirect<Host, int>(name, null!));

        var property = Property.RegisterDirect<Host, int>(name, o => o.Value);

        Assert.Equal(7, new Host().GetValue(property));
        Assert.Throws<InvalidOperationException>(() => Property.Register<Host, int>(name));
    }

    [Fact]
    public void CallbackFailurePreservesFieldAndSkipsHostNotifications()
    {
        var host = new Host { ThrowCallback = true };
        var notified = false;
        host.PropertyChanged += (_, _) => notified = true;

        Assert.Throws<InvalidOperationException>(() => host.Value = 9);

        Assert.Equal(9, host.Value);
        Assert.False(notified);
    }

    private sealed class Host : ObservableObject
    {
        internal static readonly DirectProperty<Host, int> ValueProperty =
            Property.RegisterDirect<Host, int>(nameof(Value), o => o.Value, (o, v) => o.Value = v,
                unsetValue: -1, onChanged: (o, _, _) =>
                {
                    o.Notifications.Add("callback");
                    if (o.ThrowCallback)
                        throw new InvalidOperationException("callback failed");
                });

        internal static readonly DirectProperty<Host, bool> ActiveProperty =
            Property.RegisterDirect<Host, bool>(nameof(Active), o => o.Active);

        private int _value = 7;
        private bool _active;
        internal bool Active => _active;
        internal List<string> Notifications { get; } = [];
        internal int SetterCalls { get; private set; }
        internal bool RejectWrites { get; init; }
        internal bool ThrowCallback { get; init; }
        internal bool HasStoredValue => HasLocalValue(ValueProperty);

        internal int Value
        {
            get => _value;
            set
            {
                SetterCalls++;
                SetField(ValueProperty, ref _value, value);
            }
        }

        internal bool UpdateValue(int value) => SetField(ValueProperty, ref _value, value);
        internal void SetActive(bool value) => SetField(ActiveProperty, ref _active, value);

        protected override void VerifyMutationAccess()
        {
            if (RejectWrites)
                throw new InvalidOperationException("write rejected");
        }
    }

    private sealed class ValueHost<T>(T value) : ObservableObject
    {
        internal static readonly DirectProperty<ValueHost<T>, T> ValueProperty =
            Property.RegisterDirect<ValueHost<T>, T>(nameof(Value), host => host.Value);
        internal static readonly DirectProperty<ValueHost<T>, T> ValueComparedProperty =
            Property.RegisterDirect<ValueHost<T>, T>("ValueCompared", host => host.Value,
                comparer: EqualityComparer<T>.Default);

        private T _value = value;
        internal T Value => _value;

        internal bool UpdateValue(T next, bool useValueComparer = false) =>
            SetField(useValueComparer ? ValueComparedProperty : ValueProperty, ref _value, next);
    }

    private sealed record EqualValue(int Number);

    private sealed class ForeignHost : ObservableObject
    {
        private int _value;
        internal int Value => _value;
        internal void UpdateForeignField() => SetField(Host.ValueProperty, ref _value, 1);
    }
}
