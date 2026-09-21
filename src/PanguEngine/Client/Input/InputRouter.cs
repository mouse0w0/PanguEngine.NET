using System.Runtime.ExceptionServices;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Client.Input;

internal sealed class InputRouter
{
    private readonly InputBindingMap _bindings;
    private readonly InputActionStateStore _stateStore = new();
    private readonly Dictionary<InputContext, ActiveContext> _contexts = [];
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly HashSet<InputSource> _pressed = [];
    private readonly HashSet<InputSource> _suppressed = [];
    private readonly Queue<Action> _pendingContextChanges = [];
    private readonly HashSet<InputContext> _pendingScreenResets = [];
    private readonly List<InputBindingChange> _pendingBindingChanges = [];
    private InputRouteIndex _routeIndex;
    private Dictionary<InputAction, HandlerRegistration[]> _handlerSnapshots = [];
    private ActiveContext[] _orderedContexts = [];
    private long _nextContextSequence;
    private long _nextHandlerSequence;
    private KeyModifiers _currentModifiers;
    private bool _modifiersChanged;
    private bool _recordedPressIsNew;
    private int _dispatchDepth;
    private bool _stopDispatch;
    private bool _handlersFrozen;
    private bool _destroyed;

    internal InputRouter(InputBindingMap bindings)
    {
        _bindings = bindings;
        _routeIndex = InputRouteIndex.Create(bindings.Entries);
        bindings.Changed += OnBindingsChanged;
    }

    internal event Action? StateInvalidated;

    internal void NotifyUiTopologyChanged(InputContext? previousContext)
    {
        if (previousContext is not null)
            _pendingScreenResets.Add(previousContext);
        ScheduleContextChange(static () => { });
    }

    internal IDisposable ActivateContext(InputContext context)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var registration = new ContextRegistration(context);
        if (_contexts.TryGetValue(context, out var existing))
        {
            existing.Count++;
            registration.IsActive = true;
            return new CallbackToken(() => DeactivateContext(registration));
        }

        ScheduleContextChange(() =>
        {
            if (registration.IsReleased)
                return;
            if (_contexts.TryGetValue(context, out var pendingExisting))
                pendingExisting.Count++;
            else
                _contexts.Add(context, new ActiveContext(context, ++_nextContextSequence));
            registration.IsActive = true;
        });

        return new CallbackToken(() => DeactivateContext(registration));
    }

    internal void RegisterHandler(
        InputAction action,
        Func<InputActionEvent, InputHandling> handler,
        int priority = 0)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_handlersFrozen)
            throw new InvalidOperationException("Input action handlers are frozen after input starts.");
        ArgumentNullException.ThrowIfNull(handler);
        _handlers.Add(new HandlerRegistration(action, handler, priority, ++_nextHandlerSequence));
    }

    internal void FreezeHandlers()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_handlersFrozen)
            return;

        _handlerSnapshots = _handlers
            .GroupBy(static registration => registration.Action)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(static registration => registration.Priority)
                    .ThenBy(static registration => registration.Sequence)
                    .ToArray());
        _handlers.Clear();
        _handlersFrozen = true;
    }

    internal InputActionValue GetValue(InputAction action)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        foreach (var activeContext in _orderedContexts)
        {
            if (_stateStore.TryGetValue(activeContext.Context, action, out var value))
                return value;
        }

        return InputActionValue.Zero;
    }

    internal void BeginInputEvent()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        _dispatchDepth++;
    }

    internal void EndInputEvent()
    {
        if (--_dispatchDepth != 0)
            return;

        _dispatchDepth = 1;
        try
        {
            ApplyPendingChanges();
        }
        finally
        {
            _dispatchDepth = 0;
            _modifiersChanged = false;
            _stopDispatch = false;
        }
    }

    internal void RecordPress(InputSource source, KeyModifiers modifiers)
    {
        _modifiersChanged = modifiers != _currentModifiers;
        _currentModifiers = modifiers;
        _recordedPressIsNew = _pressed.Add(source);
    }

    internal void RecordRelease(InputSource source, KeyModifiers modifiers)
    {
        _modifiersChanged = modifiers != _currentModifiers;
        _currentModifiers = modifiers;
        _pressed.Remove(source);
        _suppressed.Remove(source);
    }

    internal void RoutePress(InputSource source, bool skipNewRouting)
    {
        if (_modifiersChanged)
        {
            var maintenance = _stateStore.WithdrawInvalidModifiers(_currentModifiers, source);
            _modifiersChanged = false;
            DispatchRequiredEvents(maintenance);
        }
        if (!_recordedPressIsNew || skipNewRouting || _suppressed.Contains(source) || _stopDispatch)
            return;

        RouteStateSource(source, _currentModifiers);
    }

    internal void RouteRelease(InputSource source)
    {
        var maintenance = _stateStore.Release(source, _currentModifiers, _modifiersChanged);
        _modifiersChanged = false;
        DispatchRequiredEvents(maintenance);
    }

    internal void RouteSample(
        InputSource source,
        Vector2D<double> sample,
        KeyModifiers modifiers,
        bool skipNewRouting)
    {
        var modifiersChanged = modifiers != _currentModifiers;
        _currentModifiers = modifiers;
        if (modifiersChanged)
        {
            var maintenance = _stateStore.WithdrawInvalidModifiers(modifiers, source);
            DispatchRequiredEvents(maintenance);
        }
        if (skipNewRouting || _stopDispatch || sample == Vector2D<double>.Zero)
            return;

        RouteTransientSample(source, sample, modifiers);
    }

    internal void Reset()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        BeginInputEvent();
        Exception? error = null;
        try
        {
            var maintenance = _stateStore.CancelAll();
            _pressed.Clear();
            _suppressed.Clear();
            _currentModifiers = KeyModifiers.None;
            _modifiersChanged = false;
            _recordedPressIsNew = false;
            StateInvalidated?.Invoke();
            DispatchRequiredEvents(maintenance);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            try
            {
                EndInputEvent();
            }
            catch (Exception exception)
            {
                error = error is null ? exception : new AggregateException(error, exception);
            }
        }

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;

        try
        {
            var maintenance = _stateStore.CancelAll();
            StateInvalidated?.Invoke();
            DispatchRequiredEvents(maintenance);
        }
        finally
        {
            _bindings.Changed -= OnBindingsChanged;
            _contexts.Clear();
            _handlers.Clear();
            _handlerSnapshots = [];
            _pressed.Clear();
            _suppressed.Clear();
            _pendingContextChanges.Clear();
            _pendingScreenResets.Clear();
            _pendingBindingChanges.Clear();
            _orderedContexts = [];
            StateInvalidated = null;
            _destroyed = true;
        }
    }

    private void RouteStateSource(InputSource source, KeyModifiers modifiers)
    {
        var category = GetCaptureMask(source);
        foreach (var activeContext in _orderedContexts)
        {
            var candidates = _routeIndex.GetCandidates(source, activeContext.Context, modifiers);
            foreach (var candidate in candidates)
            {
                if (!_stateStore.Activate(candidate, out var eventArgs))
                    continue;
                var handling = InvokeHandlers(eventArgs);
                if (handling == InputHandling.Handled || _stopDispatch)
                    return;
            }

            if ((activeContext.Context.CaptureMask & category) != 0)
                return;
        }
    }

    private void RouteTransientSample(
        InputSource source,
        Vector2D<double> sample,
        KeyModifiers modifiers)
    {
        var category = GetCaptureMask(source);
        foreach (var activeContext in _orderedContexts)
        {
            var candidates = _routeIndex.GetCandidates(source, activeContext.Context, modifiers);
            foreach (var candidate in candidates)
            {
                var value = candidate.Action.ValueType switch
                {
                    InputValueType.Axis1D => InputActionValue.FromAxis1D(
                        sample.X * candidate.Scale.X + sample.Y * candidate.Scale.Y),
                    InputValueType.Axis2D => InputActionValue.FromAxis2D(new Vector2D<double>(
                        sample.X * candidate.Scale.X,
                        sample.Y * candidate.Scale.Y)),
                    InputValueType.Button or InputValueType.Axis3D => throw new InvalidOperationException(
                        $"Action type '{candidate.Action.ValueType}' cannot accept a transient sample."),
                    _ => throw new InvalidOperationException(
                        $"Action type '{candidate.Action.ValueType}' cannot accept a transient sample.")
                };
                var eventArgs = new InputActionEvent(
                    candidate.Action,
                    activeContext.Context,
                    InputActionPhase.Updated,
                    value,
                    source);
                if (InvokeHandlers(eventArgs) == InputHandling.Handled || _stopDispatch)
                    return;
            }

            if ((activeContext.Context.CaptureMask & category) != 0)
                return;
        }
    }

    private void DispatchRequiredEvents(IReadOnlyList<InputActionEvent> events)
    {
        foreach (var eventArgs in events)
            _ = InvokeHandlers(eventArgs, stopOnTopologyChange: false);
    }

    private InputHandling InvokeHandlers(
        InputActionEvent eventArgs,
        bool stopOnTopologyChange = true)
    {
        if (!_handlerSnapshots.TryGetValue(eventArgs.Action, out var handlers))
            return InputHandling.Pass;

        foreach (var registration in handlers)
        {
            if (registration.Handler(eventArgs) == InputHandling.Handled)
                return InputHandling.Handled;
            if (stopOnTopologyChange && _stopDispatch)
                return InputHandling.Pass;
        }
        return InputHandling.Pass;
    }

    private void DeactivateContext(ContextRegistration registration)
    {
        registration.IsReleased = true;
        if (_destroyed || !registration.IsActive)
            return;
        registration.IsActive = false;
        var context = registration.Context;
        if (!_contexts.TryGetValue(context, out var active))
            return;
        if (active.Count > 1)
        {
            active.Count--;
            return;
        }

        ScheduleContextChange(() =>
        {
            if (!_contexts.TryGetValue(context, out var pendingActive))
                return;
            if (pendingActive.Count > 1)
                pendingActive.Count--;
            else
                _contexts.Remove(context);
        });
    }

    private void OnBindingsChanged(IReadOnlyList<InputBindingChange> changes)
    {
        foreach (var change in changes)
            _pendingBindingChanges.Add(change);
        _stopDispatch = true;
        if (_dispatchDepth == 0)
        {
            BeginInputEvent();
            EndInputEvent();
        }
    }

    private void ScheduleContextChange(Action change)
    {
        _pendingContextChanges.Enqueue(change);
        _stopDispatch = true;
        if (_dispatchDepth == 0)
        {
            BeginInputEvent();
            EndInputEvent();
        }
    }

    private void ApplyPendingChanges()
    {
        try
        {
            while (_pendingContextChanges.Count != 0 || _pendingBindingChanges.Count != 0)
            {
                var slotIds = new HashSet<ResourceKey>();
                var contextsChanged = _pendingContextChanges.Count != 0;
                if (contextsChanged)
                {
                    var canceledCategories = _orderedContexts.ToDictionary(
                        static active => active.Context,
                        static _ => InputCaptureMask.All);
                    while (_pendingContextChanges.TryDequeue(out var change))
                        change();
                    RebuildOrderedContexts();

                    var captured = InputCaptureMask.None;
                    foreach (var active in _orderedContexts)
                    {
                        if (canceledCategories.ContainsKey(active.Context))
                        {
                            canceledCategories[active.Context] = _pendingScreenResets.Contains(active.Context)
                                ? InputCaptureMask.All
                                : captured;
                        }
                        captured |= active.Context.CaptureMask;
                    }
                    _pendingScreenResets.Clear();

                    foreach (var entry in _bindings.Entries)
                    {
                        if (canceledCategories.TryGetValue(entry.Context, out var categories)
                            && (categories & GetCaptureMask(entry.Source)) != 0)
                        {
                            slotIds.Add(entry.Key);
                            SuppressIfPressed(entry.Source);
                        }
                    }
                }

                if (_pendingBindingChanges.Count != 0)
                {
                    foreach (var change in _pendingBindingChanges)
                    {
                        slotIds.Add(change.Before.Key);
                        SuppressIfPressed(change.Before.Source);
                        SuppressIfPressed(change.After.Source);
                    }
                    _pendingBindingChanges.Clear();
                    RebuildRouteIndex();
                }

                var maintenance = _stateStore.RemoveBindings(slotIds);
                if (contextsChanged)
                    StateInvalidated?.Invoke();
                DispatchRequiredEvents(maintenance);
            }
        }
        catch
        {
            _pendingContextChanges.Clear();
            _pendingScreenResets.Clear();
            _pendingBindingChanges.Clear();
            throw;
        }
    }

    private void RebuildRouteIndex() => _routeIndex = InputRouteIndex.Create(_bindings.Entries);

    private void SuppressIfPressed(InputSource source)
    {
        if (_pressed.Contains(source))
            _suppressed.Add(source);
    }

    private void RebuildOrderedContexts()
    {
        var contexts = _contexts.Values.ToArray();
        Array.Sort(contexts, CompareContexts);
        _orderedContexts = contexts;
    }

    private static int CompareContexts(ActiveContext left, ActiveContext right)
    {
        var scopeComparison = GetScopeOrder(left.Context.Scope).CompareTo(GetScopeOrder(right.Context.Scope));
        return scopeComparison != 0 ? scopeComparison : right.Sequence.CompareTo(left.Sequence);
    }

    private static int GetScopeOrder(InputScope scope) => scope == InputScope.Ui ? 0 : 1;

    private static InputCaptureMask GetCaptureMask(InputSource source) => source.Type switch
    {
        InputSourceType.Key => InputCaptureMask.Keyboard,
        InputSourceType.MouseButton => InputCaptureMask.MouseButton,
        InputSourceType.MouseMove => InputCaptureMask.MouseMove,
        InputSourceType.MouseWheel => InputCaptureMask.MouseWheel,
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private sealed class ActiveContext(InputContext context, long sequence)
    {
        internal InputContext Context { get; } = context;
        internal long Sequence { get; } = sequence;
        internal int Count { get; set; } = 1;
    }

    private sealed class ContextRegistration(InputContext context)
    {
        internal InputContext Context { get; } = context;
        internal bool IsActive { get; set; }
        internal bool IsReleased { get; set; }
    }

    private sealed record HandlerRegistration(
        InputAction Action,
        Func<InputActionEvent, InputHandling> Handler,
        int Priority,
        long Sequence);

    private sealed class CallbackToken(Action callback) : IDisposable
    {
        private Action? _callback = callback;

        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }
}
