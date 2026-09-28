using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.ComponentModel;

public sealed class DirectPropertyTests
{
    [Fact]
    public void ReadsFieldInsteadOfDefaultOrFallback()
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
            Property.RegisterDirect<Host, bool>("Active", o => o._active);

        private int _value = 7;
        private bool _active;
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

        protected override T GetFallbackValue<T>(Property<T> property) =>
            throw new InvalidOperationException("Direct properties must not use fallback values.");

        protected override void VerifyMutationAccess()
        {
            if (RejectWrites)
                throw new InvalidOperationException("write rejected");
        }
    }

    private sealed class ForeignHost : ObservableObject
    {
        private int _value;
        internal int Value => _value;
        internal void UpdateForeignField() => SetField(Host.ValueProperty, ref _value, 1);
    }
}
