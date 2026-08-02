using System.Runtime.CompilerServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiPropertyTests
{
    [Fact]
    public void PropertyDescriptorPreservesIdentityAndMetadata()
    {
        var property = TestNode.ValueProperty;

        Assert.Equal(nameof(TestNode.Value), property.Name);
        Assert.Equal(typeof(TestNode), property.OwnerType);
        Assert.Equal(typeof(TestNode), property.TargetType);
        Assert.Equal(typeof(int), property.ValueType);
        Assert.Equal(10, property.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Measure, property.Invalidation);
    }

    [Fact]
    public void DerivedOwnerCanUseBaseProperty()
    {
        var node = new DerivedTestNode();

        Assert.Equal(10, node.GetValue(TestNode.ValueProperty));
    }

    [Fact]
    public void ForeignOwnerIsRejectedByPropertyOperations()
    {
        var node = new ForeignNode();

        Assert.Throws<ArgumentException>(() => node.GetValue(TestNode.ValueProperty));
        Assert.Throws<ArgumentException>(() => node.SetValue(TestNode.ValueProperty, 1));
        Assert.Throws<ArgumentException>(() => node.ClearValue(TestNode.ValueProperty));
        Assert.Throws<ArgumentException>(() => node.Subscribe(TestNode.ValueProperty, (_, _) => { }));
        Assert.Throws<ArgumentException>(() => node.SubscribeWeak(TestNode.ValueProperty, (_, _) => { }));
    }

    [Fact]
    public void RegistrationRejectsInvalidNames()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UiProperty.Register<TestNode, int>(null!));
        Assert.Throws<ArgumentException>(() =>
            UiProperty.Register<TestNode, int>(string.Empty));
        Assert.Throws<ArgumentException>(() =>
            UiProperty.Register<TestNode, int>(" "));
    }

    [Fact]
    public void SubscribeRejectsNullArguments()
    {
        var node = new TestNode();

        Assert.Throws<ArgumentNullException>(() =>
            node.Subscribe<int>(null!, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() =>
            node.Subscribe(TestNode.ValueProperty, null!));
        Assert.Throws<ArgumentNullException>(() =>
            node.SubscribeWeak<int>(null!, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() =>
            node.SubscribeWeak(TestNode.ValueProperty, null!));
    }

    [Fact]
    public void RegistrationRejectsDuplicateOwnerAndNameRegardlessOfValueType()
    {
        _ = TestNode.ValueProperty;

        Assert.Throws<InvalidOperationException>(() =>
            UiProperty.Register<TestNode, int>(nameof(TestNode.Value), 20));
        Assert.Throws<InvalidOperationException>(() =>
            UiProperty.Register<TestNode, string>(nameof(TestNode.Value), "duplicate"));
    }

    [Fact]
    public void AttachedPropertySeparatesOwnerAndTargetTypes()
    {
        var property = AttachedOwner.ValueProperty;

        Assert.Equal("Value", property.Name);
        Assert.Equal(typeof(AttachedOwner), property.OwnerType);
        Assert.Equal(typeof(AttachedTarget), property.TargetType);
        Assert.Equal(typeof(int), property.ValueType);
        Assert.Equal(7, property.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Arrange, property.Invalidation);
    }

    [Fact]
    public void AttachedPropertyCanBeStoredOnTargetAndDerivedTarget()
    {
        var target = new AttachedTarget();
        var derived = new DerivedAttachedTarget();
        var changes = new List<(int OldValue, int NewValue)>();
        using var subscription = target.Subscribe(
            AttachedOwner.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        target.SetValue(AttachedOwner.ValueProperty, 9);
        derived.SetValue(AttachedOwner.ValueProperty, 11);

        Assert.Equal(9, target.GetValue(AttachedOwner.ValueProperty));
        Assert.Equal(11, derived.GetValue(AttachedOwner.ValueProperty));
        Assert.Equal([(7, 9)], changes);

        target.ClearValue(AttachedOwner.ValueProperty);

        Assert.Equal(7, target.GetValue(AttachedOwner.ValueProperty));
        Assert.Equal([(7, 9), (9, 7)], changes);
    }

    [Fact]
    public void AttachedPropertyRejectsUnrelatedTargetAcrossPropertyEntrypoints()
    {
        var node = new ForeignNode();
        var source = new AttachedTarget();

        Assert.Throws<ArgumentException>(() => node.GetValue(AttachedOwner.ValueProperty));
        Assert.Throws<ArgumentException>(() => node.SetValue(AttachedOwner.ValueProperty, 1));
        Assert.Throws<ArgumentException>(() => node.ClearValue(AttachedOwner.ValueProperty));
        Assert.Throws<ArgumentException>(() =>
            node.Subscribe(AttachedOwner.ValueProperty, (_, _) => { }));
        Assert.Throws<ArgumentException>(() =>
            node.SubscribeWeak(AttachedOwner.ValueProperty, (_, _) => { }));
        Assert.Throws<ArgumentException>(() =>
            node.Bind(AttachedOwner.ValueProperty, source, AttachedOwner.ValueProperty));
        Assert.Throws<ArgumentException>(() => node.IsBound(AttachedOwner.ValueProperty));
        Assert.Throws<ArgumentException>(() => node.Unbind(AttachedOwner.ValueProperty));
    }

    [Fact]
    public void AttachedRegistrationUsesOwnerAndNameAsTheUniqueKey()
    {
        var name = $"Attached_{Guid.NewGuid():N}";
        _ = UiProperty.RegisterAttached<AttachedOwner, AttachedTarget, int>(name);

        Assert.Throws<InvalidOperationException>(() =>
            UiProperty.Register<AttachedOwner, int>(name));
        Assert.Throws<InvalidOperationException>(() =>
            UiProperty.RegisterAttached<AttachedOwner, DerivedAttachedTarget, int>(name));
        Assert.Throws<InvalidOperationException>(() =>
            UiProperty.RegisterAttached<AttachedOwner, AttachedTarget, string>(name));
    }

    [Fact]
    public async Task ConcurrentRegistrationAtomicallyAcceptsOneDescriptor()
    {
        var name = $"Concurrent_{Guid.NewGuid():N}";
        var barrier = new Barrier(2);

        static Task<Exception?> RegisterAsync(Barrier barrier, string name)
        {
            return Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    UiProperty.Register<ConcurrentNode, int>(name);
                    return (Exception?)null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });
        }

        var results = await Task.WhenAll(
            RegisterAsync(barrier, name),
            RegisterAsync(barrier, name));

        Assert.Single(results.Where(exception => exception is null));
        var failure = Assert.Single(results.Where(exception => exception is not null));
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void DefaultValueAndEqualAssignmentsDoNotNotify()
    {
        var node = new TestNode();
        var changes = new List<(int OldValue, int NewValue)>();
        node.Subscribe(TestNode.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        node.Value = 10;
        node.Value = 12;
        node.Value = 12;

        Assert.Equal(12, node.Value);
        Assert.Equal([(10, 12)], changes);
    }

    [Fact]
    public void ClearValueRestoresDefaultAndNotifiesOnlyWhenEffectiveValueChanges()
    {
        var node = new TestNode();
        var changes = new List<(int OldValue, int NewValue)>();
        node.Subscribe(TestNode.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        node.ClearValue(TestNode.ValueProperty);
        node.Value = 12;
        node.ClearValue(TestNode.ValueProperty);
        node.ClearValue(TestNode.ValueProperty);

        Assert.Equal(10, node.Value);
        Assert.Equal([(10, 12), (12, 10)], changes);
    }

    [Fact]
    public void ReferenceValuesUseDefaultEqualityComparer()
    {
        var node = new TestNode();
        var notifications = 0;
        node.Subscribe(TestNode.TextProperty, (_, _) => notifications++);

        node.Text = new string('x', 3);
        node.Text = new string('x', 3);

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void SubscribeDoesNotSendCurrentValueAndSupportsMultipleSubscriptions()
    {
        var node = new TestNode();
        var calls = new List<string>();
        var first = node.Subscribe(TestNode.ValueProperty, (_, _) => calls.Add("first"));
        var second = node.Subscribe(TestNode.ValueProperty, (_, _) => calls.Add("second"));

        Assert.Empty(calls);
        node.Value = 11;
        second.Dispose();
        node.Value = 12;
        first.Dispose();
        first.Dispose();
        node.Value = 13;

        Assert.Equal(["first", "second", "first"], calls);
    }

    [Fact]
    public void WeakSubscriptionInvokesWhileHandlerIsAlive()
    {
        var node = new TestNode();
        var calls = 0;
        EventHandler<UiPropertyChangedEventArgs<int>> handler = (_, _) => calls++;
        using var subscription = node.SubscribeWeak(TestNode.ValueProperty, handler);

        node.Value = 11;

        Assert.Equal(1, calls);
        GC.KeepAlive(handler);
    }

    [Fact]
    public void WeakSubscriptionTokenStopsNotificationsWhileHandlerIsAlive()
    {
        var node = new TestNode();
        var calls = 0;
        EventHandler<UiPropertyChangedEventArgs<int>> handler = (_, _) => calls++;
        var subscription = node.SubscribeWeak(TestNode.ValueProperty, handler);

        subscription.Dispose();
        subscription.Dispose();
        node.Value = 11;

        Assert.Equal(0, calls);
        GC.KeepAlive(handler);
    }

    [Fact]
    public void WeakSubscriptionStopsAfterHandlerIsCollected()
    {
        var node = new TestNode();
        var calls = new List<int>();
        var weakHandler = CreateWeakSubscription(node, calls, out var subscription);

        GC.Collect();
        GC.Collect();

        Assert.False(weakHandler.TryGetTarget(out _));
        node.Value = 11;
        node.Value = 12;
        Assert.Empty(calls);

        subscription.Dispose();
        subscription.Dispose();
    }

    [Fact]
    public void NotificationUsesSenderAndOrderedSnapshots()
    {
        var node = new TestNode();
        var calls = new List<string>();
        var globalCalls = 0;
        IDisposable? secondSubscription = null;

        node.PropertyChanged += (sender, args) =>
        {
            Assert.Same(node, sender);
            Assert.Same(TestNode.ValueProperty, args.Property);
            if (globalCalls == 0)
            {
                Assert.Equal(10, args.OldValue);
                Assert.Equal(11, args.NewValue);
            }

            globalCalls++;
            calls.Add("global");
            secondSubscription!.Dispose();
        };
        node.Subscribe(TestNode.ValueProperty, (_, _) => calls.Add("first"));
        secondSubscription = node.Subscribe(TestNode.ValueProperty, (_, _) => calls.Add("second"));

        node.Value = 11;
        node.Value = 12;

        Assert.Equal(["global", "first", "second", "global", "first"], calls);
    }

    [Fact]
    public void NestedNotificationUsesUpdatedSubscriptionsWhileOuterUsesOriginalSnapshot()
    {
        var node = new TestNode();
        var calls = new List<string>();
        IDisposable? secondSubscription = null;
        IDisposable? thirdSubscription = null;
        using var firstSubscription = node.Subscribe(TestNode.ValueProperty, (_, args) =>
        {
            calls.Add($"first:{args.NewValue}");
            if (args.NewValue != 11)
                return;

            secondSubscription!.Dispose();
            thirdSubscription = node.Subscribe(
                TestNode.ValueProperty,
                (_, nestedArgs) => calls.Add($"third:{nestedArgs.NewValue}"));
            node.Value = 12;
        });
        secondSubscription = node.Subscribe(
            TestNode.ValueProperty,
            (_, args) => calls.Add($"second:{args.NewValue}"));

        node.Value = 11;

        Assert.Equal(["first:11", "first:12", "third:12", "second:11"], calls);
        thirdSubscription!.Dispose();
    }

    [Fact]
    public void NotificationExceptionDoesNotPreventLaterSubscriptionChanges()
    {
        var node = new TestNode();
        var exception = new InvalidOperationException("notification failed");
        var existingNotifications = 0;
        using var existingSubscription = node.Subscribe(
            TestNode.ValueProperty,
            (_, _) => existingNotifications++);
        EventHandler<UiPropertyChangedEventArgs> throwingHandler = (_, _) => throw exception;
        node.PropertyChanged += throwingHandler;

        var actual = Assert.Throws<InvalidOperationException>(() => node.Value = 11);

        Assert.Same(exception, actual);
        Assert.Equal(0, existingNotifications);
        node.PropertyChanged -= throwingHandler;
        var addedNotifications = 0;
        using var addedSubscription = node.Subscribe(
            TestNode.ValueProperty,
            (_, _) => addedNotifications++);
        node.Value = 12;
        Assert.Equal(1, existingNotifications);
        Assert.Equal(1, addedNotifications);
    }

    [Fact]
    public void NotificationExceptionPropagatesAfterValueIsCommitted()
    {
        var node = new TestNode();
        var exception = new InvalidOperationException("notification failed");
        var laterCalls = 0;
        node.PropertyChanged += (_, _) => throw exception;
        node.Subscribe(TestNode.ValueProperty, (_, _) => laterCalls++);

        var actual = Assert.Throws<InvalidOperationException>(() => node.Value = 11);

        Assert.Same(exception, actual);
        Assert.Equal(11, node.Value);
        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public void DerivedOnPropertyChangedCanPreserveBaseDispatch()
    {
        var node = new TracingNode();
        var notifications = 0;
        node.Subscribe(TestNode.ValueProperty, (_, _) => notifications++);

        node.Value = 11;

        Assert.Equal(1, node.OnPropertyChangedCalls);
        Assert.Equal(1, notifications);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<EventHandler<UiPropertyChangedEventArgs<int>>> CreateWeakSubscription(
        TestNode node,
        List<int> calls,
        out IDisposable subscription)
    {
        EventHandler<UiPropertyChangedEventArgs<int>> handler = (_, args) => calls.Add(args.NewValue);
        var weakHandler = new WeakReference<EventHandler<UiPropertyChangedEventArgs<int>>>(handler);
        subscription = node.SubscribeWeak(TestNode.ValueProperty, handler);
        return weakHandler;
    }

    private class TestNode : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<TestNode, int>(nameof(Value), 10, UiPropertyInvalidation.Measure);

        internal static readonly UiProperty<string> TextProperty =
            UiProperty.Register<TestNode, string>(nameof(Text), string.Empty);

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

    private sealed class DerivedTestNode : TestNode
    {
    }

    private sealed class ForeignNode : UiNode
    {
    }

    private sealed class ConcurrentNode : UiNode
    {
    }

    private sealed class AttachedOwner : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.RegisterAttached<AttachedOwner, AttachedTarget, int>(
                "Value",
                7,
                UiPropertyInvalidation.Arrange);
    }

    private class AttachedTarget : UiNode
    {
    }

    private sealed class DerivedAttachedTarget : AttachedTarget
    {
    }

    private sealed class TracingNode : TestNode
    {
        internal int OnPropertyChangedCalls { get; private set; }

        protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
        {
            OnPropertyChangedCalls++;
            base.OnPropertyChanged(eventArgs);
        }
    }
}
