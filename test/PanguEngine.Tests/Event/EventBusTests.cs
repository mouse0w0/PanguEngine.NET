using PanguEngine.Event;

namespace PanguEngine.Tests.Event;

public sealed class EventBusTests
{
    [Fact]
    public void PublishDispatchesToMatchingEventBaseTypesAndInterfaces()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<DerivedEvent>(_ => calls.Add("derived"));
        bus.Register<BaseEvent>(_ => calls.Add("base"));
        bus.Register<ITestEvent>(_ => calls.Add("interface"));
        bus.Register<IEvent>(_ => calls.Add("event"));

        bus.Publish(new DerivedEvent());
        Assert.Equal(3, calls.Count);
        Assert.Contains("derived", calls);
        Assert.Contains("base", calls);
        Assert.Contains("event", calls);

        calls.Clear();
        bus.Publish(new InterfaceEvent());

        Assert.Equal(2, calls.Count);
        Assert.Contains("interface", calls);
        Assert.Contains("event", calls);
    }

    [Fact]
    public void PublishDispatchesByOrder()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<BaseEvent>(_ => calls.Add("late"), Order.Late);
        bus.Register<BaseEvent>(_ => calls.Add("first"), Order.First);
        bus.Register<BaseEvent>(_ => calls.Add("default"));
        bus.Register<BaseEvent>(_ => calls.Add("early"), Order.Early);
        bus.Register<BaseEvent>(_ => calls.Add("last"), Order.Last);

        bus.Publish(new BaseEvent());

        Assert.Equal(["first", "early", "default", "late", "last"], calls);
    }

    [Fact]
    public void RegisterParentListenerUpdatesExistingChildEventLists()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<DerivedEvent>(_ => calls.Add("derived"));
        bus.Publish(new DerivedEvent());
        bus.Register<BaseEvent>(_ => calls.Add("base"));

        bus.Publish(new DerivedEvent());

        Assert.Equal("derived", calls[0]);
        Assert.Equal(3, calls.Count);
        Assert.Contains("derived", calls.Skip(1));
        Assert.Contains("base", calls.Skip(1));
    }

    [Fact]
    public void ParentAndChildEventListenerListsRemainOrderedAfterIncrementalRegistration()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<DerivedEvent>(_ => calls.Add("derived-default"));
        bus.Publish(new DerivedEvent());
        bus.Register<BaseEvent>(_ => calls.Add("base-first"), Order.First);
        bus.Register<DerivedEvent>(_ => calls.Add("derived-early"), Order.Early);
        bus.Register<BaseEvent>(_ => calls.Add("base-last"), Order.Last);

        bus.Publish(new DerivedEvent());

        Assert.Equal(["derived-default", "base-first", "derived-early", "derived-default", "base-last"], calls);
    }

    [Fact]
    public void RegisterChildListenerDoesNotUpdateExistingParentEventLists()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<BaseEvent>(_ => calls.Add("base"));
        bus.Publish(new BaseEvent());
        bus.Register<DerivedEvent>(_ => calls.Add("derived"));

        bus.Publish(new BaseEvent());
        bus.Publish(new DerivedEvent());

        Assert.Equal("base", calls[0]);
        Assert.Equal("base", calls[1]);
        Assert.Equal(4, calls.Count);
        Assert.Contains("base", calls.Skip(2));
        Assert.Contains("derived", calls.Skip(2));
    }

    [Fact]
    public void UnregisterParentListenerRemovesItFromExistingChildEventLists()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();
        var listener = new InstanceListener();

        bus.Register<DerivedEvent>(_ => calls.Add("derived"));
        bus.Register(listener);
        bus.Publish(new DerivedEvent());

        bus.Unregister(listener);
        bus.Publish(new DerivedEvent());

        Assert.Equal([nameof(DerivedEvent)], listener.Calls);
        Assert.Equal(["derived", "derived"], calls);
    }

    [Fact]
    public void PublishRejectsBoxedValueTypeEvents()
    {
        var bus = new EventBus(new TestExceptionHandler());
        IEvent eventInstance = new ValueTypeEvent();

        Assert.Throws<ArgumentException>(() => bus.Publish(eventInstance));
    }

    [Fact]
    public void CanceledEventsOnlyReachListenersThatReceiveCanceledEvents()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<CancelableTestEvent>(eventInstance =>
        {
            calls.Add("cancel");
            eventInstance.Cancel();
        });
        bus.Register<CancelableTestEvent>(_ => calls.Add("skipped"));
        bus.Register<CancelableTestEvent>(_ => calls.Add("received"), receiveCanceled: true);

        bus.Publish(new CancelableTestEvent());

        Assert.Equal(["cancel", "received"], calls);
    }

    [Fact]
    public void ListenerExceptionStopsDispatchAndCallsExceptionHandler()
    {
        var handler = new TestExceptionHandler();
        var bus = new EventBus(handler);
        var calls = new List<string>();
        var eventInstance = new BaseEvent();
        var exception = new InvalidOperationException("listener failed");

        bus.Register<BaseEvent>(_ => throw exception, Order.First);
        bus.Register<BaseEvent>(_ => calls.Add("after"), Order.Last);

        bus.Publish(eventInstance);

        Assert.Empty(calls);
        Assert.Same(bus, handler.Bus);
        Assert.Same(eventInstance, handler.Event);
        Assert.Same(exception, handler.Exception);
        Assert.Equal(0, handler.Index);
        Assert.NotNull(handler.Listeners);
        Assert.Equal(2, handler.Listeners.Count);
        Assert.Equal(typeof(BaseEvent), handler.Listeners[handler.Index].EventType);
    }

    [Fact]
    public void ThrowingExceptionHandlerRethrowsListenerExceptions()
    {
        var bus = new EventBus(ThrowingEventExceptionHandler.Instance);
        var exception = new InvalidOperationException("listener failed");

        bus.Register<BaseEvent>(_ => throw exception);

        var actual = Assert.Throws<InvalidOperationException>(() => bus.Publish(new BaseEvent()));

        Assert.Same(exception, actual);
    }

    [Fact]
    public void RegisterScansInstanceAndStaticListenerMethods()
    {
        StaticListener.Clear();
        var bus = new EventBus(new TestExceptionHandler());
        var instance = new InstanceListener();
        object staticListenerType = typeof(StaticListener);

        bus.Register(instance);
        bus.Register(staticListenerType);
        bus.Publish(new BaseEvent());

        Assert.Equal([nameof(BaseEvent)], instance.Calls);
        Assert.Equal([nameof(BaseEvent)], StaticListener.Calls);
    }

    [Fact]
    public void ListenerMethodExceptionUnwrapsInvocationExceptionAndProvidesListenerMetadata()
    {
        var handler = new TestExceptionHandler();
        var bus = new EventBus(handler);
        var listener = new ThrowingInstanceListener();
        var exception = new InvalidOperationException("listener failed");

        listener.Exception = exception;
        bus.Register(listener);

        bus.Publish(new BaseEvent());

        Assert.Same(bus, handler.Bus);
        Assert.Same(exception, handler.Exception);
        Assert.NotNull(handler.Listeners);
        Assert.Single(handler.Listeners);
        Assert.Equal(typeof(ThrowingInstanceListener), handler.Listeners[0].OwnerType);
        Assert.Equal("OnBase", handler.Listeners[0].Method.Name);
    }

    [Fact]
    public void RegisterDoesNotScanInheritedListenerMethods()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var listener = new DerivedInheritedListener();

        var exception = Assert.Throws<InvalidOperationException>(() => bus.Register(listener));

        Assert.Contains("does not declare any listener methods", exception.Message);
    }

    [Fact]
    public void UnregisterRemovesInstanceAndStaticListeners()
    {
        StaticListener.Clear();
        var bus = new EventBus(new TestExceptionHandler());
        var instance = new InstanceListener();

        bus.Register(instance);
        bus.Register(typeof(StaticListener));
        bus.Unregister(instance);
        bus.Unregister(typeof(StaticListener));
        bus.Publish(new BaseEvent());

        Assert.Empty(instance.Calls);
        Assert.Empty(StaticListener.Calls);
    }

    [Fact]
    public void UnregisterRemovesDelegateListener()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = 0;
        Action<BaseEvent> listener = _ => calls++;

        bus.Register(listener);
        bus.Unregister<BaseEvent>(listener);
        bus.Publish(new BaseEvent());

        Assert.Equal(0, calls);
    }

    [Fact]
    public void UnregisterIgnoresUnregisteredInstanceTypeAndDelegate()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var instance = new InstanceListener();
        Action<BaseEvent> listener = _ => { };

        bus.Unregister(instance);
        bus.Unregister(typeof(StaticListener));
        bus.Unregister<BaseEvent>(listener);
    }

    [Fact]
    public void RegisterRejectsDuplicateInstanceTypeAndDelegateRegistrations()
    {
        StaticListener.Clear();
        var bus = new EventBus(new TestExceptionHandler());
        var instance = new InstanceListener();
        Action<BaseEvent> listener = _ => { };

        bus.Register(instance);
        bus.Register(typeof(StaticListener));
        bus.Register(listener);

        Assert.Throws<InvalidOperationException>(() => bus.Register(instance));
        Assert.Throws<InvalidOperationException>(() => bus.Register(typeof(StaticListener)));
        Assert.Throws<InvalidOperationException>(() => bus.Register(listener));
    }

    [Fact]
    public void RegisterRejectsEmptyAndInvalidListenerTypesWithoutMarkingThemRegistered()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var empty = new EmptyListener();
        var invalid = new InvalidSignatureListener();

        var firstEmpty = Assert.Throws<InvalidOperationException>(() => bus.Register(empty));
        var secondEmpty = Assert.Throws<InvalidOperationException>(() => bus.Register(empty));
        var firstInvalid = Assert.Throws<InvalidOperationException>(() => bus.Register(invalid));
        var secondInvalid = Assert.Throws<InvalidOperationException>(() => bus.Register(invalid));

        Assert.Contains("does not declare any listener methods", firstEmpty.Message);
        Assert.Contains("does not declare any listener methods", secondEmpty.Message);
        Assert.Contains("must have exactly one parameter", firstInvalid.Message);
        Assert.Contains("must have exactly one parameter", secondInvalid.Message);
    }

    private sealed class TestExceptionHandler : IEventExceptionHandler
    {
        public IEventBus? Bus { get; private set; }

        public IEvent? Event { get; private set; }

        public IReadOnlyList<IEventListener>? Listeners { get; private set; }

        public int Index { get; private set; } = -1;

        public Exception? Exception { get; private set; }

        public void Handle(IEventBus bus, IEvent eventInstance, IReadOnlyList<IEventListener> listeners, int index,
            Exception exception)
        {
            Bus = bus;
            Event = eventInstance;
            Listeners = listeners;
            Index = index;
            Exception = exception;
        }
    }

    private class BaseEvent : IEvent
    {
    }

    private sealed class DerivedEvent : BaseEvent
    {
    }

    private interface ITestEvent : IEvent
    {
    }

    private sealed class InterfaceEvent : ITestEvent
    {
    }

    private readonly struct ValueTypeEvent : IEvent
    {
    }

    private sealed class CancelableTestEvent : ICancelableEvent
    {
        public bool IsCanceled { get; private set; }

        public void Cancel()
        {
            IsCanceled = true;
        }
    }

    private sealed class InstanceListener
    {
        public readonly List<string> Calls = [];

        [Listener]
        private void OnBase(BaseEvent eventInstance)
        {
            Calls.Add(eventInstance.GetType().Name);
        }
    }

    private sealed class ThrowingInstanceListener
    {
        public Exception Exception { get; set; } = null!;

        [Listener]
        private void OnBase(BaseEvent eventInstance)
        {
            throw Exception;
        }
    }

    private class InheritedListenerBase
    {
        [Listener]
        protected void OnBase(BaseEvent eventInstance)
        {
        }
    }

    private sealed class DerivedInheritedListener : InheritedListenerBase
    {
    }

    private static class StaticListener
    {
        public static readonly List<string> Calls = [];

        [Listener]
        private static void OnBase(BaseEvent eventInstance)
        {
            Calls.Add(eventInstance.GetType().Name);
        }

        public static void Clear()
        {
            Calls.Clear();
        }
    }

    private sealed class EmptyListener
    {
    }

    private sealed class InvalidSignatureListener
    {
        [Listener]
        private void Invalid()
        {
        }
    }
}