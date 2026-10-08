using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.ComponentModel;

public sealed class PropertyValidationBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedSourceUpdatePreservesTargetAndBinding(bool twoWay)
    {
        var source = new Source { Value = 5 };
        var target = new Target();
        if (twoWay)
            target.BindTwoWay(Target.ValueProperty, source, Source.ValueProperty);
        else
            target.Bind(Target.ValueProperty, source, Source.ValueProperty);
        var notifications = 0;
        using var subscription = target.Subscribe(Target.ValueProperty, (_, _) => notifications++);

        Assert.Throws<ArgumentException>(() => source.Value = -1);

        Assert.Equal(-1, source.Value);
        Assert.Equal(5, target.Value);
        Assert.True(target.IsBound(Target.ValueProperty));
        Assert.Equal(0, notifications);
        source.Value = 7;
        Assert.Equal(7, target.Value);
        Assert.Equal(1, notifications);
        if (twoWay)
        {
            Assert.Throws<ArgumentException>(() => target.Value = -2);
            Assert.Equal(7, source.Value);
            target.Value = 9;
            Assert.Equal(9, source.Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedInitialValueRetainsBindingAndLaterSourceUpdateRecovers(bool twoWay)
    {
        var source = new Source { Value = -1 };
        var target = new Target { Value = 5 };

        Assert.Throws<ArgumentException>(() =>
        {
            if (twoWay)
                target.BindTwoWay(Target.ValueProperty, source, Source.ValueProperty);
            else
                target.Bind(Target.ValueProperty, source, Source.ValueProperty);
        });

        Assert.True(target.IsBound(Target.ValueProperty));
        Assert.Equal(5, target.Value);
        source.Value = 7;
        Assert.Equal(7, target.Value);
        if (twoWay)
        {
            target.Value = 9;
            Assert.Equal(9, source.Value);
        }
    }

    [Fact]
    public void UnbindAfterRejectedInitializationDoesNotMaterializeFallback()
    {
        var source = new Source { Value = -1 };
        var target = new Target { Fallback = 5 };

        Assert.Throws<ArgumentException>(() => target.Bind(Target.ValueProperty, source, Source.ValueProperty));
        target.Unbind(Target.ValueProperty);

        Assert.False(target.IsBound(Target.ValueProperty));
        Assert.False(target.HasValue);
        Assert.Equal(5, target.Value);
        target.Fallback = 7;
        source.Value = 9;
        Assert.Equal(7, target.Value);
    }

    [Fact]
    public void UnbindAfterRejectedInitializationPreservesAnExistingLocalValue()
    {
        var source = new Source { Value = -1 };
        var target = new Target { Fallback = 3, Value = 5 };

        Assert.Throws<ArgumentException>(() => target.Bind(Target.ValueProperty, source, Source.ValueProperty));
        target.Unbind(Target.ValueProperty);

        Assert.False(target.IsBound(Target.ValueProperty));
        Assert.True(target.HasValue);
        target.Fallback = 7;
        source.Value = 9;
        Assert.Equal(5, target.Value);
        target.ClearValue(Target.ValueProperty);
        Assert.Equal(7, target.Value);
    }

    [Fact]
    public void ExpressionConverterRejectionsKeepBindingAndRecover()
    {
        var source = new NotifyingSource { Value = 0 };
        var target = new Target { Value = 5 };

        var error = Assert.Throws<ArgumentException>(() =>
            target.Bind(Target.ValueProperty, source, model => model.Value, static value => value - 1));

        Assert.StartsWith("Value must be non-negative.", error.Message);
        Assert.Equal("value", error.ParamName);
        Assert.True(target.IsBound(Target.ValueProperty));
        Assert.Equal(5, target.Value);
        source.Value = 7;
        Assert.Equal(6, target.Value);
        Assert.Throws<ArgumentException>(() => source.Value = 0);
        Assert.True(target.IsBound(Target.ValueProperty));
        Assert.Equal(6, target.Value);
        source.Value = 5;
        Assert.Equal(4, target.Value);
    }

    [Fact]
    public void PropertyConverterResultIsValidated()
    {
        var source = new Source { Value = 2 };
        var target = new Target();
        target.Bind(Target.ValueProperty, source, Source.ValueProperty, static value => value - 1);
        Assert.Equal(1, target.Value);

        Assert.Throws<ArgumentException>(() => source.Value = 0);

        Assert.True(target.IsBound(Target.ValueProperty));
        Assert.Equal(1, target.Value);
        source.Value = 5;
        Assert.Equal(4, target.Value);
    }

    private sealed class Source : ObservableObject
    {
        internal static readonly Property<int> ValueProperty = Property.Register<Source, int>("Value");

        internal int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }

    private sealed class Target : ObservableObject
    {
        internal static readonly Property<int> ValueProperty = Property.Register<Target, int>(
            "Value", validate: static value => value >= 0, validationMessage: "Value must be non-negative.");

        internal int Fallback { get; set; }

        internal bool HasValue => HasLocalValue(ValueProperty);

        internal int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        protected override T GetFallbackValue<T>(Property<T> property) =>
            ReferenceEquals(property, ValueProperty) ? (T)(object)Fallback : base.GetFallbackValue(property);
    }

    private sealed class NotifyingSource : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get;
            set
            {
                if (field == value)
                    return;
                field = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        } = 1;
    }
}
