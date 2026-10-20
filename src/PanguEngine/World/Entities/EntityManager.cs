using System.Diagnostics.CodeAnalysis;
using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

/// <summary>
/// Owns entity identities, facades, and required archetype component storage.
/// </summary>
public sealed class EntityManager
{
    private readonly IRegistry<EntityType> _entityTypes;
    private readonly int _ownerThreadId;
    private readonly List<EntitySlot> _slots = [];
    private readonly Stack<int> _freeSlots = [];
    private readonly Dictionary<int, Archetype> _archetypes = [];
    private bool _isUpdating;
    private bool _isDestroyed;

    /// <summary>
    /// Creates an entity manager from the frozen process-level entity definitions.
    /// </summary>
    public EntityManager() : this(GetBuiltinEntityTypes())
    {
    }

    internal EntityManager(IRegistry<EntityType> entityTypes)
    {
        if (!entityTypes.IsFrozen)
            throw new InvalidOperationException("Entity definitions must be frozen before creating a manager.");

        _entityTypes = entityTypes;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>The number of live entities.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Creates an entity with a complete set of required component values.
    /// </summary>
    /// <typeparam name="TEntity">The entity facade class.</typeparam>
    /// <param name="type">The registered entity type.</param>
    /// <param name="initialValues">The one-use required value builder.</param>
    /// <returns>The created entity facade.</returns>
    public TEntity Spawn<TEntity>(EntityType<TEntity> type, EntitySpawnBuilder initialValues)
        where TEntity : Entity
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(initialValues);
        var values = initialValues.Consume();
        EnsureThread();
        EnsureAvailable();
        EnsureNotUpdating();
        ValidateSpawn(type, values);

        var archetype = GetOrCreateArchetype(type);
        var slotIndex = AcquireSlot();
        var slot = _slots[slotIndex];
        var handle = new EntityHandle(slotIndex, slot.Generation);
        var row = -1;
        try
        {
            row = archetype.Add(handle, values);
            slot.IsAlive = true;
            slot.Type = type;
            slot.Archetype = archetype;
            slot.Row = row;

            var entity = type.Factory(this, handle) as TEntity
                         ?? throw new InvalidOperationException("The entity factory returned an invalid facade.");
            if (!ReferenceEquals(entity.Entities, this) || entity.Handle != handle)
                throw new InvalidOperationException("The entity factory returned a facade for another entity.");

            slot.Entity = entity;
            Count++;
            return entity;
        }
        catch
        {
            if (row >= 0)
                UpdateMovedRow(archetype.RemoveAtSwapBack(row), row);
            InvalidateAndRelease(slotIndex, slot);
            throw;
        }
    }

    /// <summary>
    /// Gets whether a handle identifies a live entity.
    /// </summary>
    /// <param name="handle">The entity handle.</param>
    /// <returns>Whether the entity is live.</returns>
    public bool IsAlive(EntityHandle handle)
    {
        EnsureThread();
        return TryResolveSlot(handle, out _);
    }

    /// <summary>
    /// Attempts to resolve a live entity facade.
    /// </summary>
    /// <param name="handle">The entity handle.</param>
    /// <param name="entity">The live facade when found.</param>
    /// <returns>Whether the entity was found.</returns>
    public bool TryGetEntity(EntityHandle handle, [NotNullWhen(true)] out Entity? entity)
    {
        EnsureThread();
        if (TryResolveSlot(handle, out var slot) && slot.Entity is not null)
        {
            entity = slot.Entity;
            return true;
        }

        entity = null;
        return false;
    }

    /// <summary>
    /// Destroys every entity and releases managed storage references.
    /// </summary>
    public void Destroy()
    {
        EnsureThread();
        if (_isDestroyed)
            return;
        EnsureNotUpdating();

        for (var index = 0; index < _slots.Count; index++)
        {
            var slot = _slots[index];
            if (slot.IsAlive)
                Destroy(new EntityHandle(index, slot.Generation));
        }

        _archetypes.Clear();
        _freeSlots.Clear();
        _slots.Clear();
        _isDestroyed = true;
    }

    internal T GetComponent<T>(EntityHandle handle, ComponentType<T> type) where T : unmanaged
    {
        EnsureThread();
        var slot = ResolveSlot(handle);
        return slot.Archetype!.Get(slot.Row, type);
    }

    internal void SetComponent<T>(EntityHandle handle, ComponentType<T> type, T value) where T : unmanaged
    {
        EnsureThread();
        var slot = ResolveSlot(handle);
        slot.Archetype!.Set(slot.Row, type, value);
    }

    internal EntityType GetEntityType(EntityHandle handle)
    {
        EnsureThread();
        return ResolveSlot(handle).Type!;
    }

    internal bool TryGetArchetype(EntityType type, [NotNullWhen(true)] out Archetype? archetype)
    {
        EnsureThread();
        EnsureAvailable();
        if (!_entityTypes.TryGetId(type, out var typeId))
        {
            archetype = null;
            return false;
        }

        return _archetypes.TryGetValue(typeId, out archetype);
    }

    internal void Destroy(EntityHandle handle)
    {
        EnsureThread();
        EnsureAvailable();
        EnsureNotUpdating();
        var slot = ResolveSlot(handle);
        UpdateMovedRow(slot.Archetype!.RemoveAtSwapBack(slot.Row), slot.Row);
        slot.Entity = null;
        slot.Type = null;
        slot.Archetype = null;
        slot.Row = -1;
        slot.IsAlive = false;
        slot.Generation = unchecked(slot.Generation + 1);
        _freeSlots.Push(handle.Index);
        Count--;
    }

    internal void BeginUpdate()
    {
        EnsureThread();
        EnsureAvailable();
        if (_isUpdating)
            throw new InvalidOperationException("Nested entity updates are not supported.");
        _isUpdating = true;
    }

    internal void EndUpdate()
    {
        EnsureThread();
        if (!_isUpdating)
            throw new InvalidOperationException("No entity update is active.");
        _isUpdating = false;
    }

    private void ValidateSpawn(EntityType type, IReadOnlyDictionary<ComponentType, object> values)
    {
        if (!_entityTypes.TryGetId(type, out _))
            throw new InvalidOperationException("The entity type is not registered in this entity registry.");
        if (values.Count != type.RequiredSignature.Count)
            throw new InvalidOperationException("The required component values do not match the entity type signature.");

        foreach (var componentType in type.RequiredSignature)
        {
            if (!values.ContainsKey(componentType))
                throw new InvalidOperationException("The required component values do not match the entity type signature.");
        }
    }

    private Archetype GetOrCreateArchetype(EntityType type)
    {
        var typeId = _entityTypes.GetId(type);
        if (_archetypes.TryGetValue(typeId, out var archetype))
            return archetype;

        archetype = new Archetype(type.RequiredSignature);
        _archetypes.Add(typeId, archetype);
        return archetype;
    }

    private static IRegistry<EntityType> GetBuiltinEntityTypes()
    {
        if (!BuiltinRegistries.EntityComponentType.IsFrozen || !BuiltinRegistries.EntityType.IsFrozen)
            throw new InvalidOperationException("Entity definitions must be frozen before creating a manager.");

        return BuiltinRegistries.EntityType;
    }

    private int AcquireSlot()
    {
        if (_freeSlots.TryPop(out var index))
            return index;

        _slots.Add(new EntitySlot());
        return _slots.Count - 1;
    }

    private void InvalidateAndRelease(int slotIndex, EntitySlot slot)
    {
        slot.Entity = null;
        slot.Type = null;
        slot.Archetype = null;
        slot.Row = -1;
        slot.IsAlive = false;
        slot.Generation = unchecked(slot.Generation + 1);
        _freeSlots.Push(slotIndex);
    }

    private void UpdateMovedRow(EntityHandle? movedHandle, int row)
    {
        if (movedHandle is { } handle)
            _slots[handle.Index].Row = row;
    }

    private EntitySlot ResolveSlot(EntityHandle handle)
    {
        return TryResolveSlot(handle, out var slot)
            ? slot
            : throw new InvalidOperationException("The entity handle is not alive.");
    }

    private bool TryResolveSlot(EntityHandle handle, out EntitySlot slot)
    {
        if (!_isDestroyed && handle.Index >= 0 && handle.Index < _slots.Count)
        {
            slot = _slots[handle.Index];
            if (slot.IsAlive && slot.Generation == handle.Generation)
                return true;
        }

        slot = null!;
        return false;
    }

    private void EnsureThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Entity storage must be accessed from its owner thread.");
    }

    private void EnsureAvailable()
    {
        if (_isDestroyed)
            throw new InvalidOperationException("The entity manager has been destroyed.");
    }

    private void EnsureNotUpdating()
    {
        if (_isUpdating)
            throw new InvalidOperationException("Entity structure cannot change during an update.");
    }

    private sealed class EntitySlot
    {
        internal uint Generation;
        internal bool IsAlive;
        internal Entity? Entity;
        internal EntityType? Type;
        internal Archetype? Archetype;
        internal int Row = -1;
    }
}
