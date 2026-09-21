using System.Collections.ObjectModel;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Client.Input;

/// <summary>
/// Describes a runtime input binding slot.
/// </summary>
public sealed record InputBindingEntry(
    ResourceKey Key,
    ResourceKey ActionKey,
    InputAction Action,
    ResourceKey ContextKey,
    InputContext Context,
    InputSource Source,
    KeyModifiers Modifiers,
    Vector3D<double> Scale,
    int Priority,
    int Order,
    bool IsEnabled);

internal readonly record struct InputBindingChange(
    InputBindingEntry Before,
    InputBindingEntry After);

/// <summary>
/// Stores mutable runtime bindings copied from frozen action definitions.
/// </summary>
public sealed class InputBindingMap
{
    private readonly List<BindingSlot> _slots;
    private readonly Dictionary<ResourceKey, BindingSlot> _slotsByKey;

    private InputBindingMap(List<BindingSlot> slots)
    {
        _slots = slots;
        _slotsByKey = slots.ToDictionary(slot => slot.Key);
        Entries = CreateEntries();
    }

    /// <summary>The current runtime binding entries.</summary>
    public IReadOnlyList<InputBindingEntry> Entries { get; private set; }

    internal event Action<IReadOnlyList<InputBindingChange>>? Changed;

    /// <summary>
    /// Creates a runtime binding map from frozen input definition registries.
    /// </summary>
    /// <param name="actionRegistry">The frozen input action registry.</param>
    /// <param name="contextRegistry">The frozen input context registry.</param>
    /// <returns>The created runtime map.</returns>
    public static InputBindingMap CreateFromRegistries(
        IRegistry<InputAction> actionRegistry,
        IRegistry<InputContext> contextRegistry)
    {
        ArgumentNullException.ThrowIfNull(actionRegistry);
        ArgumentNullException.ThrowIfNull(contextRegistry);
        if (!actionRegistry.IsFrozen || !contextRegistry.IsFrozen)
            throw new InvalidOperationException("Input definition registries must be frozen.");

        var slots = new List<BindingSlot>();
        var slotKeys = new HashSet<ResourceKey>();
        foreach (var actionEntry in actionRegistry.Entries)
        {
            var action = actionEntry.Value;
            foreach (var definition in action.Bindings)
            {
                var contextKey = contextRegistry.GetKey(definition.Context);
                var key = ResourceKey.Create(
                    actionEntry.Key.Namespace,
                    $"{actionEntry.Key.Path}/{definition.Name}");
                if (!slotKeys.Add(key))
                    throw new InvalidOperationException($"Input binding key '{key}' is declared more than once.");
                ValidateCompatibility(key, actionEntry.Key, action, contextKey, definition.Source);
                slots.Add(new BindingSlot(
                    key,
                    actionEntry.Key,
                    action,
                    contextKey,
                    definition.Context,
                    definition.Source,
                    definition.Modifiers,
                    definition.Scale,
                    definition.Priority,
                    slots.Count));
            }
        }

        return new InputBindingMap(slots);
    }

    /// <summary>
    /// Replaces the physical source and modifiers of a binding slot.
    /// </summary>
    public void SetBinding(ResourceKey key, InputSource source, KeyModifiers modifiers)
    {
        var slot = GetSlot(key);
        ValidateCompatibility(slot.Key, slot.ActionKey, slot.Action, slot.ContextKey, source);
        if (slot.IsEnabled && slot.Source == source && slot.Modifiers == modifiers)
            return;

        var before = slot.ToEntry();
        slot.Source = source;
        slot.Modifiers = modifiers;
        slot.IsEnabled = true;
        PublishChanges([new InputBindingChange(before, slot.ToEntry())]);
    }

    /// <summary>Disables a binding slot.</summary>
    public void Disable(ResourceKey key)
    {
        var slot = GetSlot(key);
        if (!slot.IsEnabled)
            return;
        var before = slot.ToEntry();
        slot.IsEnabled = false;
        PublishChanges([new InputBindingChange(before, slot.ToEntry())]);
    }

    /// <summary>Restores a binding slot to its default source and modifiers.</summary>
    public void Reset(ResourceKey key)
    {
        var slot = GetSlot(key);
        if (slot.IsDefault)
            return;
        var before = slot.ToEntry();
        slot.Reset();
        PublishChanges([new InputBindingChange(before, slot.ToEntry())]);
    }

    /// <summary>Restores every binding slot to its default source and modifiers.</summary>
    public void ResetAll()
    {
        var changes = new List<InputBindingChange>();
        foreach (var slot in _slots)
        {
            if (slot.IsDefault)
                continue;
            var before = slot.ToEntry();
            slot.Reset();
            changes.Add(new InputBindingChange(before, slot.ToEntry()));
        }

        if (changes.Count != 0)
            PublishChanges(changes.ToArray());
    }

    /// <summary>Finds enabled slots whose source and modifiers overlap the requested binding.</summary>
    public IReadOnlyList<InputBindingEntry> FindConflicts(InputSource source, KeyModifiers modifiers) =>
        _slots
            .Where(slot => slot.IsEnabled &&
                           slot.Source == source &&
                           (modifiers == KeyModifiers.None ||
                            slot.Modifiers == KeyModifiers.None ||
                            slot.Modifiers == modifiers))
            .Select(static slot => slot.ToEntry())
            .ToArray();

    private ReadOnlyCollection<InputBindingEntry> CreateEntries() =>
        Array.AsReadOnly(_slots.Select(static slot => slot.ToEntry()).ToArray());

    private void PublishChanges(InputBindingChange[] changes)
    {
        Entries = CreateEntries();
        Changed?.Invoke(changes);
    }

    private BindingSlot GetSlot(ResourceKey key) =>
        _slotsByKey.TryGetValue(key, out var slot)
            ? slot
            : throw new KeyNotFoundException($"Input binding key '{key}' is not registered.");

    private static void ValidateCompatibility(
        ResourceKey key,
        ResourceKey actionKey,
        InputAction action,
        ResourceKey contextKey,
        InputSource source)
    {
        var digital = source.Type is InputSourceType.Key or InputSourceType.MouseButton;
        var state = action.ValueType is InputValueType.Button
            or InputValueType.Axis1D
            or InputValueType.Axis2D
            or InputValueType.Axis3D;
        var transientAxis = source.Type is InputSourceType.MouseMove or InputSourceType.MouseWheel &&
                            action.ValueType is InputValueType.Axis1D or InputValueType.Axis2D;
        if (digital && state || transientAxis)
            return;

        throw new InvalidOperationException(
            $"Input binding '{key}' in context '{contextKey}' maps source '{source.Type}' " +
            $"to incompatible action '{actionKey}' with value type '{action.ValueType}'.");
    }

    private sealed class BindingSlot
    {
        private readonly InputSource _defaultSource;
        private readonly KeyModifiers _defaultModifiers;

        internal BindingSlot(
            ResourceKey key,
            ResourceKey actionKey,
            InputAction action,
            ResourceKey contextKey,
            InputContext context,
            InputSource source,
            KeyModifiers modifiers,
            Vector3D<double> scale,
            int priority,
            int order)
        {
            Key = key;
            ActionKey = actionKey;
            Action = action;
            ContextKey = contextKey;
            Context = context;
            Source = _defaultSource = source;
            Modifiers = _defaultModifiers = modifiers;
            Scale = scale;
            Priority = priority;
            Order = order;
            IsEnabled = true;
        }

        internal ResourceKey Key { get; }
        internal ResourceKey ActionKey { get; }
        internal InputAction Action { get; }
        internal ResourceKey ContextKey { get; }
        private InputContext Context { get; }
        internal InputSource Source { get; set; }
        internal KeyModifiers Modifiers { get; set; }
        private Vector3D<double> Scale { get; }
        private int Priority { get; }
        private int Order { get; }
        internal bool IsEnabled { get; set; }
        internal bool IsDefault =>
            IsEnabled && Source == _defaultSource && Modifiers == _defaultModifiers;

        internal void Reset()
        {
            Source = _defaultSource;
            Modifiers = _defaultModifiers;
            IsEnabled = true;
        }

        internal InputBindingEntry ToEntry() =>
            new(Key, ActionKey, Action, ContextKey, Context, Source, Modifiers, Scale, Priority, Order, IsEnabled);
    }
}
