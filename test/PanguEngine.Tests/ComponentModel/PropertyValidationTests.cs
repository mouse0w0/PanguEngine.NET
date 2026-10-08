using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.ComponentModel;

public sealed class PropertyValidationTests
{
    [Fact]
    public void InvalidDefaultIsRejectedBeforeDescriptorPublication()
    {
        var name = $"Value_{Guid.NewGuid():N}";

        var exception = Assert.Throws<ArgumentException>(() =>
            Property.Register<DefaultOwner, int>(
                name,
                -1,
                validate: static value => value >= 0));
        Assert.Equal("defaultValue", exception.ParamName);

        var property = Property.Register<DefaultOwner, int>(
            name,
            0,
            validate: static value => value >= 0);

        Assert.Equal(0, property.DefaultValue);
    }

    [Fact]
    public void ValidatorExceptionPropagatesBeforeDescriptorPublication()
    {
        var name = $"Value_{Guid.NewGuid():N}";
        var expected = new InvalidOperationException("validator");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            Property.Register<DefaultOwner, int>(
                name,
                validate: _ => throw expected));

        Assert.Same(expected, actual);
        Assert.NotNull(Property.Register<DefaultOwner, int>(name));
    }

    [Fact]
    public void InvalidDefaultValidationPrecedesDuplicateRegistrationFailure()
    {
        _ = Property.Register<DuplicateOwner, int>("Value", 1);

        Assert.Throws<ArgumentException>(() =>
            Property.Register<DuplicateOwner, int>(
                "Value",
                -1,
                validate: static value => value >= 0));
    }

    [Fact]
    public void OnChangedRemainsTheThirdPositionalRegistrationArgument()
    {
        var owner = new LegacyOwner();
        var lastValue = -1;
        var property = Property.Register<LegacyOwner, int>(
            "Value",
            0,
            (_, _, newValue) => lastValue = newValue);

        owner.SetValue(property, 3);

        Assert.Equal(3, lastValue);
    }

    [Fact]
    public void ReadOnlyAndAttachedOnChangedRemainThirdPositionalArguments()
    {
        var owner = new ValueOwner();
        var readOnlyValue = -1;
        var key = Property.RegisterReadOnly<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", 0, (_, _, newValue) => readOnlyValue = newValue);
        owner.SetKeyValue(key, 3);
        Assert.Equal(3, readOnlyValue);

        var attachedValue = -1;
        var property = Property.RegisterAttached<AttachedOwner, ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", 0, (_, _, newValue) => attachedValue = newValue);
        owner.SetValue(property, 5);
        Assert.Equal(5, attachedValue);
    }

    [Fact]
    public void RejectedValuesAreNotFormattedForExceptionMessages()
    {
        var name = $"Value_{Guid.NewGuid():N}";
        var invalid = new UnformattableValue(false);
        var registrationError = Assert.Throws<ArgumentException>(() => Property.Register<ValueOwner, UnformattableValue>(
            name, invalid, validate: static value => value.IsValid));
        Assert.Equal("defaultValue", registrationError.ParamName);
        Assert.Contains(name, registrationError.Message);

        var valid = new UnformattableValue(true);
        var property = Property.Register<ValueOwner, UnformattableValue>(
            name, valid, validate: static value => value.IsValid);
        var owner = new ValueOwner();

        var writeError = Assert.Throws<ArgumentException>(() => owner.SetValue(property, invalid));

        Assert.Equal("value", writeError.ParamName);
        Assert.Contains(name, writeError.Message);
        Assert.Same(valid, owner.GetValue(property));
    }

    [Fact]
    public void CustomValidationMessageIsUsedForDefaultAndLocalRejection()
    {
        const string message = "Value must be non-negative.";
        var name = $"Value_{Guid.NewGuid():N}";
        var registrationError = Assert.Throws<ArgumentException>(() => Property.Register<ValueOwner, int>(
            name, -1, validate: static value => value >= 0, validationMessage: message));
        Assert.Equal("defaultValue", registrationError.ParamName);
        Assert.StartsWith(message, registrationError.Message);

        var property = Property.Register<ValueOwner, int>(
            name, validate: static value => value >= 0, validationMessage: message);
        var owner = new ValueOwner();

        var writeError = Assert.Throws<ArgumentException>(() => owner.SetValue(property, -1));

        Assert.Equal("value", writeError.ParamName);
        Assert.StartsWith(message, writeError.Message);
        Assert.Equal(0, owner.GetValue(property));
    }

    [Fact]
    public void ReadOnlyAndAttachedValidationMessagesAreUsed()
    {
        const string message = "A positive value is required.";
        var readOnlyError = Assert.Throws<ArgumentException>(() => Property.RegisterReadOnly<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", validate: static value => value > 0, validationMessage: message));
        Assert.StartsWith(message, readOnlyError.Message);
        Assert.Equal("defaultValue", readOnlyError.ParamName);
        var attachedError = Assert.Throws<ArgumentException>(() => Property.RegisterAttached<AttachedOwner, ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", validate: static value => value > 0, validationMessage: message));
        Assert.StartsWith(message, attachedError.Message);
        Assert.Equal("defaultValue", attachedError.ParamName);

        var owner = new ValueOwner();
        var key = Property.RegisterReadOnly<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", 1, validate: static value => value > 0, validationMessage: message);
        var keyWriteError = Assert.Throws<ArgumentException>(() => owner.SetKeyValue(key, 0));
        Assert.StartsWith(message, keyWriteError.Message);
        Assert.Equal("value", keyWriteError.ParamName);

        var attached = Property.RegisterAttached<AttachedOwner, ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", 1, validate: static value => value > 0, validationMessage: message);
        var attachedWriteError = Assert.Throws<ArgumentException>(() => owner.SetValue(attached, 0));
        Assert.StartsWith(message, attachedWriteError.Message);
        Assert.Equal("value", attachedWriteError.ParamName);
    }

    [Fact]
    public void InvalidLocalValueIsRejectedWithoutStorageOrNotification()
    {
        var owner = new ValueOwner();
        var notifications = 0;
        using var subscription = owner.Subscribe(ValueOwner.ValueProperty, (_, _) => notifications++);

        var error = Assert.Throws<ArgumentException>(() => owner.SetValue(ValueOwner.ValueProperty, -1));

        Assert.Equal("value", error.ParamName);
        Assert.False(owner.HasValue);
        Assert.Equal(0, owner.GetValue(ValueOwner.ValueProperty));
        Assert.Empty(owner.Changes);
        Assert.Equal(0, notifications);

        owner.SetValue(ValueOwner.ValueProperty, 5);
        Assert.Throws<ArgumentException>(() => owner.SetValue(ValueOwner.ValueProperty, -2));
        Assert.Equal(5, owner.GetValue(ValueOwner.ValueProperty));
        Assert.Equal([(0, 5)], owner.Changes);
        Assert.Equal(1, notifications);
        owner.ClearValue(ValueOwner.ValueProperty);
        Assert.Equal(0, owner.GetValue(ValueOwner.ValueProperty));
        Assert.False(owner.HasValue);
        Assert.Equal([(0, 5), (5, 0)], owner.Changes);
    }

    [Fact]
    public void ReadOnlyKeyWritesValidateAndClearRestoresTheDefault()
    {
        var owner = new ValueOwner();
        owner.SetReadOnlyValue(5);
        owner.Changes.Clear();
        var notifications = 0;
        owner.PropertyChanged += (_, _) => notifications++;
        using var subscription = owner.Subscribe(ValueOwner.ReadOnlyProperty, (_, _) => notifications++);

        Assert.Throws<ArgumentException>(() => owner.SetReadOnlyValue(-1));
        Assert.Equal(5, owner.GetValue(ValueOwner.ReadOnlyProperty));
        Assert.Empty(owner.Changes);
        Assert.Equal(0, notifications);

        owner.ClearReadOnlyValue();
        Assert.Equal(0, owner.GetValue(ValueOwner.ReadOnlyProperty));
        Assert.Equal([(5, 0)], owner.Changes);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void AttachedPropertyWritesAreValidatedOnTheTarget()
    {
        var target = new ValueOwner();
        target.SetValue(AttachedOwner.ValueProperty, 5);
        target.Changes.Clear();
        var notifications = 0;
        target.PropertyChanged += (_, _) => notifications++;
        using var subscription = target.Subscribe(AttachedOwner.ValueProperty, (_, _) => notifications++);

        Assert.Throws<ArgumentException>(() => target.SetValue(AttachedOwner.ValueProperty, -1));

        Assert.Equal(5, target.GetValue(AttachedOwner.ValueProperty));
        Assert.Empty(target.Changes);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void ValidatorExceptionOnWritePropagatesWithoutChangingTheValue()
    {
        var expected = new InvalidOperationException("validator");
        var property = Property.Register<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}",
            validate: value => value == 2 ? throw expected : true,
            validationMessage: "This message must not replace a callback exception.");
        var owner = new ValueOwner();
        owner.SetValue(property, 1);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => owner.SetValue(property, 2)));
        Assert.Equal(1, owner.GetValue(property));
        owner.SetValue(property, 3);
        Assert.Equal(3, owner.GetValue(property));
    }

    [Fact]
    public void EqualWriteIsValidatedBeforeEqualitySuppression()
    {
        var accept = true;
        var property = Property.Register<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", validate: _ => accept);
        var owner = new ValueOwner();
        owner.SetValue(property, 1);
        accept = false;

        Assert.Throws<ArgumentException>(() => owner.SetValue(property, 1));
        Assert.Equal(1, owner.GetValue(property));
    }

    [Fact]
    public void ReadsAndClearDoNotRepeatValidation()
    {
        var calls = 0;
        var property = Property.Register<ValueOwner, int>(
            $"Value_{Guid.NewGuid():N}", validate: _ => { calls++; return true; });
        var owner = new ValueOwner();
        Assert.Equal(1, calls);
        Assert.Equal(0, owner.GetValue(property));
        owner.SetValue(property, 5);
        owner.ClearValue(property);
        Assert.Equal(0, owner.GetValue(property));
        Assert.Equal(2, calls);
    }

    private sealed class ValueOwner : ObservableObject
    {
        internal static readonly Property<int> ValueProperty = Property.Register<ValueOwner, int>(
            "Value",
            onChanged: static (owner, oldValue, newValue) => owner.Changes.Add((oldValue, newValue)),
            validate: static value => value >= 0);

        private static readonly PropertyKey<int> ReadOnlyKey = Property.RegisterReadOnly<ValueOwner, int>(
            "ReadOnly",
            onChanged: static (owner, oldValue, newValue) => owner.Changes.Add((oldValue, newValue)),
            validate: static value => value >= 0);

        internal static Property<int> ReadOnlyProperty => ReadOnlyKey.Property;

        internal List<(int, int)> Changes { get; } = [];

        internal bool HasValue => HasLocalValue(ValueProperty);

        internal void SetReadOnlyValue(int value) => SetValue(ReadOnlyKey, value);

        internal void SetKeyValue(PropertyKey<int> key, int value) => SetValue(key, value);

        internal void ClearReadOnlyValue() => ClearValue(ReadOnlyKey);
    }

    private sealed class AttachedOwner : ObservableObject
    {
        internal static readonly Property<int> ValueProperty = Property.RegisterAttached<AttachedOwner, ValueOwner, int>(
            "Value",
            onChanged: static (owner, oldValue, newValue) => owner.Changes.Add((oldValue, newValue)),
            validate: static value => value >= 0);
    }

    private sealed class UnformattableValue(bool isValid)
    {
        internal bool IsValid { get; } = isValid;

        public override string ToString() => throw new InvalidOperationException("formatting");
    }

    private sealed class DefaultOwner : ObservableObject
    {
    }

    private sealed class DuplicateOwner : ObservableObject
    {
    }

    private sealed class LegacyOwner : ObservableObject
    {
    }
}
