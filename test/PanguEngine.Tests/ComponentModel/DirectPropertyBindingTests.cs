using System.ComponentModel;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.ComponentModel;

public sealed class DirectPropertyBindingTests
{
    [Fact]
    public void TwoWayInitialValueDoesNotWriteTargetNormalizationBackToSource()
    {
        var source = new Host { Value = 20 };
        var target = new Host { Maximum = 10 };
        source.SetterCalls = 0;

        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);

        Assert.Equal(20, source.Value);
        Assert.Equal(10, target.Value);
        Assert.Equal(0, source.SetterCalls);
    }

    [Fact]
    public void ReverseConverterCanUnbindBeforeSourceWrite()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty, value => value,
            (int value, out int result) =>
            {
                target.Unbind(Host.ValueProperty);
                result = value;
                return true;
            });

        target.Value = 3;

        Assert.Equal(2, source.Value);
        Assert.Equal(3, target.Value);
        Assert.False(target.IsBound(Host.ValueProperty));
    }

    [Fact]
    public void NonIdempotentSettersDoNotRecursivelyWriteBackResynchronization()
    {
        var source = new Host { Adjustment = 1 };
        var target = new Host { Adjustment = 1 };
        target.PropertyChanged += (_, _) =>
        {
            if (target.SetterCalls > 8)
                throw new InvalidOperationException("Binding writeback did not terminate.");
        };
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.SetterCalls = 0;
        target.SetterCalls = 0;

        target.Value = 10;

        Assert.Equal(12, source.Value);
        Assert.Equal(13, target.Value);
        Assert.Equal(1, source.SetterCalls);
        Assert.Equal(2, target.SetterCalls);
    }

    [Fact]
    public void SourceSynchronizationDoesNotEchoThroughItsSetter()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        source.SetterCalls = 0;

        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);

        Assert.Equal(0, source.SetterCalls);
        source.Value = 3;
        Assert.Equal(1, source.SetterCalls);
        Assert.Equal(3, target.Value);
    }

    [Fact]
    public void SourceUpdateDoesNotWriteTargetNormalizationBackToSource()
    {
        var source = new Host { Value = 2 };
        var target = new Host { Maximum = 10 };
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.SetterCalls = 0;

        source.Value = 20;

        Assert.Equal(10, target.Value);
        Assert.Equal(20, source.Value);
        Assert.Equal(1, source.SetterCalls);
    }

    [Fact]
    public void ReentrantNotificationWritesLatestTargetValueInsteadOfOuterEventValue()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.PropertyChanged += (_, _) =>
        {
            if (target.Value == 3)
                target.Value = 4;
        };
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.SetterCalls = 0;

        target.Value = 3;

        Assert.Equal(4, source.Value);
        Assert.Equal(4, target.Value);
        Assert.Equal(1, source.SetterCalls);
    }

    [Fact]
    public void TwoWayUnbindRemovesTargetListener()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);

        target.Unbind(Host.ValueProperty);
        target.Value = 3;

        Assert.Equal(2, source.Value);
        source.Value = 4;
        Assert.Equal(3, target.Value);
    }

    [Fact]
    public void OneWayExpressionBindingReappliesUnchangedSourceAfterLocalWrite()
    {
        var source = new Model { Text = "2" };
        var target = new Host();
        target.Bind(Host.ValueProperty, source, o => o.Text, int.Parse);

        target.Value = 9;
        source.Text = "2";

        Assert.Equal(2, target.Value);
        Assert.True(target.IsBound(Host.ValueProperty));
    }

    [Fact]
    public void OneWayBindingAllowsBothWriteEntrypointsWithoutDetaching()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);

        Assert.Equal(2, target.Value);
        target.Value = 3;
        Assert.Equal(3, target.Value);
        target.SetValue(Host.ValueProperty, 5);
        Assert.Equal(5, target.Value);
        Assert.Equal(2, source.Value);
        Assert.True(target.IsBound(Host.ValueProperty));
        source.Value = 4;
        Assert.Equal(4, target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoWayWritesSourceOnceAndAcceptsNormalization(bool useSetValue)
    {
        var source = new Host { Maximum = 10, Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.SetterCalls = 0;

        if (useSetValue)
            target.SetValue(Host.ValueProperty, 20);
        else
            target.Value = 20;

        Assert.Equal(1, source.SetterCalls);
        Assert.Equal(10, source.Value);
        Assert.Equal(10, target.Value);
    }

    [Fact]
    public void TargetSetterNormalizationIsTheValueWrittenBack()
    {
        var source = new Host { Value = 2 };
        var target = new Host { Maximum = 10 };
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.SetterCalls = 0;

        target.SetValue(Host.ValueProperty, 20);

        Assert.Equal(10, source.Value);
        Assert.Equal(1, source.SetterCalls);
    }

    [Fact]
    public void StoredPropertyCanDriveDirectPropertyInBothDirections()
    {
        var source = new Host();
        source.SetValue(Host.StoredProperty, 2);
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.StoredProperty);

        Assert.Equal(2, target.Value);
        target.Value = 3;
        Assert.Equal(3, source.GetValue(Host.StoredProperty));
        source.SetValue(Host.StoredProperty, 4);
        Assert.Equal(4, target.Value);
    }

    [Fact]
    public void EqualInitialValueCreatesBindingThatAllowsLocalWrites()
    {
        var source = new Host();
        var target = new Host();
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);

        Assert.True(target.IsBound(Host.ValueProperty));
        target.Value = 1;
        Assert.Equal(1, target.Value);
        Assert.Equal(0, source.Value);
        source.Value = 2;
        Assert.Equal(2, target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedInitialBindingWriteKeepsBindingForLaterUpdates(bool twoWay)
    {
        var source = new Host { Value = 2 };
        var target = new Host { ThrowSetter = true };

        Assert.Throws<InvalidOperationException>(() =>
        {
            if (twoWay)
                target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
            else
                target.Bind(Host.ValueProperty, source, Host.ValueProperty);
        });
        target.ThrowSetter = false;

        Assert.True(target.IsBound(Host.ValueProperty));
        target.Value = 9;
        Assert.Equal(twoWay ? 9 : 2, source.Value);
        source.Value = 3;
        Assert.Equal(3, target.Value);
    }

    [Fact]
    public void ReadOnlySourceUpdatesBothStorageKindsAndRejectsWritableRoles()
    {
        var source = new Host();
        var direct = new Host();
        var stored = new Host();
        direct.Bind(Host.ValueProperty, source, Host.ReadOnlyProperty);
        stored.Bind(Host.StoredProperty, source, Host.ReadOnlyProperty);

        source.UpdateReadOnly(4);

        Assert.Equal(4, direct.Value);
        Assert.Equal(4, stored.GetValue(Host.StoredProperty));
        Assert.Throws<InvalidOperationException>(() =>
            new Host().Bind(Host.ReadOnlyProperty, source, Host.ValueProperty));
        Assert.Throws<InvalidOperationException>(() =>
            new Host().BindTwoWay(Host.ReadOnlyProperty, source, Host.ValueProperty));
        Assert.Throws<InvalidOperationException>(() =>
            new Host().BindTwoWay(Host.ValueProperty, source, Host.ReadOnlyProperty));
    }

    [Fact]
    public void ExpressionBindingSupportsConversionsInBothDirections()
    {
        var source = new Model { Text = "2" };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, o => o.Text, int.Parse,
            static (int value, out string result) => { result = value.ToString(); return true; });

        Assert.Equal(2, target.Value);
        target.Value = 7;
        Assert.Equal("7", source.Text);
        source.Text = "8";
        Assert.Equal(8, target.Value);
    }

    [Fact]
    public void UnbindKeepsFieldWithoutCallingAccessorsOrCreatingLocalValue()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);
        target.SetterCalls = 0;
        target.ThrowOnRead = true;

        target.Unbind(Host.ValueProperty);
        source.Value = 3;

        target.ThrowOnRead = false;
        Assert.Equal(2, target.Value);
        Assert.Equal(0, target.SetterCalls);
        Assert.False(target.HasStoredValue);
        Assert.False(target.IsBound(Host.ValueProperty));
    }

    [Fact]
    public void ClearUnbindsBeforeResettingField()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);

        target.ClearValue(Host.ValueProperty);

        Assert.Equal(-1, target.Value);
        Assert.Equal(2, source.Value);
        Assert.False(target.IsBound(Host.ValueProperty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotificationDuringOneWaySourceUpdateCanChangeTargetWithoutWriteback(bool useSetValue)
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);
        target.PropertyChanged += (_, _) =>
        {
            if (target.Value == 3)
            {
                if (useSetValue)
                    target.SetValue(Host.ValueProperty, 9);
                else
                    target.Value = 9;
            }
        };

        source.Value = 3;

        Assert.Equal(9, target.Value);
        Assert.Equal(3, source.Value);
        source.Value = 4;
        Assert.Equal(4, target.Value);
    }

    [Fact]
    public void TargetChangesDuringSourceSynchronizationDoNotWriteBack()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        target.PropertyChanged += (_, _) =>
        {
            if (target.Value == 3)
                target.Value = 4;
        };

        source.Value = 3;

        Assert.Equal(4, target.Value);
        Assert.Equal(3, source.Value);
        target.Value = 5;
        Assert.Equal(5, source.Value);
    }

    [Fact]
    public void NestedSourceUpdatesDoNotEnableWritebackInsideOuterSynchronization()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        target.PropertyChanged += (_, _) =>
        {
            if (target.Value == 3)
            {
                source.Value = 4;
                target.Value = 5;
            }
        };

        source.Value = 3;

        Assert.Equal(5, target.Value);
        Assert.Equal(4, source.Value);
        target.Value = 6;
        Assert.Equal(6, source.Value);
    }

    [Fact]
    public void NotificationCanChangeAnotherOneWayTargetWithoutWritingSource()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.Bind(Host.OtherProperty, source, Host.ValueProperty);
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);
        target.PropertyChanged += (_, e) =>
        {
            if (e.Property == Host.ValueProperty)
                target.Other = 9;
        };

        source.Value = 3;

        Assert.Equal(9, target.Other);
        Assert.Equal(3, source.Value);
    }

    [Fact]
    public void NotificationCanReplaceBindingWithoutWritingOldValueToNewSource()
    {
        var first = new Host { Value = 2 };
        var second = new Host { Value = 8 };
        var target = new Host();
        target.PropertyChanged += (_, _) =>
        {
            if (target.Value == 3)
            {
                target.Unbind(Host.ValueProperty);
                target.BindTwoWay(Host.ValueProperty, second, Host.ValueProperty);
            }
        };
        target.BindTwoWay(Host.ValueProperty, first, Host.ValueProperty);

        target.Value = 3;

        Assert.Equal(2, first.Value);
        Assert.Equal(8, second.Value);
        Assert.Equal(8, target.Value);
    }

    [Fact]
    public void NotificationFailurePreservesFieldAndSkipsWriteback()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        target.ThrowNotifications = true;

        Assert.Throws<InvalidOperationException>(() => target.Value = 3);

        Assert.Equal(3, target.Value);
        Assert.Equal(2, source.Value);
        Assert.True(target.IsBound(Host.ValueProperty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedTargetSynchronizationDoesNotDisableLaterWriteback(bool failBeforeField)
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        target.ThrowSetter = failBeforeField;
        target.ThrowNotifications = !failBeforeField;

        Assert.Throws<InvalidOperationException>(() => source.Value = 3);
        target.ThrowSetter = false;
        target.ThrowNotifications = false;
        target.Value = 9;

        Assert.Equal(9, source.Value);
        Assert.True(target.IsBound(Host.ValueProperty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedBindingWriteAllowsLaterLocalAndSourceUpdates(bool failBeforeField)
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.Bind(Host.ValueProperty, source, Host.ValueProperty);
        target.ThrowSetter = failBeforeField;
        target.ThrowNotifications = !failBeforeField;

        Assert.Throws<InvalidOperationException>(() => source.Value = 3);
        target.ThrowSetter = false;
        target.ThrowNotifications = false;

        target.Value = 9;
        Assert.Equal(3, source.Value);
        source.Value = 4;
        Assert.Equal(4, target.Value);
    }

    [Fact]
    public void SourceSetterFailurePreservesCommittedTargetAndBinding()
    {
        var source = new Host { Value = 2 };
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);
        source.ThrowSetter = true;

        Assert.Throws<InvalidOperationException>(() => target.Value = 3);

        Assert.Equal(3, target.Value);
        Assert.Equal(2, source.Value);
        Assert.True(target.IsBound(Host.ValueProperty));
        source.ThrowSetter = false;
        target.Value = 4;
        Assert.Equal(4, source.Value);
    }

    [Fact]
    public void TwoWaySourceCanBeTheTargetOfAOneWayBinding()
    {
        var root = new Host { Value = 2 };
        var source = new Host();
        source.Bind(Host.ValueProperty, root, Host.ValueProperty);
        var target = new Host();
        target.BindTwoWay(Host.ValueProperty, source, Host.ValueProperty);

        target.Value = 3;
        Assert.Equal(3, source.Value);
        Assert.Equal(2, root.Value);
        root.Value = 4;
        Assert.Equal(4, source.Value);
        Assert.Equal(4, target.Value);
    }

    [Fact]
    public void MutualBindingsTerminateAtEqualValues()
    {
        var first = new Host();
        var second = new Host();
        first.BindTwoWay(Host.ValueProperty, second, Host.ValueProperty);
        second.BindTwoWay(Host.ValueProperty, first, Host.ValueProperty);

        first.Value = 4;

        Assert.Equal(4, first.Value);
        Assert.Equal(4, second.Value);
    }

    private sealed class Host : ObservableObject
    {
        internal static readonly DirectProperty<Host, int> ValueProperty =
            Property.RegisterDirect<Host, int>(nameof(Value), o => o.Value, (o, v) => o.Value = v, -1);
        internal static readonly DirectProperty<Host, int> OtherProperty =
            Property.RegisterDirect<Host, int>(nameof(Other), o => o.Other, (o, v) => o.Other = v);
        internal static readonly DirectProperty<Host, int> ReadOnlyProperty =
            Property.RegisterDirect<Host, int>("ReadOnly", o => o._readOnly);
        internal static readonly Property<int> StoredProperty = Property.Register<Host, int>("Stored");

        private int _value;
        private int _other;
        private int _readOnly;
        internal int SetterCalls { get; set; }
        internal int Maximum { get; init; } = int.MaxValue;
        internal int Adjustment { get; init; }
        internal bool ThrowOnRead { get; set; }
        internal bool ThrowSetter { get; set; }
        internal bool ThrowNotifications { get; set; }
        internal bool HasStoredValue => HasLocalValue(ValueProperty);

        internal int Value
        {
            get => ThrowOnRead ? throw new InvalidOperationException("getter failed") : _value;
            set
            {
                SetterCalls++;
                if (ThrowSetter)
                    throw new InvalidOperationException("setter failed");
                SetField(ValueProperty, ref _value, Math.Min(value + Adjustment, Maximum));
            }
        }

        internal int Other
        {
            get => _other;
            set => SetField(OtherProperty, ref _other, value);
        }

        internal void UpdateReadOnly(int value) => SetField(ReadOnlyProperty, ref _readOnly, value);

        protected override void OnPropertyChanged(PanguEngine.ComponentModel.PropertyChangedEventArgs eventArgs)
        {
            if (ThrowNotifications)
                throw new InvalidOperationException("notification failed");
            base.OnPropertyChanged(eventArgs);
        }
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private string _text = "0";
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Text)));
            }
        }
    }
}
