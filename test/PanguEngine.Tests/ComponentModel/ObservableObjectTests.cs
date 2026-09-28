using System.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;
using PropertyChangedEventArgs = PanguEngine.ComponentModel.PropertyChangedEventArgs;

namespace PanguEngine.Tests.ComponentModel;

public sealed class ObservableObjectTests
{
    [Fact]
    public void ValuesNotifyOnlyEffectiveChangesInOrder()
    {
        var source = new Model();
        var changes = new List<(int Old, int New)>();
        source.PropertyChanged += (sender, args) =>
        {
            Assert.Same(source, sender);
            source.Trace.Add("event");
        };
        using var subscription = source.Subscribe(Model.ValueProperty, (sender, args) =>
        {
            Assert.Same(source, sender);
            Assert.Same(Model.ValueProperty, args.Property);
            changes.Add((args.OldValue, args.NewValue));
            source.Trace.Add("subscription");
        });

        Assert.Equal(3, source.Value);
        source.Value = 3;
        Assert.Empty(source.Trace);
        source.Value = 7;
        Assert.Equal(new[] { "callback", "virtual", "event", "subscription" }, source.Trace);
        source.Trace.Clear();
        source.SetReadOnly(4);
        Assert.Equal(new[] { (3, 7) }, changes);
        Assert.Equal(new[] { "virtual", "event" }, source.Trace);
        source.Trace.Clear();
        source.Value = 7;
        Assert.Empty(source.Trace);
        source.ClearValue(Model.ValueProperty);

        Assert.Equal(3, source.Value);
        Assert.Equal(new[] { (3, 7), (7, 3) }, changes);
    }

    [Fact]
    public void ReadOnlyKeyCanWriteAndClearWhilePublicWritesAreRejected()
    {
        var source = new Model();
        Assert.Throws<InvalidOperationException>(() => source.SetValue(Model.ReadOnlyProperty, 9));
        Assert.Throws<InvalidOperationException>(() => source.ClearValue(Model.ReadOnlyProperty));

        source.SetReadOnly(9);
        Assert.Equal(9, source.GetValue(Model.ReadOnlyProperty));
        source.ClearReadOnly();
        Assert.Equal(0, source.GetValue(Model.ReadOnlyProperty));
    }

    [Fact]
    public void RegisteredAndAttachedPropertiesEnforceTheirTargetTypes()
    {
        var source = new Model();
        var target = new OtherModel();
        Assert.Throws<ArgumentException>(() => target.GetValue(Model.ValueProperty));
        Assert.Throws<ArgumentException>(() => target.SetValue(Model.ValueProperty, 1));
        target.SetValue(Model.AttachedProperty, 5);
        Assert.Equal(5, target.GetValue(Model.AttachedProperty));
        Assert.Equal((0, 5), target.LastChange);
        Assert.Throws<ArgumentException>(() => source.GetValue(Model.AttachedProperty));
    }

    [Fact]
    public void CallbackFailurePreservesCommittedValueAndStopsNotification()
    {
        var source = new Model { FailCallback = true };
        source.PropertyChanged += (_, _) => source.Trace.Add("event");
        using var subscription = source.Subscribe(Model.ValueProperty, (_, _) => source.Trace.Add("subscription"));

        Assert.Throws<InvalidOperationException>(() => source.Value = 8);

        Assert.Equal(8, source.Value);
        Assert.Equal(new[] { "callback" }, source.Trace);
    }

    [Fact]
    public void FallbackAndWriteAccessHooksApplyToPlainObjects()
    {
        var source = new FallbackModel();
        Assert.Equal(11, source.Value);
        source.Value = 11;
        source.Fallback = 15;
        Assert.Equal(11, source.Value);
        source.ClearValue(Model.ValueProperty);
        Assert.Equal(15, source.Value);
        source.RejectWrites = true;

        Assert.Throws<InvalidOperationException>(() => source.Value = 20);
        Assert.Equal(15, source.Value);
    }

    [Fact]
    public void ReadOnlyPropertiesUsePlainObjectFallbackAfterClearingLocalValue()
    {
        var source = new FallbackModel();
        Assert.Equal(11, source.GetValue(Model.ReadOnlyProperty));
        source.SetReadOnly(7);
        source.Fallback = 15;
        Assert.Equal(7, source.GetValue(Model.ReadOnlyProperty));
        source.ClearReadOnly();
        Assert.Equal(15, source.GetValue(Model.ReadOnlyProperty));
    }

    [Fact]
    public void OneWayBindingAllowsWritesAndUnbindPreservesLastValue()
    {
        var source = new Model { Value = 6 };
        var target = new Model();
        target.Bind(Model.ValueProperty, source, Model.ValueProperty);
        Assert.Equal(6, target.Value);
        target.Value = 9;
        Assert.Equal(9, target.Value);
        Assert.Equal(6, source.Value);
        source.Value = 8;
        Assert.Equal(8, target.Value);

        target.Unbind(Model.ValueProperty);
        source.Value = 10;
        Assert.False(target.IsBound(Model.ValueProperty));
        Assert.Equal(8, target.Value);
        target.ClearValue(Model.ValueProperty);
        Assert.Equal(3, target.Value);
    }

    [Fact]
    public void TwoWayBindingWritesBackAndClearDetaches()
    {
        var source = new Model();
        var target = new Model();
        target.BindTwoWay(Model.ValueProperty, source, Model.ValueProperty);
        target.Value = 9;
        Assert.Equal(9, source.Value);
        source.Value = 12;
        Assert.Equal(12, target.Value);

        target.ClearValue(Model.ValueProperty);
        source.Value = 15;
        Assert.False(target.IsBound(Model.ValueProperty));
        Assert.Equal(3, target.Value);
    }

    [Fact]
    public void NodesAndPlainObjectsCanBeBindingTargets()
    {
        var model = new Model { Value = 6 };
        var node = new ValueNode();
        node.BindTwoWay(ValueNode.ValueProperty, model, Model.ValueProperty);
        Assert.Equal(6, node.GetValue(ValueNode.ValueProperty));
        node.SetValue(ValueNode.ValueProperty, 9);
        Assert.Equal(9, model.Value);
        node.Unbind(ValueNode.ValueProperty);

        model.BindTwoWay(Model.ValueProperty, node, ValueNode.ValueProperty);
        node.SetValue(ValueNode.ValueProperty, 12);
        Assert.Equal(12, model.Value);
        model.Value = 15;
        Assert.Equal(15, node.GetValue(ValueNode.ValueProperty));
    }

    [Fact]
    public void ObservableObjectDoesNotImplementNotifyPropertyChanged()
    {
        Assert.False(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(ObservableObject)));
    }

    [Fact]
    public void PlainObjectBindingMasksStyleAndClearRestoresStyleNotification()
    {
        var source = new Model { Value = 7 };
        var node = new ValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<ValueNode>(), [UiStyleSetter.Create(ValueNode.ValueProperty, 20)])
        ])]);
        var changes = new List<(int Old, int New)>();
        using var subscription = node.Subscribe(ValueNode.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        node.Bind(ValueNode.ValueProperty, source, Model.ValueProperty);
        Assert.True(Assert.Single(node.GetStyleValueSources(ValueNode.ValueProperty)).IsMaskedByLocalValue);
        node.ClearValue(ValueNode.ValueProperty);

        Assert.Equal(20, node.GetValue(ValueNode.ValueProperty));
        Assert.False(node.IsBound(ValueNode.ValueProperty));
        Assert.False(Assert.Single(node.GetStyleValueSources(ValueNode.ValueProperty)).IsMaskedByLocalValue);
        Assert.Equal(new[] { (20, 7), (7, 20) }, changes);
    }

    private class Model : ObservableObject
    {
        internal static readonly Property<int> ValueProperty = Property.Register<Model, int>(
            "Value", 3, onChanged: static (owner, _, _) =>
            {
                owner.Trace.Add("callback");
                if (owner.FailCallback)
                    throw new InvalidOperationException("callback");
            });
        private static readonly PropertyKey<int> ReadOnlyKey = Property.RegisterReadOnly<Model, int>("ReadOnly");
        internal static readonly Property<int> ReadOnlyProperty = ReadOnlyKey.Property;
        internal static readonly Property<int> AttachedProperty = Property.RegisterAttached<Model, OtherModel, int>(
            "Attached", onChanged: static (target, oldValue, newValue) => target.LastChange = (oldValue, newValue));

        internal List<string> Trace { get; } = [];
        internal bool FailCallback { get; init; }
        internal int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        internal void SetReadOnly(int value) => SetValue(ReadOnlyKey, value);
        internal void ClearReadOnly() => ClearValue(ReadOnlyKey);

        protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            Trace.Add("virtual");
            base.OnPropertyChanged(eventArgs);
        }
    }

    private sealed class OtherModel : ObservableObject
    {
        internal (int Old, int New) LastChange { get; set; }
    }

    private sealed class FallbackModel : Model
    {
        internal int Fallback { get; set; } = 11;
        internal bool RejectWrites { get; set; }

        protected override T GetFallbackValue<T>(Property<T> property) =>
            ReferenceEquals(property, ValueProperty) || ReferenceEquals(property, ReadOnlyProperty)
                ? (T)(object)Fallback
                : base.GetFallbackValue(property);

        protected override void VerifyMutationAccess()
        {
            if (RejectWrites)
                throw new InvalidOperationException("writes disabled");
        }
    }

    private sealed class ValueNode : UiNode
    {
        internal static readonly Property<int> ValueProperty = Property.Register<ValueNode, int>("Value");
    }
}
