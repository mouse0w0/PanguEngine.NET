using System.ComponentModel;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiPropertyKeyTests
{
    [Fact]
    public void ReadOnlyRegistrationPreservesMetadataAndKeyIdentity()
    {
        var property = ReadOnlyNode.ValueProperty;

        Assert.Equal(nameof(ReadOnlyNode.Value), property.Name);
        Assert.Equal(typeof(ReadOnlyNode), property.OwnerType);
        Assert.Equal(typeof(ReadOnlyNode), property.TargetType);
        Assert.Equal(typeof(int), property.ValueType);
        Assert.Equal(5, property.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Render, property.Invalidation);
        Assert.True(property.IsReadOnly);
        Assert.Same(property, ReadOnlyNode.ValueProperty);
        Assert.Empty(typeof(UiPropertyKey<int>).GetConstructors());
    }

    [Fact]
    public void ExistingRegistrationKindsRemainWritable()
    {
        var node = new WritableNode();
        var attachedTarget = new AttachedTarget();

        Assert.False(WritableNode.ValueProperty.IsReadOnly);
        Assert.False(AttachedOwner.ValueProperty.IsReadOnly);

        node.Value = 9;
        attachedTarget.SetValue(AttachedOwner.ValueProperty, 11);

        Assert.Equal(9, node.Value);
        Assert.Equal(11, attachedTarget.GetValue(AttachedOwner.ValueProperty));
    }

    [Fact]
    public void OwnerKeyWritesClearAndNotifyEffectiveChanges()
    {
        var node = new ReadOnlyNode();
        var globalChanges = new List<(int OldValue, int NewValue)>();
        var subscribedChanges = new List<(int OldValue, int NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            var typedArgs = Assert.IsType<UiPropertyChangedEventArgs<int>>(args);
            globalChanges.Add((typedArgs.OldValue, typedArgs.NewValue));
        };
        var subscription = node.Subscribe(
            ReadOnlyNode.ValueProperty,
            (_, args) => subscribedChanges.Add((args.OldValue, args.NewValue)));

        node.SetValueFromOwner(9);
        node.SetValueFromOwner(9);
        node.ClearValueFromOwner();
        subscription.Dispose();
        node.SetValueFromOwner(12);

        Assert.Equal(12, node.Value);
        Assert.Equal([(5, 9), (9, 5), (5, 12)], globalChanges);
        Assert.Equal([(5, 9), (9, 5)], subscribedChanges);
    }

    [Fact]
    public void KeyWritesRejectNullAndForeignOwnersBeforeMutation()
    {
        var node = new ReadOnlyNode();

        Assert.Throws<ArgumentNullException>(() => node.SetArbitraryKey(null!, 9));
        Assert.Throws<ArgumentNullException>(() => node.ClearArbitraryKey(null!));
        Assert.Throws<ArgumentException>(() =>
            node.SetArbitraryKey(ForeignReadOnlyNode.ValuePropertyKey, 9));
        Assert.Throws<ArgumentException>(() =>
            node.ClearArbitraryKey(ForeignReadOnlyNode.ValuePropertyKey));

        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void KeyWritesOnOpenScreenRejectWrongThreadBeforeMutation()
    {
        var node = new ReadOnlyNode();
        var screen = new UiScreen(node);
        screen.Open();
        Exception? setError = null;
        Exception? clearError = null;
        var thread = new Thread(() =>
        {
            setError = Record.Exception(() => node.SetValueFromOwner(9));
            clearError = Record.Exception(node.ClearValueFromOwner);
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(setError);
        Assert.IsType<InvalidOperationException>(clearError);
        Assert.Equal(5, node.Value);
        screen.Close();
    }

    [Fact]
    public void PublicWritesRejectReadOnlyPropertyWithoutSideEffects()
    {
        var node = new ReadOnlyNode();
        var notifications = 0;
        node.PropertyChanged += (_, _) => notifications++;

        var setError = Assert.Throws<InvalidOperationException>(() =>
            node.SetValue(ReadOnlyNode.ValueProperty, 9));
        var clearError = Assert.Throws<InvalidOperationException>(() =>
            node.ClearValue(ReadOnlyNode.ValueProperty));

        Assert.Equal("Property 'Value' is read-only.", setError.Message);
        Assert.Equal("Property 'Value' is read-only.", clearError.Message);
        Assert.Equal(5, node.Value);
        Assert.False(node.IsBound(ReadOnlyNode.ValueProperty));
        Assert.Equal(0, notifications);

        node.Unbind(ReadOnlyNode.ValueProperty);
        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void PublicWriteReportsOwnerMismatchBeforeReadOnlyProtection()
    {
        var node = new ForeignNode();

        Assert.Throws<ArgumentException>(() =>
            node.SetValue(ReadOnlyNode.ValueProperty, 9));
        Assert.Throws<ArgumentException>(() =>
            node.ClearValue(ReadOnlyNode.ValueProperty));
    }

    [Fact]
    public void DirectBindingsRejectReadOnlyTargetsBeforeReadingOrSubscribing()
    {
        var node = new ReadOnlyNode();
        var source = new ValueSource { Value = 9 };

        AssertReadOnlyError(() =>
            node.Bind(ReadOnlyNode.ValueProperty, source, item => item.Value));
        AssertReadOnlyError(() =>
            node.Bind(ReadOnlyNode.ValueProperty, source, item => item.Value, value => value));
        AssertReadOnlyError(() =>
            node.BindTwoWay(ReadOnlyNode.ValueProperty, source, item => item.Value));
        AssertReadOnlyError(() =>
            node.BindTwoWay(
                ReadOnlyNode.ValueProperty,
                source,
                item => item.Value,
                value => value,
                static (int value, out int result) =>
                {
                    result = value;
                    return true;
                }));

        Assert.Equal(5, node.Value);
        Assert.False(node.IsBound(ReadOnlyNode.ValueProperty));
        Assert.Equal(0, source.ValueReads);
        Assert.Equal(0, source.SubscriptionCount);
    }

    [Fact]
    public void UiPropertyBindingsRejectReadOnlyTargetsBeforeReadingSource()
    {
        var node = new ReadOnlyNode();
        var source = new WritableNode { Value = 9 };

        AssertReadOnlyError(() =>
            node.Bind(ReadOnlyNode.ValueProperty, source, WritableNode.ValueProperty));
        AssertReadOnlyError(() =>
            node.Bind(
                ReadOnlyNode.ValueProperty,
                source,
                WritableNode.ValueProperty,
                value => value));
        AssertReadOnlyError(() =>
            node.BindTwoWay(ReadOnlyNode.ValueProperty, source, WritableNode.ValueProperty));
        AssertReadOnlyError(() =>
            node.BindTwoWay(
                ReadOnlyNode.ValueProperty,
                source,
                WritableNode.ValueProperty,
                value => value,
                static (int value, out int result) =>
                {
                    result = value;
                    return true;
                }));

        Assert.Equal(5, node.Value);
        Assert.False(node.IsBound(ReadOnlyNode.ValueProperty));
    }

    [Fact]
    public void ReadOnlyUiPropertySupportsOneWayBindings()
    {
        var source = new ReadOnlyNode();
        var sameTypeTarget = new WritableNode();
        var convertedTarget = new WritableNode();

        sameTypeTarget.Bind(
            WritableNode.ValueProperty,
            source,
            ReadOnlyNode.ValueProperty);
        convertedTarget.Bind(
            WritableNode.TextProperty,
            source,
            ReadOnlyNode.ValueProperty,
            value => value.ToString());

        source.SetValueFromOwner(9);

        Assert.Equal(9, sameTypeTarget.Value);
        Assert.Equal("9", convertedTarget.Text);
    }

    [Fact]
    public void TwoWayBindingsRejectReadOnlyUiSourcesBeforeChangingTarget()
    {
        var source = new ReadOnlyNode();
        var sameTypeTarget = new WritableNode { Value = 9 };
        var convertedTarget = new WritableNode { Text = "local" };

        AssertReadOnlyError(() =>
            sameTypeTarget.BindTwoWay(
                WritableNode.ValueProperty,
                source,
                ReadOnlyNode.ValueProperty));
        AssertReadOnlyError(() =>
            convertedTarget.BindTwoWay(
                WritableNode.TextProperty,
                source,
                ReadOnlyNode.ValueProperty,
                value => value.ToString(),
                static (string value, out int result) => int.TryParse(value, out result)));

        Assert.Equal(9, sameTypeTarget.Value);
        Assert.Equal("local", convertedTarget.Text);
        Assert.False(sameTypeTarget.IsBound(WritableNode.ValueProperty));
        Assert.False(convertedTarget.IsBound(WritableNode.TextProperty));
    }

    [Fact]
    public void TwoWayReadOnlySourceReportsOwnerMismatchFirst()
    {
        var target = new WritableNode { Value = 9 };
        var source = new ForeignNode();

        Assert.Throws<ArgumentException>(() =>
            target.BindTwoWay(
                WritableNode.ValueProperty,
                source,
                ReadOnlyNode.ValueProperty));

        Assert.Equal(9, target.Value);
        Assert.False(target.IsBound(WritableNode.ValueProperty));
    }

    private static void AssertReadOnlyError(Action action)
    {
        var error = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Property 'Value' is read-only.", error.Message);
    }

    private class ReadOnlyNode : UiNode
    {
        private static readonly UiPropertyKey<int> ValuePropertyKey =
            UiProperty.RegisterReadOnly<ReadOnlyNode, int>(
                nameof(Value),
                5,
                UiPropertyInvalidation.Render);

        internal static UiProperty<int> ValueProperty => ValuePropertyKey.Property;
        internal int Value => GetValue(ValueProperty);
        internal void SetValueFromOwner(int value) => SetValue(ValuePropertyKey, value);
        internal void ClearValueFromOwner() => ClearValue(ValuePropertyKey);
        internal void SetArbitraryKey(UiPropertyKey<int> propertyKey, int value) =>
            SetValue(propertyKey, value);
        internal void ClearArbitraryKey(UiPropertyKey<int> propertyKey) =>
            ClearValue(propertyKey);
    }

    private sealed class WritableNode : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<WritableNode, int>(nameof(Value));

        internal static readonly UiProperty<string> TextProperty =
            UiProperty.Register<WritableNode, string>(nameof(Text), string.Empty);

        internal int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        internal string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }

    private sealed class ForeignReadOnlyNode : UiNode
    {
        internal static readonly UiPropertyKey<int> ValuePropertyKey =
            UiProperty.RegisterReadOnly<ForeignReadOnlyNode, int>("Value");
    }

    private sealed class ForeignNode : UiNode
    {
    }

    private sealed class AttachedOwner : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.RegisterAttached<AttachedOwner, AttachedTarget, int>("Value");
    }

    private sealed class AttachedTarget : UiNode
    {
    }

    private sealed class ValueSource : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                SubscriptionCount++;
            }
            remove
            {
                _propertyChanged -= value;
                SubscriptionCount--;
            }
        }

        internal int SubscriptionCount { get; private set; }
        internal int ValueReads { get; private set; }

        internal int Value
        {
            get
            {
                ValueReads++;
                return _value;
            }
            set
            {
                if (_value == value)
                    return;
                _value = value;
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}
