using System.ComponentModel;
using System.Runtime.CompilerServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiBindingTests
{
    [Fact]
    public void OneWayDirectBindingSynchronizesInitialAndNamedChanges()
    {
        var model = new Model { Value = 3 };
        var node = new TestNode();

        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        Assert.Equal(3, node.Value);
        model.Value = 4;
        Assert.Equal(4, node.Value);
        model.Raise(nameof(Model.Other));
        Assert.Equal(4, node.Value);
    }

    [Fact]
    public void DirectBindingRespondsToEmptyAndNullPropertyNames()
    {
        var model = new Model { Value = 3 };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        model.SetValueWithoutNotification(4);
        model.Raise(string.Empty);
        Assert.Equal(4, node.Value);
        model.SetValueWithoutNotification(5);
        model.Raise(null);

        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void OneWayBindingStripsTopLevelConvertForNotificationFiltering()
    {
        var model = new Model { Value = 3 };
        var node = new TestNode();
        node.Bind(TestNode.ObjectProperty, model, value => (object?)value.Value);

        model.SetValueWithoutNotification(4);
        model.Raise(nameof(Model.Other));
        Assert.Equal(3, node.ObjectValue);
        model.Raise(nameof(Model.Value));
        Assert.Equal(4, node.ObjectValue);
    }

    [Fact]
    public void ComputedBindingRespondsToAnyRootNotification()
    {
        var model = new Model { Value = 2, Other = 3 };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, model, value => value.Value + value.Other);

        Assert.Equal(5, node.Value);
        model.Other = 4;
        Assert.Equal(6, node.Value);
        model.Raise(nameof(Model.Unrelated));
        Assert.Equal(6, node.Value);
    }

    [Fact]
    public void ComputedBindingDoesNotTrackNestedObjectNotifications()
    {
        var model = new Model { Nested = new NestedModel { Value = 2 } };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, model, value => value.Nested.Value);

        model.Nested.Value = 5;

        Assert.Equal(2, node.Value);
        model.Raise(nameof(Model.Nested));
        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void DuplicateBindingFailsBeforeReadingNewSource()
    {
        var first = new Model { Value = 2 };
        var second = new Model { Value = 3, ThrowOnValueRead = true };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, first, value => value.Value);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            node.Bind(TestNode.ValueProperty, second, value => value.Value));

        Assert.Contains("already bound", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void ReentrantBindingDetachesTheUncommittedOuterBinding()
    {
        var outerSource = new Model { Value = 2 };
        var innerSource = new Model { Value = 3 };
        var node = new TestNode();
        outerSource.OnValueRead = () =>
        {
            outerSource.OnValueRead = null;
            node.Bind(TestNode.ValueProperty, innerSource, value => value.Value);
        };

        Assert.Throws<InvalidOperationException>(() =>
            node.Bind(TestNode.ValueProperty, outerSource, value => value.Value));

        Assert.Equal(3, node.Value);
        Assert.True(node.IsBound(TestNode.ValueProperty));
        innerSource.Value = 4;
        Assert.Equal(4, node.Value);
    }

    [Fact]
    public void OneWayTargetRejectsDirectAssignment()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        Assert.Throws<InvalidOperationException>(() => node.Value = 3);
        Assert.Equal(2, node.Value);
        Assert.Equal(2, model.Value);
    }

    [Fact]
    public void EqualInitialValueStillCreatesBindingWithoutNotification()
    {
        var model = new Model { Value = 10 };
        var node = new TestNode();
        var notifications = 0;
        node.Subscribe(TestNode.ValueProperty, (_, _) => notifications++);

        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        Assert.True(node.IsBound(TestNode.ValueProperty));
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void TwoWayDirectBindingSynchronizesBothDirections()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.BindTwoWay(TestNode.ValueProperty, model, value => value.Value);

        model.Value = 3;
        Assert.Equal(3, node.Value);
        node.Value = 4;
        Assert.Equal(4, model.Value);
        node.Value = 4;
        Assert.Equal(4, model.Value);
    }

    [Fact]
    public void TwoWayBindingAcceptsSourceNormalization()
    {
        var model = new Model { Normalized = 2 };
        var node = new TestNode();
        node.BindTwoWay(TestNode.ValueProperty, model, value => value.Normalized);

        node.Value = 20;

        Assert.Equal(10, model.Normalized);
        Assert.Equal(10, node.Value);
    }

    [Fact]
    public void ConvertedBindingSynchronizesForwardAndBackward()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.BindTwoWay(
            TestNode.TextProperty,
            model,
            value => value.Value,
            value => value.ToString(),
            static (string value, out int result) => int.TryParse(value, out result));

        Assert.Equal("2", node.Text);
        node.Text = "7";
        Assert.Equal(7, model.Value);
        model.Value = 8;
        Assert.Equal("8", node.Text);
    }

    [Fact]
    public void FailedReverseConversionKeepsUiValueAndBinding()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.BindTwoWay(
            TestNode.TextProperty,
            model,
            value => value.Value,
            value => value.ToString(),
            static (string value, out int result) =>
            {
                result = 0;
                return int.TryParse(value, out result);
            });

        node.Text = "bad";

        Assert.Equal("bad", node.Text);
        Assert.Equal(2, model.Value);
        Assert.True(node.IsBound(TestNode.TextProperty));
    }

    [Fact]
    public void BindingExceptionsPropagateWithoutRemovingBinding()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.Bind(
            TestNode.ValueProperty,
            model,
            value => value.Value,
            static value => value == 3
                ? throw new InvalidOperationException("conversion failed")
                : value);

        var exception = Assert.Throws<InvalidOperationException>(() => model.Value = 3);

        Assert.Equal("conversion failed", exception.Message);
        Assert.Equal(2, node.Value);
        Assert.True(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void SourceSetterExceptionLeavesUpdatedUiValueAndBinding()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.BindTwoWay(TestNode.ValueProperty, model, value => value.Value);
        model.ThrowOnValueWrite = true;

        var exception = Assert.Throws<InvalidOperationException>(() => node.Value = 3);

        Assert.Equal("source write failed", exception.Message);
        Assert.Equal(3, node.Value);
        Assert.True(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void TargetNotificationExceptionSkipsCurrentWriteback()
    {
        var model = new Model { Value = 2 };
        var node = new ThrowingNode();
        node.BindTwoWay(TestNode.ValueProperty, model, value => value.Value);
        node.ThrowNotifications = true;

        var exception = Assert.Throws<InvalidOperationException>(() => node.Value = 3);

        Assert.Equal("target notification failed", exception.Message);
        Assert.Equal(3, node.Value);
        Assert.Equal(2, model.Value);
        Assert.True(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void UnbindKeepsSynchronizedValueInsteadOfOldLocalValue()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.Value = 1;
        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        node.Unbind(TestNode.ValueProperty);
        model.Value = 3;

        Assert.Equal(2, node.Value);
        Assert.False(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void UnboundPropertyCanBindAgain()
    {
        var first = new Model { Value = 2 };
        var second = new Model { Value = 3 };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, first, value => value.Value);
        node.Unbind(TestNode.ValueProperty);

        node.Bind(TestNode.ValueProperty, second, value => value.Value);

        Assert.Equal(3, node.Value);
        Assert.True(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void ClearValueUnbindsAndRestoresDefault()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();
        node.Bind(TestNode.ValueProperty, model, value => value.Value);

        node.ClearValue(TestNode.ValueProperty);
        model.Value = 3;

        Assert.Equal(10, node.Value);
        Assert.False(node.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void TwoWayExpressionRejectsUnsupportedShapes()
    {
        var model = new Model { Value = 2 };
        var node = new TestNode();

        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value.Value + 1));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value.Field));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value[0]));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value.ReadOnly));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value.PrivateSetter));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ValueProperty, model, value => value.Nested.Value));
        Assert.Throws<ArgumentException>(() =>
            node.BindTwoWay(TestNode.ObjectProperty, model, value => (object?)value.Value));
    }

    [Fact]
    public void BindingManagementRejectsForeignOwnerProperties()
    {
        var model = new Model { Value = 2 };
        var foreign = new ForeignNode();

        Assert.Throws<ArgumentException>(() => foreign.IsBound(TestNode.ValueProperty));
        Assert.Throws<ArgumentException>(() => foreign.Unbind(TestNode.ValueProperty));
        Assert.Throws<ArgumentException>(() =>
            foreign.Bind(TestNode.ValueProperty, model, value => value.Value));
    }

    [Fact]
    public void UiPropertyBindingsSynchronizeAndHonorSourceProtection()
    {
        var source = new TestNode { Value = 2 };
        var target = new TestNode();
        target.Bind(TestNode.ValueProperty, source, TestNode.ValueProperty);

        source.Value = 3;
        Assert.Equal(3, target.Value);

        var twoWayTarget = new TestNode();
        twoWayTarget.BindTwoWay(TestNode.ValueProperty, source, TestNode.ValueProperty);
        twoWayTarget.Value = 4;
        Assert.Equal(4, source.Value);
    }

    [Fact]
    public void ConvertedUiPropertyBindingsSynchronizeBothDirections()
    {
        var source = new TestNode { Value = 2 };
        var oneWayTarget = new TestNode();
        oneWayTarget.Bind(
            TestNode.TextProperty,
            source,
            TestNode.ValueProperty,
            value => value.ToString());

        source.Value = 3;
        Assert.Equal("3", oneWayTarget.Text);

        var twoWayTarget = new TestNode();
        twoWayTarget.BindTwoWay(
            TestNode.TextProperty,
            source,
            TestNode.ValueProperty,
            value => value.ToString(),
            static (string value, out int result) => int.TryParse(value, out result));
        twoWayTarget.Text = "4";

        Assert.Equal(4, source.Value);
        Assert.Equal("4", twoWayTarget.Text);
    }

    [Fact]
    public void UiTwoWaySourceUsesItsPublicSetValueProtection()
    {
        var model = new Model { Value = 2 };
        var source = new TestNode();
        source.Bind(TestNode.ValueProperty, model, value => value.Value);
        var target = new TestNode();
        target.BindTwoWay(TestNode.ValueProperty, source, TestNode.ValueProperty);

        Assert.Throws<InvalidOperationException>(() => target.Value = 3);
        Assert.Equal(2, model.Value);
    }

    [Fact]
    public void StaleSourceCallbackCannotRestoreAnUnboundTarget()
    {
        var source = new TestNode { Value = 2 };
        var target = new TestNode();
        target.Bind(TestNode.ValueProperty, source, TestNode.ValueProperty);
        source.PropertyChanged += (_, _) => target.Unbind(TestNode.ValueProperty);

        source.Value = 3;

        Assert.Equal(2, target.Value);
        Assert.False(target.IsBound(TestNode.ValueProperty));
    }

    [Fact]
    public void MutualUiBindingsTerminateAtEqualValues()
    {
        var first = new TestNode { Value = 1 };
        var second = new TestNode { Value = 1 };
        first.BindTwoWay(TestNode.ValueProperty, second, TestNode.ValueProperty);
        second.BindTwoWay(TestNode.ValueProperty, first, TestNode.ValueProperty);

        first.Value = 4;

        Assert.Equal(4, first.Value);
        Assert.Equal(4, second.Value);
    }

    [Fact]
    public void BindingStronglyHoldsSourceWhileTargetIsAlive()
    {
        var weakSource = CreateTargetWithSource(out var target);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.True(weakSource.TryGetTarget(out _));
        GC.KeepAlive(target);
    }

    [Fact]
    public void LongLivedSourceDoesNotKeepTargetAlive()
    {
        var (weakTarget, source) = CreateTargetWithExternalSource();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        source.Value = 4;

        Assert.False(weakTarget.TryGetTarget(out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Model> CreateTargetWithSource(out TestNode target)
    {
        var source = new Model { Value = 2 };
        target = new TestNode();
        target.Bind(TestNode.ValueProperty, source, value => value.Value);
        return new WeakReference<Model>(source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<TestNode> Target, Model Source) CreateTargetWithExternalSource()
    {
        var source = new Model { Value = 2 };
        var target = new TestNode();
        target.Bind(TestNode.ValueProperty, source, value => value.Value);
        return (new WeakReference<TestNode>(target), source);
    }

    private class TestNode : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<TestNode, int>(nameof(Value), 10);

        internal static readonly UiProperty<string> TextProperty =
            UiProperty.Register<TestNode, string>(nameof(Text), string.Empty);

        internal static readonly UiProperty<object?> ObjectProperty =
            UiProperty.Register<TestNode, object?>(nameof(ObjectValue));

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

        internal object? ObjectValue
        {
            get => GetValue(ObjectProperty);
            set => SetValue(ObjectProperty, value);
        }
    }

    private sealed class ThrowingNode : TestNode
    {
        internal bool ThrowNotifications { get; set; }

        protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
        {
            base.OnPropertyChanged(eventArgs);
            if (ThrowNotifications)
                throw new InvalidOperationException("target notification failed");
        }
    }

    private sealed class ForeignNode : UiNode
    {
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private int _value;
        private int _other;
        private int _normalized;
        private int _privateSetter;
        private NestedModel _nested = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool ThrowOnValueRead { get; set; }

        public bool ThrowOnValueWrite { get; set; }

        public Action? OnValueRead { get; set; }

        public int Value
        {
            get
            {
                if (ThrowOnValueRead)
                    throw new InvalidOperationException("source read failed");
                OnValueRead?.Invoke();
                return _value;
            }
            set
            {
                if (ThrowOnValueWrite)
                    throw new InvalidOperationException("source write failed");
                if (_value == value)
                    return;
                _value = value;
                Raise(nameof(Value));
            }
        }

        public int Other
        {
            get => _other;
            set
            {
                if (_other == value)
                    return;
                _other = value;
                Raise(nameof(Other));
            }
        }

        public int Normalized
        {
            get => _normalized;
            set
            {
                _normalized = Math.Clamp(value, 0, 10);
                Raise(nameof(Normalized));
            }
        }

        public int ReadOnly => _value;

        public int PrivateSetter
        {
            get => _privateSetter;
            private set => _privateSetter = value;
        }

        public int Field;

        public NestedModel Nested
        {
            get => _nested;
            set
            {
                _nested = value;
                Raise(nameof(Nested));
            }
        }

        public int this[int index] => _value + index;

        public void Raise(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void SetValueWithoutNotification(int value) =>
            _value = value;

        public const string Unrelated = nameof(Unrelated);
    }

    private sealed class NestedModel : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}