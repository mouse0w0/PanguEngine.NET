using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiPropertyCallbackTests
{
    [Fact]
    public void SetAndClearNotifyAfterCallbackWithCommittedValues()
    {
        var node = new CallbackNode();
        node.PropertyChanged += (_, _) => node.Trace.Add("event");
        using var subscription = node.Subscribe(CallbackNode.ValueProperty, (_, _) => node.Trace.Add("subscription"));
        node.Changed = (oldValue, newValue) =>
        {
            Assert.Equal(newValue, node.GetValue(CallbackNode.ValueProperty));
            node.Changes.Add((oldValue, newValue));
        };

        Assert.Equal(0, node.GetValue(CallbackNode.ValueProperty));
        node.SetValue(CallbackNode.ValueProperty, 0);
        node.ClearValue(CallbackNode.ValueProperty);
        Assert.Empty(node.Trace);

        node.SetValue(CallbackNode.ValueProperty, 7);
        node.SetValue(CallbackNode.ValueProperty, 7);
        node.ClearValue(CallbackNode.ValueProperty);

        Assert.Equal([(0, 7), (7, 0)], node.Changes);
        Assert.Equal(["internal", "virtual", "event", "subscription",
            "internal", "virtual", "event", "subscription"], node.Trace);
    }

    [Fact]
    public void OverrideWithoutBaseCannotBypassInternalCallback()
    {
        var node = new NonDispatchingNode();
        node.PropertyChanged += (_, _) => node.Trace.Add("event");
        using var subscription = node.Subscribe(CallbackNode.ValueProperty, (_, _) => node.Trace.Add("subscription"));

        node.SetValue(CallbackNode.ValueProperty, 3);

        Assert.Equal(["internal", "override"], node.Trace);
    }

    [Fact]
    public void ReadOnlyKeySetAndClearInvokeCallback()
    {
        var node = new CallbackNode();
        node.SetReadOnlyValue(4);
        node.ClearReadOnlyValue();

        Assert.Equal([(0, 4), (4, 0)], node.Changes);
    }

    [Fact]
    public void BindingWritesInvokeInternalCallback()
    {
        var source = new CallbackNode();
        var target = new CallbackNode();
        source.SetValue(CallbackNode.ValueProperty, 2);
        target.Changed = (oldValue, newValue) => target.Changes.Add((oldValue, newValue));

        target.Bind(CallbackNode.ValueProperty, source, CallbackNode.ValueProperty);
        source.SetValue(CallbackNode.ValueProperty, 5);
        target.ClearValue(CallbackNode.ValueProperty);

        Assert.Equal([(0, 2), (2, 5), (5, 0)], target.Changes);
    }

    [Fact]
    public void InternalFailurePreservesValueAndSkipsAllExternalNotifications()
    {
        var error = new InvalidOperationException("internal");
        var node = new CallbackNode { Changed = (_, _) => throw error };
        node.PropertyChanged += (_, _) => node.Trace.Add("event");
        using var subscription = node.Subscribe(CallbackNode.ValueProperty, (_, _) => node.Trace.Add("subscription"));

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => node.SetValue(CallbackNode.ValueProperty, 1)));
        Assert.Equal(1, node.GetValue(CallbackNode.ValueProperty));
        node.SetValue(CallbackNode.ValueProperty, 1);
        Assert.Equal(["internal"], node.Trace);
    }

    [Fact]
    public void ExternalFailurePreservesInternalEffects()
    {
        var error = new InvalidOperationException("external");
        var node = new CallbackNode();
        node.Changed = (oldValue, newValue) => node.Changes.Add((oldValue, newValue));
        node.PropertyChanged += (_, _) => throw error;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => node.SetValue(CallbackNode.ValueProperty, 1)));

        Assert.Equal([(0, 1)], node.Changes);
        Assert.Equal(1, node.GetValue(CallbackNode.ValueProperty));
    }

    [Fact]
    public void StyleBatchCommitsAllValuesBeforeCallbacksAndContinuesAfterFailure()
    {
        var error = new InvalidOperationException("internal");
        var node = new CallbackNode();
        var screen = new UiScreen(node);
        node.Changed = (_, _) =>
        {
            Assert.Equal(8, node.GetValue(CallbackNode.OtherProperty));
            throw error;
        };
        var notified = new List<UiProperty>();
        node.PropertyChanged += (_, args) => notified.Add(args.Property);

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => screen.SetStyleSheets([
            new UiStyleSheet([new UiStyleRule(UiStyleSelector.For<CallbackNode>(), [
                UiStyleSetter.Create(CallbackNode.ValueProperty, 7),
                UiStyleSetter.Create(CallbackNode.OtherProperty, 8)])])])));

        Assert.Equal(7, node.GetValue(CallbackNode.ValueProperty));
        Assert.Equal([CallbackNode.OtherProperty], notified);
        Assert.Equal(["internal", "other", "virtual"], node.Trace);
    }

    [Fact]
    public void ClassRecomputeAndClearLocalValueInvokeCallbacks()
    {
        var node = new CallbackNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<CallbackNode>(classes: ["active"]), [
                UiStyleSetter.Create(CallbackNode.ValueProperty, 7)])])]);
        node.Changed = (oldValue, newValue) => node.Changes.Add((oldValue, newValue));

        node.Classes.Add("active");
        node.SetValue(CallbackNode.ValueProperty, 9);
        node.ClearValue(CallbackNode.ValueProperty);
        node.Classes.Remove("active");

        Assert.Equal([(0, 7), (7, 9), (9, 7), (7, 0)], node.Changes);
    }

    private class CallbackNode : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<CallbackNode, int>("Value", onChanged: static (node, oldValue, newValue) =>
            {
                var target = (CallbackNode)node;
                target.Trace.Add("internal");
                target.Changed?.Invoke(oldValue, newValue);
            });

        internal static readonly UiProperty<int> OtherProperty =
            UiProperty.Register<CallbackNode, int>("Other", onChanged: static (node, _, _) => ((CallbackNode)node).Trace.Add("other"));

        private static readonly UiPropertyKey<int> ReadOnlyKey =
            UiProperty.RegisterReadOnly<CallbackNode, int>("ReadOnly", onChanged: static (node, oldValue, newValue) =>
                ((CallbackNode)node).Changes.Add((oldValue, newValue)));

        internal List<string> Trace { get; } = [];
        internal List<(int OldValue, int NewValue)> Changes { get; } = [];
        internal Action<int, int>? Changed { get; set; }
        internal void SetReadOnlyValue(int value) => SetValue(ReadOnlyKey, value);
        internal void ClearReadOnlyValue() => ClearValue(ReadOnlyKey);

        protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
        {
            Trace.Add("virtual");
            base.OnPropertyChanged(eventArgs);
        }
    }

    private sealed class NonDispatchingNode : CallbackNode
    {
        protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs) => Trace.Add("override");
    }
}
