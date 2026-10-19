using System.ComponentModel;
using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Styling;
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
    public void ReferencePropertiesUseReferenceEqualityAndRestoreTheDefaultReference()
    {
        var source = new ReferenceModel();
        var original = source.Value;
        var replacement = new EqualValue(7);
        var changes = new List<(EqualValue? OldValue, EqualValue? NewValue)>();
        using var subscription = source.Subscribe(ReferenceModel.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));

        source.Value = replacement;
        source.Value = replacement;
        source.ClearValue(ReferenceModel.ValueProperty);

        Assert.Same(original, source.Value);
        Assert.Equal(2, changes.Count);
        Assert.Same(original, changes[0].OldValue);
        Assert.Same(replacement, changes[0].NewValue);
        Assert.Same(replacement, changes[1].OldValue);
        Assert.Same(original, changes[1].NewValue);

        source.Value = null;
        source.ClearValue(ReferenceModel.ValueProperty);

        Assert.Equal(4, changes.Count);
        Assert.Null(changes[2].NewValue);
        Assert.Null(changes[3].OldValue);
        Assert.Same(original, changes[3].NewValue);
    }

    [Fact]
    public void EqualAssignmentsStoreTheRequestedReferenceWithoutNotifying()
    {
        var source = new ReferenceModel();
        var original = source.GetValue(ReferenceModel.ValueComparedProperty);
        var replacement = new EqualValue(7);
        var changes = 0;
        using var subscription = source.Subscribe(ReferenceModel.ValueComparedProperty, (_, _) => changes++);

        source.SetValue(ReferenceModel.ValueComparedProperty, replacement);
        Assert.Same(replacement, source.GetValue(ReferenceModel.ValueComparedProperty));
        var secondReplacement = new EqualValue(7);
        source.SetValue(ReferenceModel.ValueComparedProperty, secondReplacement);
        Assert.Same(secondReplacement, source.GetValue(ReferenceModel.ValueComparedProperty));
        source.ClearValue(ReferenceModel.ValueComparedProperty);

        Assert.Same(original, source.GetValue(ReferenceModel.ValueComparedProperty));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void StringPropertiesReplaceEqualInstancesWithoutNotifying()
    {
        var source = new ReferenceModel();
        var original = new string('a', 1);
        source.SetValue(ReferenceModel.TextProperty, original);
        var replacement = new string('a', 1);
        var changes = new List<string>();
        using var subscription = source.Subscribe(ReferenceModel.TextProperty,
            (_, change) => changes.Add(change.NewValue));

        source.SetValue(ReferenceModel.TextProperty, replacement);

        Assert.Same(replacement, source.GetValue(ReferenceModel.TextProperty));
        Assert.Empty(changes);
        source.ClearValue(ReferenceModel.TextProperty);
        Assert.Equal(string.Empty, Assert.Single(changes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectPropertiesUseReferenceEqualityForBoxedValuesAndStringsByDefault(bool useStrings)
    {
        object original = useStrings ? new string('a', 1) : 7;
        object replacement = useStrings ? new string('a', 1) : 7;
        var source = new ReferenceModel();
        source.SetValue(ReferenceModel.ObjectProperty, original);
        var changes = new List<PropertyChangedEventArgs<object?>>();
        using var subscription = source.Subscribe(ReferenceModel.ObjectProperty, (_, change) => changes.Add(change));

        source.SetValue(ReferenceModel.ObjectProperty, replacement);
        source.SetValue(ReferenceModel.ObjectProperty, replacement);

        var change = Assert.Single(changes);
        Assert.Same(original, change.OldValue);
        Assert.Same(replacement, change.NewValue);
        Assert.Same(replacement, source.GetValue(ReferenceModel.ObjectProperty));
    }

    [Fact]
    public void TwoWayBindingsSynchronizeEqualReferenceInstancesInBothDirections()
    {
        var source = new ReferenceModel();
        var target = new ReferenceModel();
        target.BindTwoWay(ReferenceModel.ReferenceValueProperty, source, ReferenceModel.ReferenceValueProperty);
        var sourceChanges = 0;
        var targetChanges = 0;
        using var sourceSubscription =
            source.Subscribe(ReferenceModel.ReferenceValueProperty, (_, _) => sourceChanges++);
        using var targetSubscription =
            target.Subscribe(ReferenceModel.ReferenceValueProperty, (_, _) => targetChanges++);
        var fromTarget = new EqualValue(7);

        target.ReferenceValue = fromTarget;

        Assert.Same(fromTarget, source.ReferenceValue);
        Assert.Same(fromTarget, target.ReferenceValue);
        var fromSource = new EqualValue(7);
        source.ReferenceValue = fromSource;
        Assert.Same(fromSource, source.ReferenceValue);
        Assert.Same(fromSource, target.ReferenceValue);
        Assert.Equal(2, sourceChanges);
        Assert.Equal(2, targetChanges);
    }

    [Fact]
    public void TwoWayWritebackStoresAnEqualSourceInstanceWithoutSourceNotification()
    {
        var source = new ReferenceModel();
        var target = new ReferenceModel();
        target.BindTwoWay(ReferenceModel.ReferenceValueProperty, source, ReferenceModel.ValueComparedProperty);
        var sourceChanges = 0;
        var targetChanges = 0;
        using var sourceSubscription =
            source.Subscribe(ReferenceModel.ValueComparedProperty, (_, _) => sourceChanges++);
        using var targetSubscription =
            target.Subscribe(ReferenceModel.ReferenceValueProperty, (_, _) => targetChanges++);
        var replacement = new EqualValue(7);

        target.ReferenceValue = replacement;

        Assert.Same(replacement, source.GetValue(ReferenceModel.ValueComparedProperty));
        Assert.Same(replacement, target.ReferenceValue);
        Assert.Equal(0, sourceChanges);
        Assert.Equal(1, targetChanges);
    }

    [Fact]
    public void ConvertedTwoWayWritebackUsesTheSourceReferenceComparer()
    {
        var source = new ReferenceModel();
        var original = source.ReferenceValue;
        var converted = original;
        var target = new ReferenceModel();
        target.BindTwoWay(ReferenceModel.ValueProperty, source, ReferenceModel.ReferenceValueProperty,
            value => value, (EqualValue? value, out EqualValue? result) =>
            {
                result = converted;
                return true;
            });
        var changes = 0;
        using var subscription = source.Subscribe(ReferenceModel.ReferenceValueProperty, (_, _) => changes++);

        converted = new EqualValue(7);
        target.Value = new EqualValue(8);

        Assert.NotSame(original, source.ReferenceValue);
        Assert.Same(source.ReferenceValue, target.Value);
        Assert.Equal(7, target.Value!.Number);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClrSourceSetterDecidesWhetherToAcceptAnEqualInstance(bool rejectEqualValues)
    {
        var source = new ClrReferenceModel { RejectEqualValues = rejectEqualValues };
        var original = source.Value;
        var target = new ReferenceModel();
        target.BindTwoWay(ReferenceModel.ReferenceValueProperty, source, model => model.Value);
        source.SetterCalls = 0;
        var replacement = new EqualValue(7);

        target.ReferenceValue = replacement;

        Assert.Same(rejectEqualValues ? original : replacement, source.Value);
        Assert.Same(replacement, target.ReferenceValue);
        Assert.Equal(1, source.SetterCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComparerFailurePreservesTheCommittedLocalValueWithoutNotifying(bool hasLocalValue)
    {
        var comparer = new FailingComparer();
        var property = Property.Register<Model, int>(Guid.NewGuid().ToString(), comparer: comparer);
        var source = new Model();
        if (hasLocalValue)
            source.SetValue(property, 7);
        var changes = 0;
        using var subscription = source.Subscribe(property, (_, _) => changes++);
        comparer.Fail = true;

        Assert.Throws<InvalidOperationException>(() => source.SetValue(property, 9));

        Assert.Equal(9, source.GetValue(property));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ReadOnlyAndAttachedPropertiesHonorTheirCustomComparers()
    {
        var source = new ReferenceModel();
        var node = new ReferenceNode();
        var readOnlyChanges = new List<string>();
        var attachedChanges = new List<string>();
        using var readOnlySubscription = source.Subscribe(ReferenceModel.ReadOnlyTextProperty,
            (_, change) => readOnlyChanges.Add(change.NewValue));
        using var attachedSubscription = node.Subscribe(ReferenceModel.AttachedTextProperty,
            (_, change) => attachedChanges.Add(change.NewValue));

        source.SetReadOnlyText("ALPHA");
        node.SetValue(ReferenceModel.AttachedTextProperty, "ALPHA");
        Assert.Equal("ALPHA", source.GetValue(ReferenceModel.ReadOnlyTextProperty));
        Assert.Equal("ALPHA", node.GetValue(ReferenceModel.AttachedTextProperty));
        source.ClearReadOnlyText();
        node.ClearValue(ReferenceModel.AttachedTextProperty);
        Assert.Empty(readOnlyChanges);
        Assert.Empty(attachedChanges);

        source.SetReadOnlyText("beta");
        node.SetValue(ReferenceModel.AttachedTextProperty, "beta");
        Assert.Equal("beta", Assert.Single(readOnlyChanges));
        Assert.Equal("beta", Assert.Single(attachedChanges));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleChangesUseThePropertyComparer(bool useReferenceComparer)
    {
        var original = new EqualValue(7);
        var replacement = new EqualValue(7);
        var node = new ReferenceNode();
        var property = useReferenceComparer ? ReferenceNode.ReferenceValueProperty : ReferenceNode.ValueProperty;
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<ReferenceNode>(), [StyleSetter.Create(property, original)])
            ])
        ]);
        scene.Root!.UpdateStyles();
        var changes = new List<(EqualValue? OldValue, EqualValue? NewValue)>();
        using var subscription = node.Subscribe(property,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));

        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<ReferenceNode>(), [StyleSetter.Create(property, replacement)])
            ])
        ]);
        scene.Root.UpdateStyles();

        Assert.Same(replacement, node.GetValue(property));
        if (useReferenceComparer)
        {
            var change = Assert.Single(changes);
            Assert.Same(original, change.OldValue);
            Assert.Same(replacement, change.NewValue);
        }
        else
            Assert.Empty(changes);
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
    public void WriteAccessPolicyAppliesToPlainObjects()
    {
        var source = new AccessControlledModel { Value = 11 };
        source.ClearValue(Model.ValueProperty);
        Assert.Equal(3, source.Value);
        source.RejectWrites = true;

        Assert.Throws<InvalidOperationException>(() => source.Value = 20);
        Assert.Equal(3, source.Value);
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
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<ValueNode>(), [StyleSetter.Create(ValueNode.ValueProperty, 20)])
            ])
        ]);
        scene.Root!.UpdateStyles();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualAssignmentMasksStyleUntilClearedWithoutNotifying(bool bind)
    {
        var node = new ValueNode();
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<ValueNode>(), [StyleSetter.Create(ValueNode.ValueProperty, 7)])
            ])
        ]);
        scene.Root!.UpdateStyles();
        var changes = new List<(int OldValue, int NewValue)>();
        using var subscription = node.Subscribe(ValueNode.ValueProperty,
            (_, change) => changes.Add((change.OldValue, change.NewValue)));

        if (bind)
            node.Bind(ValueNode.ValueProperty, new Model { Value = 7 }, Model.ValueProperty);
        else
            node.SetValue(ValueNode.ValueProperty, 7);

        Assert.True(Assert.Single(node.GetStyleValueSources(ValueNode.ValueProperty)).IsMaskedByLocalValue);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<ValueNode>(), [StyleSetter.Create(ValueNode.ValueProperty, 9)])
            ])
        ]);
        scene.Root.UpdateStyles();
        Assert.Equal(7, node.GetValue(ValueNode.ValueProperty));
        Assert.Empty(changes);

        node.ClearValue(ValueNode.ValueProperty);

        Assert.Equal(9, node.GetValue(ValueNode.ValueProperty));
        Assert.False(Assert.Single(node.GetStyleValueSources(ValueNode.ValueProperty)).IsMaskedByLocalValue);
        Assert.Equal((7, 9), Assert.Single(changes));
    }

    [Fact]
    public void ClearingTheLastLocalValueAllowsReentrantWritesToRecreateState()
    {
        var source = new Model { Value = 7 };
        using var subscription = source.Subscribe(Model.ValueProperty, (_, args) =>
        {
            if (args.NewValue == 3)
                source.Value = 20;
        });

        source.ClearValue(Model.ValueProperty);

        Assert.Equal(20, source.Value);
        subscription.Dispose();
        source.ClearValue(Model.ValueProperty);
        Assert.Equal(3, source.Value);
    }

    private sealed record EqualValue(int Number);

    private sealed class FailingComparer : IEqualityComparer<int>
    {
        internal bool Fail { get; set; }

        public bool Equals(int left, int right) =>
            Fail ? throw new InvalidOperationException("comparison failed") : left == right;

        public int GetHashCode(int value) => value.GetHashCode();
    }

    private sealed class ReferenceModel : ObservableObject
    {
        internal static readonly Property<EqualValue?> ValueProperty =
            Property.Register<ReferenceModel, EqualValue?>(nameof(Value), new EqualValue(7));

        internal static readonly Property<EqualValue?> ValueComparedProperty =
            Property.Register<ReferenceModel, EqualValue?>("ValueCompared", new EqualValue(7),
                comparer: EqualityComparer<EqualValue?>.Default);

        internal static readonly Property<EqualValue?> ReferenceValueProperty =
            Property.Register<ReferenceModel, EqualValue?>(nameof(ReferenceValue), new EqualValue(7),
                comparer: ReferenceEqualityComparer.Instance);

        internal static readonly Property<string> TextProperty =
            Property.Register<ReferenceModel, string>("Text", string.Empty);

        internal static readonly Property<object?>
            ObjectProperty = Property.Register<ReferenceModel, object?>("Object");

        private static readonly PropertyKey<string> ReadOnlyTextKey = Property.RegisterReadOnly<ReferenceModel, string>(
            "ReadOnlyText", "alpha", comparer: StringComparer.OrdinalIgnoreCase);

        internal static readonly Property<string> ReadOnlyTextProperty = ReadOnlyTextKey.Property;

        internal static readonly Property<string> AttachedTextProperty =
            Property.RegisterAttached<ReferenceModel, ReferenceNode, string>(
                "AttachedText", "alpha", comparer: StringComparer.OrdinalIgnoreCase);

        internal EqualValue? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        internal EqualValue? ReferenceValue
        {
            get => GetValue(ReferenceValueProperty);
            set => SetValue(ReferenceValueProperty, value);
        }

        internal void SetReadOnlyText(string value) => SetValue(ReadOnlyTextKey, value);
        internal void ClearReadOnlyText() => ClearValue(ReadOnlyTextKey);
    }

    private sealed class ReferenceNode : UIElement
    {
        internal static readonly Property<EqualValue?> ValueProperty = Property.Register<ReferenceNode, EqualValue?>(
            "Value",
            comparer: EqualityComparer<EqualValue?>.Default);

        internal static readonly Property<EqualValue?> ReferenceValueProperty =
            Property.Register<ReferenceNode, EqualValue?>(
                "ReferenceValue", comparer: ReferenceEqualityComparer.Instance);
    }

    private sealed class ClrReferenceModel : INotifyPropertyChanged
    {
        private EqualValue? _value = new EqualValue(7);
        public event PropertyChangedEventHandler? PropertyChanged;
        internal int SetterCalls { get; set; }
        internal bool RejectEqualValues { get; init; }

        public EqualValue? Value
        {
            get => _value;
            set
            {
                SetterCalls++;
                if (RejectEqualValues && EqualityComparer<EqualValue?>.Default.Equals(_value, value))
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        }
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

    private sealed class AccessControlledModel : Model
    {
        internal bool RejectWrites { get; set; }

        protected override void VerifyMutationAccess()
        {
            if (RejectWrites)
                throw new InvalidOperationException("writes disabled");
        }
    }

    private sealed class ValueNode : UIElement
    {
        internal static readonly Property<int> ValueProperty = Property.Register<ValueNode, int>("Value");
    }
}