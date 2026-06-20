using System.Collections.Concurrent;
using PanguEngine.Events;

namespace PanguEngine.Tests.Events;

public sealed class EventBusTests
{
    [Fact]
    public void PublishDispatchesToMatchingEventBaseTypes()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<DerivedEvent>(_ => calls.Add("derived"));
        bus.Register<BaseEvent>(_ => calls.Add("base"));
        bus.Register<Event>(_ => calls.Add("event"));

        bus.Publish(new DerivedEvent());
        Assert.Equal(3, calls.Count);
        Assert.Contains("derived", calls);
        Assert.Contains("base", calls);
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
    public void ParentAndChildEventListenerListsRespectOrderBucketsAfterIncrementalRegistration()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<DerivedEvent>(_ => calls.Add("derived-default"));
        bus.Publish(new DerivedEvent());
        calls.Clear();
        bus.Register<BaseEvent>(_ => calls.Add("base-first"), Order.First);
        bus.Register<DerivedEvent>(_ => calls.Add("derived-early"), Order.Early);
        bus.Register<BaseEvent>(_ => calls.Add("base-last"), Order.Last);

        bus.Publish(new DerivedEvent());

        Assert.Contains("base-first", calls);
        Assert.Contains("derived-early", calls);
        Assert.Contains("derived-default", calls);
        Assert.Contains("base-last", calls);

        var baseFirst = calls.IndexOf("base-first");
        var derivedEarly = calls.IndexOf("derived-early");
        var derivedDefault = calls.IndexOf("derived-default");
        var baseLast = calls.IndexOf("base-last");

        Assert.True(baseFirst < derivedEarly);
        Assert.True(derivedEarly < derivedDefault);
        Assert.True(derivedDefault < baseLast);
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
    public void CanceledEventsOnlyReachListenersThatReceiveCanceledEvents()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<CancelableTestEvent>(eventInstance =>
        {
            calls.Add("cancel");
            eventInstance.Cancel();
        }, Order.First);
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

    [Fact]
    public async Task ConcurrentDuplicateDelegateRegistrationAllowsOnlyOneSuccess()
    {
        var bus = new EventBus(new TestExceptionHandler());
        Action<BaseEvent> listener = _ => { };
        var gate = new Barrier(2);

        Task<Exception?> RegisterAsync() => Task.Run(() =>
        {
            gate.SignalAndWait();
            try
            {
                bus.Register(listener);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        var results = await Task.WhenAll(RegisterAsync(), RegisterAsync());
        var failures = results.Where(exception => exception is not null).ToArray();

        Assert.Single(failures);
        Assert.IsType<InvalidOperationException>(failures[0]);
    }

    [Fact]
    public async Task ConcurrentPublishDoesNotThrowCollectionModificationException()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var errors = new ConcurrentBag<Exception>();
        var ready = new CountdownEvent(2);
        var release = new ManualResetEventSlim();
        var blockingListener = new Action<BaseEvent>(_ =>
        {
            ready.Signal();
            release.Wait();
        });

        bus.Register(blockingListener);

        Task PublishAsync() => Task.Run(() =>
        {
            try
            {
                bus.Publish(new BaseEvent());
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        });

        var first = PublishAsync();
        var second = PublishAsync();
        ready.Wait();
        bus.Register<BaseEvent>(_ => { }, Order.Last);
        bus.Unregister(blockingListener);
        release.Set();
        await Task.WhenAll(first, second);

        Assert.Empty(errors);
    }

    [Fact]
    public void PublishKeepsCurrentSnapshotWhenListenersRegisterDuringDispatch()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();
        var registered = false;

        bus.Register<BaseEvent>(_ =>
        {
            calls.Add("first");
            if (registered)
                return;

            registered = true;
            bus.Register<BaseEvent>(_ => calls.Add("late"), Order.Early);
        }, Order.First);
        bus.Register<BaseEvent>(_ => calls.Add("second"), Order.Last);

        bus.Publish(new BaseEvent());
        bus.Publish(new BaseEvent());

        Assert.Equal(["first", "second", "first", "late", "second"], calls);
    }

    [Fact]
    public void PublishKeepsCurrentSnapshotWhenListenersUnregisterDuringDispatch()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();
        Action<BaseEvent>? later = null;

        bus.Register<BaseEvent>(_ =>
        {
            calls.Add("first");
            if (later is not null)
                bus.Unregister(later);
        }, Order.First);
        later = eventInstance => calls.Add("second");
        bus.Register(later);

        bus.Publish(new BaseEvent());
        bus.Publish(new BaseEvent());

        Assert.Equal(["first", "second", "first"], calls);
    }

    [Fact]
    public void ListenerExceptionHandlerReceivesCurrentPublishSnapshot()
    {
        var handler = new TestExceptionHandler();
        var bus = new EventBus(handler);
        var calls = new List<string>();

        bus.Register<BaseEvent>(_ =>
        {
            calls.Add("first");
            bus.Register<BaseEvent>(_ => calls.Add("late"));
            throw new InvalidOperationException("boom");
        }, Order.First);
        bus.Register<BaseEvent>(_ => calls.Add("second"), Order.Last);

        bus.Publish(new BaseEvent());

        Assert.Equal(["first"], calls);
        Assert.NotNull(handler.Listeners);
        Assert.Equal(2, handler.Listeners.Count);
        Assert.Equal(Order.First, handler.Listeners[0].Order);
        Assert.Equal(Order.Last, handler.Listeners[1].Order);
    }

    [Fact]
    public void RegisterDuringDispatchDoesNotAffectCurrentExceptionSnapshot()
    {
        var handler = new TestExceptionHandler();
        var bus = new EventBus(handler);

        bus.Register<BaseEvent>(_ =>
        {
            bus.Register<BaseEvent>(_ => { }, Order.Last);
            throw new InvalidOperationException("boom");
        }, Order.First);
        bus.Register<BaseEvent>(_ => { }, Order.Last);

        bus.Publish(new BaseEvent());

        Assert.NotNull(handler.Listeners);
        Assert.Equal(2, handler.Listeners.Count);
        Assert.Equal(Order.First, handler.Listeners[0].Order);
        Assert.Equal(Order.Last, handler.Listeners[1].Order);
    }

    [Fact]
    public void NestedPublishWorksDuringListenerCallback()
    {
        var bus = new EventBus(new TestExceptionHandler());
        var calls = new List<string>();

        bus.Register<BaseEvent>(_ =>
        {
            calls.Add("outer-first");
            bus.Publish(new NestedEvent());
        }, Order.First);
        bus.Register<BaseEvent>(_ => calls.Add("outer-last"), Order.Last);
        bus.Register<NestedEvent>(_ => calls.Add("inner"));

        bus.Publish(new BaseEvent());

        Assert.Equal(["outer-first", "inner", "outer-last"], calls);
    }

    private sealed class TestExceptionHandler : IEventExceptionHandler
    {
        public IEventBus? Bus { get; private set; }

        public Event? Event { get; private set; }

        public IReadOnlyList<IEventListener>? Listeners { get; private set; }

        public int Index { get; private set; } = -1;

        public Exception? Exception { get; private set; }

        public void Handle(IEventBus bus, Event eventInstance, IReadOnlyList<IEventListener> listeners, int index,
            Exception exception)
        {
            Bus = bus;
            Event = eventInstance;
            Listeners = listeners;
            Index = index;
            Exception = exception;
        }
    }

    private class BaseEvent : Event
    {
    }

    private sealed class DerivedEvent : BaseEvent
    {
    }

    private sealed class NestedEvent : Event
    {
    }

    private sealed class CancelableTestEvent : Event, ICancelableEvent
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